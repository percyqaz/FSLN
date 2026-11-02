open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Porta.Pty
open XTerm
open XTerm.Input
open XTerm.Options

let private findExecutable (name: string) =
    let pathEntries =
        Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)

    let candidates =
        if OperatingSystem.IsWindows() && Path.GetExtension(name) = "" then
            [ name + ".exe"; name + ".cmd"; name + ".bat"; name ]
        else
            [ name ]

    pathEntries
    |> Seq.collect(fun directory -> candidates |> Seq.map(fun candidate -> Path.Combine(directory, candidate)))
    |> Seq.tryFind File.Exists
    |> Option.defaultValue name

let private parseTarget (args: string array) =
    match args with
    | [| "nvim" |] -> findExecutable "nvim", [| "--clean" |]
    | [| "fsln" |] -> findExecutable "fsln", [||]
    | [| executable |] -> findExecutable executable, [||]
    | [| executable; remainingArgs |] -> findExecutable executable, [| remainingArgs |]
    | _ ->
        eprintfn "Usage: dotnet run -- [nvim|fsln|executable [arguments]]"
        Environment.Exit(2)
        "", [||]

let private modifiers (key: ConsoleKeyInfo) =
    let mutable result = KeyModifiers.None

    if key.Modifiers.HasFlag(ConsoleModifiers.Shift) then
        result <- result ||| KeyModifiers.Shift

    if key.Modifiers.HasFlag(ConsoleModifiers.Alt) then
        result <- result ||| KeyModifiers.Alt

    if key.Modifiers.HasFlag(ConsoleModifiers.Control) then
        result <- result ||| KeyModifiers.Control

    result

let private keyInput (terminal: Terminal) (key: ConsoleKeyInfo) =
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

    match special with
    | Some value -> terminal.GenerateKeyInput(value, modifiers key)
    | None when key.KeyChar <> '\000' -> terminal.GenerateCharInput(key.KeyChar, modifiers key)
    | None -> ""

let private colorSequence (mode: int) (color: int) (foreground: bool) =
    let code = if foreground then "38" else "48"

    match mode with
    // XTerm.NET 2.0.1 reports palette colors as mode 0, RGB as a packed integer in a nonzero mode,
    // and its default foreground/background as palette-mode sentinel values above 255.
    | 0 when color >= 256 -> if foreground then "39" else "49"
    | 0 -> $"{code};5;{color}"
    | _ ->
        let red = (color >>> 16) &&& 255
        let green = (color >>> 8) &&& 255
        let blue = color &&& 255
        $"{code};2;{red};{green};{blue}"

let private cellStyle (cell: XTerm.Buffer.BufferCell) =
    let attributes = cell.Attributes
    let codes = ResizeArray<string>()
    codes.Add("0")
    codes.Add(colorSequence (attributes.GetFgColorMode()) (attributes.GetFgColor()) true)
    codes.Add(colorSequence (attributes.GetBgColorMode()) (attributes.GetBgColor()) false)

    if attributes.IsBold() then
        codes.Add("1")

    if attributes.IsDim() then
        codes.Add("2")

    if attributes.IsItalic() then
        codes.Add("3")

    if attributes.IsUnderline() then
        codes.Add("4")

    if attributes.IsBlink() then
        codes.Add("5")

    if attributes.IsInverse() then
        codes.Add("7")

    if attributes.IsInvisible() then
        codes.Add("8")

    if attributes.IsStrikethrough() then
        codes.Add("9")

    if attributes.IsOverline() then
        codes.Add("53")

    String.Join(";", codes)

let private eraseRectangle (left: int) (top: int) (cols: int) (rows: int) =
    let builder = StringBuilder()
    builder.Append("\u001b[0m") |> ignore

    for row = 0 to rows - 1 do
        builder.Append($"\u001b[{top + row + 1};{left + 1}H") |> ignore
        builder.Append(' ', cols) |> ignore

    Console.Write(builder.ToString())

let private render (terminal: Terminal) (outerCols: int) (outerRows: int) =
    let builder = StringBuilder()
    let buffer = terminal.Buffer
    let left = max 0 ((outerCols - terminal.Cols) / 2)
    let top = max 0 ((outerRows - terminal.Rows) / 2)
    let mutable previousStyle = ""
    builder.Append("\u001b[0m\u001b[?25l") |> ignore

    for row = 0 to terminal.Rows - 1 do
        let lineIndex = buffer.YDisp + row
        let line = buffer.Lines.[lineIndex]
        builder.Append($"\u001b[{top + row + 1};{left + 1}H") |> ignore

        if isNull line then
            builder.Append(' ', terminal.Cols) |> ignore
        else
            for column = 0 to terminal.Cols - 1 do
                let cell = line.[column]

                if cell.Width = 0 then
                    ()
                else
                    let style = cellStyle cell

                    if style <> previousStyle then
                        builder.Append("\u001b[").Append(style).Append('m') |> ignore
                        previousStyle <- style

                    if String.IsNullOrEmpty(cell.Content) then
                        builder.Append(' ') |> ignore
                    else
                        builder.Append(cell.Content) |> ignore

    builder.Append("\u001b[0m") |> ignore

    if terminal.CursorVisible then
        builder.Append($"\u001b[{top + buffer.Y + 1};{left + buffer.X + 1}H\u001b[?25h") |> ignore

    Console.Write(builder.ToString())

