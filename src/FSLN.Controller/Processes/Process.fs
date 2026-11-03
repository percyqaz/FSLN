namespace FSLN.Controller

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open FSLN.Controller.Config
open Porta.Pty
open XTerm
open XTerm.Input
open XTerm.Options

type ProcessState =
    | Running
    | Exited of int
    | WaitingKeypress of int

[<Interface>]
type IState =
    abstract member IsVisible: Process -> bool
    abstract member Rerender: unit -> unit

and Process =
    {
        Config: ProcessConfig
        Terminal: Terminal
        Pty: IPtyConnection
        ExitSignal: TaskCompletionSource<int>
        ReadCancellation: CancellationTokenSource
        mutable State: ProcessState
    }

    static member StartAsync(config: ProcessConfig) : Task<Process> =
        let cols, rows = 64, 32

        let pty_options =
            let app, command_line =
                if OperatingSystem.IsWindows() then
                    "cmd.exe", [| "/c"; config.Command |]
                else
                    "/bin/sh", [| "-c"; config.Command |]

            let env = Dictionary()
            env.["TERM"] <- "xterm-256color"
            env.["COLORTERM"] <- "truecolor"

            PtyOptions(
                Name = "FSLN.Controller",
                App = app,
                CommandLine = command_line,
                VerbatimCommandLine = OperatingSystem.IsWindows(),
                Cwd = Environment.CurrentDirectory,
                Cols = cols,
                Rows = rows,
                Environment = env
            )

        let toptions =
            TerminalOptions(Cols = cols, Rows = rows, Scrollback = 200, TermName = "xterm-256color")

        let terminal = new Terminal(toptions)

        let exit_signal =
            TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)

        let read_cancellation = new CancellationTokenSource()

        task {
            let! pty = PtyProvider.SpawnAsync(pty_options, CancellationToken.None)

            pty.ProcessExited.AddHandler(
                EventHandler<PtyExitedEventArgs>(fun _ event_args ->
                    exit_signal.TrySetResult(event_args.ExitCode) |> ignore
                )
            )

            if pty.WaitForExit(0) then
                exit_signal.TrySetResult(pty.ExitCode) |> ignore

            terminal.DataReceived.AddHandler(
                EventHandler<XTerm.Events.TerminalEvents.DataEventArgs>(fun _ event_args ->
                    try
                        let bytes = Encoding.UTF8.GetBytes(event_args.Data)
                        pty.WriterStream.Write(bytes, 0, bytes.Length)
                        pty.WriterStream.Flush()
                    with _ ->
                        ()
                )
            )

            return
                {
                    Config = config
                    Terminal = terminal
                    Pty = pty
                    ExitSignal = exit_signal
                    State = Running
                    ReadCancellation = read_cancellation
                }
        }

    member this.HasExitSignal: bool = this.ExitSignal.Task.IsCompleted

    member this.StartRenderLoop(state: IState) : Task =
        let output_loop () =
            task {
                let bytes = Array.zeroCreate<byte> 8192
                let mutable reading = true

                while reading do
                    try
                        let! count = this.Pty.ReaderStream.ReadAsync(bytes.AsMemory(), this.ReadCancellation.Token)

                        if count = 0 then
                            reading <- false
                        else
                            lock
                                state
                                (fun () ->
                                    this.Terminal.Write(ReadOnlySpan<byte>(bytes, 0, count))

                                    if state.IsVisible(this) then
                                        state.Rerender()
                                )
                    with
                    | :? OperationCanceledException
                    | :? ObjectDisposedException
                    | :? IOException -> reading <- false
                    | other -> raise(other)
            }
            :> Task
            
        Task.Run(Func<Task>(output_loop))

    member this.SendInterrupt() : unit =
        if not(this.HasExitSignal) then
            let bytes = Encoding.UTF8.GetBytes("\u0003")
            this.Pty.WriterStream.Write(bytes, 0, bytes.Length)
            this.Pty.WriterStream.Flush()

    member this.SendKey(key: ConsoleKeyInfo) : unit =

        let key_modifiers =
            let mutable result = KeyModifiers.None

            if key.Modifiers.HasFlag(ConsoleModifiers.Shift) then
                result <- result ||| KeyModifiers.Shift

            if key.Modifiers.HasFlag(ConsoleModifiers.Alt) then
                result <- result ||| KeyModifiers.Alt

            if key.Modifiers.HasFlag(ConsoleModifiers.Control) then
                result <- result ||| KeyModifiers.Control

            result

        let special =
            match key.Key with
            | ConsoleKey.Enter -> Some Key.Enter
            | ConsoleKey.Tab -> Some Key.Tab
            | ConsoleKey.Backspace -> Some Key.Backspace
            | ConsoleKey.Escape -> Some Key.Escape
            | ConsoleKey.Spacebar -> Some Key.Space
            | ConsoleKey.UpArrow -> Some Key.UpArrow
            | ConsoleKey.DownArrow -> Some Key.DownArrow
            | ConsoleKey.RightArrow -> Some Key.RightArrow
            | ConsoleKey.LeftArrow -> Some Key.LeftArrow
            | ConsoleKey.Home -> Some Key.Home
            | ConsoleKey.End -> Some Key.End
            | ConsoleKey.PageUp -> Some Key.PageUp
            | ConsoleKey.PageDown -> Some Key.PageDown
            | ConsoleKey.Insert -> Some Key.Insert
            | ConsoleKey.Delete -> Some Key.Delete
            | ConsoleKey.F1 -> Some Key.F1
            | ConsoleKey.F2 -> Some Key.F2
            | ConsoleKey.F3 -> Some Key.F3
            | ConsoleKey.F4 -> Some Key.F4
            | ConsoleKey.F5 -> Some Key.F5
            | ConsoleKey.F6 -> Some Key.F6
            | ConsoleKey.F7 -> Some Key.F7
            | ConsoleKey.F8 -> Some Key.F8
            | ConsoleKey.F9 -> Some Key.F9
            | ConsoleKey.F10 -> Some Key.F10
            | ConsoleKey.F11 -> Some Key.F11
            | ConsoleKey.F12 -> Some Key.F12
            | _ -> None

        let send_input (input: string) : unit =
            let bytes = Encoding.UTF8.GetBytes(input)
            this.Pty.WriterStream.Write(bytes, 0, bytes.Length)
            this.Pty.WriterStream.Flush()

        match this.State with
        | Running ->
            match special with
            | Some value -> send_input(this.Terminal.GenerateKeyInput(value, key_modifiers))
            | None when key.KeyChar <> '\000' -> send_input(this.Terminal.GenerateCharInput(key.KeyChar, key_modifiers))
            | None -> ()
        | WaitingKeypress exit_code -> this.State <- Exited exit_code
        | Exited _ -> ()

    member this.UpdateState() : unit =
        if this.State = Running && this.HasExitSignal then
            let code = this.ExitSignal.Task.Result

            if code <> 0 || this.Config.WaitForKeypress then
                this.State <- WaitingKeypress code
            else
                this.State <- Exited code

    member this.Destroy() : unit =
        this.SendInterrupt()
        this.ReadCancellation.Cancel()
        
        if not(this.HasExitSignal) then
            if this.Pty.WaitForExit(50) then
                this.ExitSignal.TrySetResult(this.Pty.ExitCode) |> ignore
            else
                this.Pty.Kill()
                
        this.ReadCancellation.Dispose()
        this.Pty.Dispose()
        this.Terminal.Dispose()

    member this.Resize(width: int, height: int) : unit =
        if this.Terminal.Cols <> width || this.Terminal.Rows <> height then
            this.Terminal.Resize(width, height)
            this.Pty.Resize(width, height)
