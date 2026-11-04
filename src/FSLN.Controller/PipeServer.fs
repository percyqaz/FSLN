namespace FSLN.Controller

open System
open System.Collections.Concurrent
open System.IO
open System.IO.Pipes
open System.Text
open System.Threading
open System.Threading.Tasks

type PipeCommandServer() =
    let active_pipes = ConcurrentDictionary<Guid, NamedPipeServerStream>()
    let reader_tasks = ConcurrentDictionary<Guid, Task>()

    let mutable cts: CancellationTokenSource option = None
    let mutable accept_task: Task option = None

    // Override this in a derived class.
    member this.HandleCommand(pid: int, command: string) : unit = printfn "%i: %s" pid command

    member private this.HandleClientAsync(id: Guid, pipe: NamedPipeServerStream, token: CancellationToken) : Task =
        task {
            try
                try
                    use reader = new StreamReader(pipe, UTF8Encoding(false), false, 1024, true)
                    use writer = new StreamWriter(pipe, UTF8Encoding(false), 1024, true)

                    let! pid_message = reader.ReadLineAsync(token)
                    let mutable pid = 0

                    if not(Int32.TryParse(pid_message, &pid)) then
                        raise(InvalidDataException("Invalid PID registration."))

                    writer.WriteLine("OK")
                    do! writer.FlushAsync(token)

                    let mutable reading = true

                    while reading && not token.IsCancellationRequested do
                        let! command = reader.ReadLineAsync(token)

                        if isNull command then reading <- false else this.HandleCommand(pid, command)
                with
                | :? OperationCanceledException -> ()
                | :? IOException -> ()
            finally
                active_pipes.TryRemove(id) |> ignore
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

                active_pipes.[id] <- pipe

                try
                    do! pipe.WaitForConnectionAsync(token)

                    let reader_task = this.HandleClientAsync(id, pipe, token)
                    reader_tasks.[id] <- reader_task

                    // Remove completed tasks from the tracking dictionary.
                    reader_task.ContinueWith(
                        (fun (_: Task) -> reader_tasks.TryRemove(id) |> ignore),
                        TaskScheduler.Default
                    )
                    |> ignore
                with
                | :? OperationCanceledException
                | :? ObjectDisposedException
                | :? IOException ->
                    active_pipes.TryRemove(id) |> ignore
                    pipe.Dispose()
        }

    /// Start listening. Returns once the accept loop has been started.
    member this.StartAsync() : Task =
        lock
            this
            (fun () ->
                match cts with
                | Some _ -> invalidOp "Server is already started."
                | None ->
                    cts <- Some(new CancellationTokenSource())
                    accept_task <- Some(this.AcceptLoopAsync(cts.Value.Token))
                    Task.CompletedTask
            )

    /// Stop accepting connections, close active pipes, and wait for tasks to end.
    member this.StopAsync() : Task =
        task {
            let state =
                lock
                    this
                    (fun () ->
                        let old_cts = cts
                        let old_accept_task = accept_task
                        cts <- None
                        accept_task <- None
                        old_cts, old_accept_task
                    )

            match state with
            | Some cts, Some accept_task ->
                cts.Cancel()

                for KeyValue(_, pipe) in active_pipes do
                    pipe.Dispose()

                try
                    do! accept_task
                with :? OperationCanceledException ->
                    ()

                try
                    do! Task.WhenAll(reader_tasks.Values |> Seq.toArray)
                with
                | :? OperationCanceledException -> ()
                | :? IOException -> ()

                cts.Dispose()
            | _ -> ()
        }