let private run (args: string array) =
    task {
        let executable, commandLine = parseTarget args
        let outerCols = max 2 Console.WindowWidth
        let outerRows = max 2 Console.WindowHeight
        let cols = max 1 (outerCols / 2)
        let rows = max 1 (outerRows / 2)

        let terminalOptions =
            TerminalOptions(Cols = cols, Rows = rows, Scrollback = 2000, TermName = "xterm-256color")

        use terminal = new Terminal(terminalOptions)

        let processOptions =
            PtyOptions(
                App = executable,
                CommandLine = commandLine,
                Cwd = Environment.CurrentDirectory,
                Cols = cols,
                Rows = rows,
                Environment = Dictionary<string, string>(StringComparer.Ordinal)
            )

        processOptions.Environment["TERM"] <- "xterm-256color"
        processOptions.Environment["COLORTERM"] <- "truecolor"

        use! pty = PtyProvider.SpawnAsync(processOptions, CancellationToken.None)

        let exitSignal =
            TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)

        let mutable lastOuterCols = outerCols
        let mutable lastOuterRows = outerRows
        let mutable paneCols = cols
        let mutable paneRows = rows

        pty.ProcessExited.AddHandler(
            EventHandler<PtyExitedEventArgs>(fun _ eventArgs -> exitSignal.TrySetResult(eventArgs.ExitCode) |> ignore)
        )

        terminal.DataReceived.AddHandler(
            EventHandler<XTerm.Events.TerminalEvents.DataEventArgs>(fun _ eventArgs ->
                let bytes = Encoding.UTF8.GetBytes(eventArgs.Data)
                pty.WriterStream.Write(bytes, 0, bytes.Length)
                pty.WriterStream.Flush()
            )
        )

        Console.Write("\u001b[?1049h\u001b[2J\u001b[H\u001b[?25l")
        let oldCursorVisible = Console.CursorVisible
        Console.CursorVisible <- false

        use readCancellation = new CancellationTokenSource()

        let outputLoop =
            task {
                let bytes = Array.zeroCreate<byte> 8192
                let mutable reading = true

                while reading do
                    let! count = pty.ReaderStream.ReadAsync(bytes.AsMemory(), readCancellation.Token)

                    if count = 0 then
                        reading <- false
                    else
                        lock
                            terminal
                            (fun () ->
                                terminal.Write(ReadOnlySpan<byte>(bytes, 0, count))
                                render terminal lastOuterCols lastOuterRows
                            )
            }

        let inputWork =
            Func<Task>(fun () ->
                task {
                    while not exitSignal.Task.IsCompleted do
                        try
                            if Console.KeyAvailable then
                                let key = Console.ReadKey(true)
                                let input = lock terminal (fun () -> keyInput terminal key)

                                if input <> "" then
                                    let bytes = Encoding.UTF8.GetBytes(input)
                                    do! pty.WriterStream.WriteAsync(bytes, 0, bytes.Length)
                                    do! pty.WriterStream.FlushAsync()

                            let currentOuterCols = max 2 Console.WindowWidth
                            let currentOuterRows = max 2 Console.WindowHeight

                            if currentOuterCols <> lastOuterCols || currentOuterRows <> lastOuterRows then
                                lock
                                    terminal
                                    (fun () ->
                                        let oldLeft = max 0 ((lastOuterCols - terminal.Cols) / 2)
                                        let oldTop = max 0 ((lastOuterRows - terminal.Rows) / 2)
                                        eraseRectangle oldLeft oldTop terminal.Cols terminal.Rows
                                        lastOuterCols <- currentOuterCols
                                        lastOuterRows <- currentOuterRows
                                        paneCols <- max 1 (lastOuterCols / 2)
                                        paneRows <- max 1 (lastOuterRows / 2)
                                        terminal.Resize(paneCols, paneRows)
                                        pty.Resize(paneCols, paneRows)
                                        render terminal lastOuterCols lastOuterRows
                                    )

                            do! Task.Delay(8)
                        with :? InvalidOperationException ->
                            do! Task.Delay(10)
                }
                :> Task
            )

        let inputLoop = Task.Run(inputWork)

        let! exitCode = exitSignal.Task

        try
            pty.WaitForExit(2000) |> ignore
        with _ ->
            ()

        readCancellation.Cancel()

        try
            do! outputLoop
        with :? OperationCanceledException ->
            ()

        do! inputLoop

        Console.Write("\u001b[0m\u001b[?25h\u001b[?1049l")
        Console.CursorVisible <- oldCursorVisible
        printfn "Hosted process exited with code %d." exitCode
        Environment.ExitCode <- exitCode
    }

[<EntryPoint>]
let main args =
    try
        run args |> fun work -> work.GetAwaiter().GetResult()
    with error ->
        Console.Write("\u001b[0m\u001b[?25h\u001b[?1049l")
        eprintfn "%s" error.Message
        Environment.ExitCode <- 1

    Environment.ExitCode
