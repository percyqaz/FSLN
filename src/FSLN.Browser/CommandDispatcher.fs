namespace FSLN

open System
open System.ComponentModel
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Threading

module private ConsoleInputMode =
    [<Literal>]
    let STD_INPUT_HANDLE = -10

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern nativeint GetStdHandle(int nStdHandle)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool GetConsoleMode(nativeint hConsoleHandle, uint32& lpMode)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool SetConsoleMode(nativeint hConsoleHandle, uint32 dwMode)

    let Capture () : (nativeint * uint32) option =
        if not(OperatingSystem.IsWindows()) then
            None
        else
            let handle = GetStdHandle(STD_INPUT_HANDLE)

            if handle = IntPtr.Zero || handle = IntPtr(-1) then
                raise(Win32Exception(Marshal.GetLastWin32Error()))

            let mutable mode = 0u

            if not(GetConsoleMode(handle, &mode)) then
                raise(Win32Exception(Marshal.GetLastWin32Error()))

            Some(handle, mode)

    let Restore (snapshot: (nativeint * uint32) option) : unit =
        match snapshot with
        | Some(handle, mode) when not(SetConsoleMode(handle, mode)) ->
            raise(Win32Exception(Marshal.GetLastWin32Error()))
        | _ -> ()

type CommandDispatcher(state: State, input_thread: InputThread) =

    member private this.ApplySubstitutions(command: string) : string =
        command
            .Replace("$$", '\uFFFD'.ToString())
            .Replace("$SOLUTION", state.Mode.Solution.FullPath)
            .Replace(
                "$GITPATH",
                match state.GitStatus with
                | Some gs -> Path.GetRelativePath(gs.RootPath, state.Mode.Selection.FullPath)
                | None -> ""
            )
            .Replace(
                "$PROJECT",
                match state.Mode.Selection.ParentProject with
                | Some project -> project.FullPath
                | None -> ""
            )
            .Replace("$", state.Mode.Selection.FullPath)
            .Replace('\uFFFD', '$')

    member private this.DispatchShell(command: string) : unit =

        let shell, args =
            if OperatingSystem.IsWindows() then
                "cmd.exe", "/c " + this.ApplySubstitutions(command)
            else
                "/bin/sh", "-c \"" + this.ApplySubstitutions(command) + "\""

        let start_info = ProcessStartInfo(shell, args)
        let console_mode = ConsoleInputMode.Capture()

        Console.Write(
            AnsiCodes.LEAVE_SECOND_SCREEN + AnsiCodes.SAVE_SCREEN + AnsiCodes.CLEAR_SCREEN + AnsiCodes.CURSOR_TO_ORIGIN
        )

        let proc = Process.Start(start_info)

        let ctrl_c_handler =
            Console.CancelKeyPress.Subscribe(fun ev ->
                if not(command.StartsWith("vim")) then
                    ev.Cancel <- true
                    proc.Kill()
                    Console.WriteLine("Process force-killed!".ForeColor(0xFF8888))
            )

        try
            proc.WaitForExit()
        finally
            ctrl_c_handler.Dispose()
            ConsoleInputMode.Restore(console_mode)

        if proc.ExitCode <> 0 then
            match input_thread.TryReadKey(Timeout.Infinite) with
            | _ -> ()

            state.StatusLine <- sprintf "(%i)" proc.ExitCode

        elif Console.GetCursorPosition() <> struct (0, 0) then
            Console.WriteLine("Press any key to return".ForeColor(0x666666))

            match input_thread.TryReadKey(Timeout.Infinite) with
            | _ -> ()

        Console.Write(AnsiCodes.RESTORE_SCREEN + AnsiCodes.ENTER_SECOND_SCREEN)

    member private this.OpenEditor(editor: string) : unit =
        if editor.Contains(" $") then this.DispatchShell(editor) else this.DispatchShell(editor + " $")

    member this.DispatchCommand(command: string) : unit =
        if command.StartsWith('!') then
            this.DispatchShell(command.Substring(1))
        else

        let split = command.Split(" ", 2, StringSplitOptions.TrimEntries)
        let args = if split.Length < 2 then "" else split.[1]

        match split.[0] with
        | "q"
        | "q!" -> state.Quit()
        | "up" -> state.NavigateUp()
        | "down" -> state.NavigateDown()
        | "expand_all" -> state.ExpandAll()
        | "expand" -> state.ExpandSelection()
        | "collapse" -> state.CollapseSelection()
        | "move_up" -> state.MoveSelectionUp()
        | "move_down" -> state.MoveSelectionDown()
        | "search" -> state.Search()
        | "git" -> state.ToggleGitMode()
        | "reload" -> state.Reload()
        | "reload_git" -> state.ReloadGit()
        | "reload_auto" -> state.AutoReload()
        | "add" when args <> "" -> state.AddFile(args)
        | "move" when args <> "" -> state.RenameSelection(args)
        | "set" when args <> "" -> state.SetConfig(args)
        | "bind" when args <> "" -> state.SetBinding(args)
        | "bind_c" when args <> "" -> state.SetCommandBinding(args)
        | "bind_s" when args <> "" -> state.SetShellBinding(args)
        | "editor" when args <> "" -> state.SetEditor(args)
        | "echo" -> state.Echo(this.ApplySubstitutions(args))
        | "edit" -> state.Edit(this.OpenEditor)
        | _ -> ()

    member this.DispatchCommandsOnState() : unit =
        state.CommandBuffer.DispatchCommands(this.DispatchCommand)

    member this.DispatchInitialCommandsOnState(config: string seq) : unit =
        state.CommandBuffer.DispatchInitialCommands(config, this.DispatchCommand)
