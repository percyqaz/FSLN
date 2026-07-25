namespace FSLN

open System
open System.Diagnostics
open System.Threading

type CommandDispatcher(state: State, input_thread: InputThread) =

    member private this.ApplySubstitutions(command: string) : string =
        match state.Mode with
        | Mode.Normal nm ->
            command
                .Replace("$$", '\uFFFD'.ToString())
                .Replace("$SOLUTION", nm.Solution.FullPath)
                .Replace(
                    "$PROJECT",
                    match nm.Selected.ParentProject with
                    | Some project -> project.FullPath
                    | None -> ""
                )
                .Replace("$", nm.Selected.FullPath)
                .Replace('\uFFFD', '$')
        | _ ->
            command
                .Replace("$$", '\uFFFD'.ToString())
                // todo: replacements in other modes
                .Replace('\uFFFD', '$')
            

    member private this.DispatchShell(state: State, command: string) : unit =

        let shell, args =

            if OperatingSystem.IsWindows() then
                "cmd.exe", "/c " + this.ApplySubstitutions(command)

            else
                "/bin/sh", "-c \"" + this.ApplySubstitutions(command) + "\""

        let start_info = ProcessStartInfo(shell, args)
        Console.Write(AnsiCodes.LEAVE_SECOND_SCREEN + AnsiCodes.SAVE_SCREEN + AnsiCodes.CLEAR_SCREEN + AnsiCodes.CURSOR_TO_ORIGIN)
        let proc = Process.Start(start_info)
        proc.WaitForExit()

        if proc.ExitCode <> 0 then
            match input_thread.TryReadKey(Timeout.Infinite) with
            | _ -> ()

            state.StatusLine <- sprintf "(%i)" proc.ExitCode

        elif Console.GetCursorPosition() <> struct (0, 0) then
            Console.WriteLine("Press any key to return".ForeColor(0x666666))

            match input_thread.TryReadKey(Timeout.Infinite) with
            | _ -> ()

        Console.Write(AnsiCodes.RESTORE_SCREEN + AnsiCodes.ENTER_SECOND_SCREEN)

    member this.DispatchCommand(command: string) : unit =
        if command.StartsWith('!') then
            this.DispatchShell(state, command.Substring(1))
        else

        let split = command.Split(" ", 2, StringSplitOptions.TrimEntries)
        let args = this.ApplySubstitutions(if split.Length < 2 then "" else split.[1])

        match split.[0] with
        | "q"
        | "q!" -> state.Quit()
        | "up" -> state.NavigateUp()
        | "down" -> state.NavigateDown()
        | "expand" -> state.ExpandSelection()
        | "collapse" -> state.CollapseSelection()
        | "move_up" -> state.MoveSelectionUp()
        | "move_down" -> state.MoveSelectionDown()
        | "search" -> state.Search()
        | "reload" -> state.Reload()
        | "reload_git" -> state.ReloadGit()
        | "reload_auto" -> state.AutoReload()
        | "add" when args <> "" -> state.AddFile(args)
        | "move" when args <> "" -> state.RenameSelection(args)
        | "set" when args <> "" -> state.SetConfig(args)
        | "bind" when args <> "" -> state.SetBinding(args)
        | "echo" -> state.Echo(args)
        | _ -> ()

    member this.DispatchCommandsOnState() : unit =
        state.Buffers.CommandBuffer.DispatchCommands(this.DispatchCommand)

    member this.DispatchInitialCommandsOnState(config: string seq) : unit =
        state.Buffers.CommandBuffer.DispatchInitialCommands(config, this.DispatchCommand)
