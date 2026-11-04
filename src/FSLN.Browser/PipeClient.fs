namespace FSLN

open System
open System.IO
open System.IO.Pipes
open System.Text

type PipeClient private (pipe: NamedPipeClientStream, writer: StreamWriter) =
    member this.SendMessage(message: string) : unit =
        if isNull message then
            nullArg(nameof message)

        if message.Contains('\r') || message.Contains('\n') then
            raise(ArgumentException("Message must not contain CR or LF.", nameof message))

        // Serialize writes in case multiple threads call SendMessage.
        lock
            writer
            (fun () ->
                writer.Write(message)
                writer.Write('\n')
                writer.Flush()
            )

    interface IDisposable with
        member _.Dispose() : unit =
            writer.Dispose()
            pipe.Dispose()

    static member EstablishConnection(timeoutMs: int) : PipeClient =
        let pipe =
            new NamedPipeClientStream(".", "EXAMPLE", PipeDirection.InOut, PipeOptions.None)

        try
            pipe.Connect(timeoutMs)

            let encoding = UTF8Encoding(false)
            let reader = new StreamReader(pipe, encoding, false, 1024, true)
            let writer = new StreamWriter(pipe, encoding, 1024, true)

            // First line is the PID registration message.
            writer.WriteLine(Environment.ProcessId)
            writer.Flush()

            // Expect one newline-terminated confirmation from the controller.
            let confirmation = reader.ReadLine()

            if confirmation <> "OK" then
                raise(IOException($"Unexpected controller confirmation: {confirmation}"))

            new PipeClient(pipe, writer)
        with _ ->
            pipe.Dispose()
            reraise()
