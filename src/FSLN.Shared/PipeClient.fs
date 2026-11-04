namespace FSLN.Shared

open System
open System.IO
open System.IO.Pipes
open System.Text

type PipeClient private (pipe: NamedPipeClientStream, writer: StreamWriter) =
    member this.SendMessage(message: string) : unit =
        if message.Contains('\r') || message.Contains('\n') then
            failwith "Message cannot contain CR or LF"

        lock
            writer
            (fun () ->
                writer.Write(message)
                writer.Write('\n')
                writer.Flush()
            )

    static member TryConnect() : Result<PipeClient, string> =
        let pipe =
            new NamedPipeClientStream(".", "EXAMPLE", PipeDirection.InOut, PipeOptions.None)

        try
            pipe.Connect(0)

            let encoding = UTF8Encoding(false)
            let reader = new StreamReader(pipe, encoding, false, 1024, true)
            let writer = new StreamWriter(pipe, encoding, 1024, true)

            writer.WriteLine(Environment.ProcessId)
            writer.Flush()

            let confirmation = reader.ReadLine()

            if confirmation <> "" then
                failwith $"Unexpected controller confirmation: '{confirmation}'"

            Ok(new PipeClient(pipe, writer))
        with error ->
            pipe.Dispose()
            Error(error.Message)

    interface IDisposable with
        member _.Dispose() : unit =
            writer.Dispose()
            pipe.Dispose()
