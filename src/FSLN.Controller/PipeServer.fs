namespace FSLN.Controller

open System
open System.Collections.Concurrent
open System.IO
open System.IO.Pipes
open System.Text
open System.Threading
open System.Threading.Tasks

type PipeCommandServer() =
    let gate = obj()
    let activePipes = ConcurrentDictionary<Guid, NamedPipeServerStream>()
    let clientTasks = ConcurrentDictionary<Guid, Task>()

    let mutable cts: CancellationTokenSource option = None
    let mutable acceptTask: Task option = None

    // Override this in a derived class.
    member this.HandleCommand(pid: int, command: string) : unit = printfn "%i: %s" pid command

    member private this.HandleClientAsync(id: Guid, pipe: NamedPipeServerStream, token: CancellationToken) : Task =
        task {
            try
                try
                    use reader = new StreamReader(pipe, UTF8Encoding(false), false, 1024, true)

                    use writer = new StreamWriter(pipe, UTF8Encoding(false), 1024, true)

                    // First line is the PID registration message.
                    let! pidLine = reader.ReadLineAsync(token)

                    let mutable pid = 0

                    if not(Int32.TryParse(pidLine, &pid)) then
                        raise(InvalidDataException("Invalid PID registration."))

                    writer.WriteLine("OK")
                    do! writer.FlushAsync(token)

                    let mutable keepReading = true

                    while keepReading && not token.IsCancellationRequested do
                        let! command = reader.ReadLineAsync(token)

                        if isNull command then keepReading <- false else this.HandleCommand(pid, command)
                with
                | :? OperationCanceledException -> ()
                | :? IOException -> ()
            finally
                activePipes.TryRemove(id) |> ignore
                pipe.Dispose()
        }

    member private this.AcceptLoopAsync(token: CancellationToken) : Task =
        task {
            while not token.IsCancellationRequested do
                let id = Guid.NewGuid()

                let pipe =
                    new NamedPipeServerStream(
                        "EXAMPLE",
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous
                    )

                activePipes.[id] <- pipe

                try
                    do! pipe.WaitForConnectionAsync(token)

                    let clientTask = this.HandleClientAsync(id, pipe, token)
                    clientTasks.[id] <- clientTask

                    // Remove completed tasks from the tracking dictionary.
                    clientTask.ContinueWith(
                        (fun (_: Task) -> clientTasks.TryRemove(id) |> ignore),
                        TaskScheduler.Default
                    )
                    |> ignore
                with
                | :? OperationCanceledException
                | :? ObjectDisposedException
                | :? IOException ->
                    activePipes.TryRemove(id) |> ignore
                    pipe.Dispose()
        }

    /// Start listening. Returns once the accept loop has been started.
    member this.StartAsync() : Task =
        lock
            gate
            (fun () ->
                match cts with
                | Some _ -> invalidOp "Server is already started."
                | None ->
                    cts <- Some(new CancellationTokenSource())
                    acceptTask <- Some(this.AcceptLoopAsync(cts.Value.Token))
                    Task.CompletedTask
            )

    /// Stop accepting connections, close active pipes, and wait for tasks to end.
    member this.StopAsync() : Task =
        task {
            let state =
                lock
                    gate
                    (fun () ->
                        let oldCts = cts
                        let oldAcceptTask = acceptTask
                        cts <- None
                        acceptTask <- None
                        oldCts, oldAcceptTask
                    )

            match state with
            | Some stopCts, Some listenerTask ->
                stopCts.Cancel()

                // Closing these also unblocks clients waiting for their next command.
                for KeyValue(_, pipe) in activePipes do
                    pipe.Dispose()

                try
                    do! listenerTask
                with :? OperationCanceledException ->
                    ()

                let runningClients = clientTasks.Values |> Seq.toArray

                try
                    do! Task.WhenAll(runningClients)
                with
                | :? OperationCanceledException -> ()
                | :? IOException -> ()

                stopCts.Dispose()

            | _ -> ()
        }
