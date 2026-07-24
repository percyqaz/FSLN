namespace FSLN

open System
open System.Diagnostics

module Commands =

    let inline apply_substitutions (state: State, command: string) : string =
        command
            .Replace("$$", '\uFFFD'.ToString())
            .Replace("$SOLUTION", state.Solution.FullPath)
            .Replace(
                "$PROJECT",
                match state.Selected.ParentProject with
                | Some project -> project.FullPath
                | None -> ""
            )
            .Replace("$", state.Selected.FullPath)
            .Replace('\uFFFD', '$')

    let dispatch_shell_command (state: State, command: string) : unit =

        let shell, args =

            if OperatingSystem.IsWindows() then
                "cmd.exe", "/c " + apply_substitutions(state, command)

            else
                "/bin/sh", "-c \"" + apply_substitutions(state, command) + "\""

        let start_info = ProcessStartInfo(shell, args)
        Console.Write("\u001b[?1049l\u001b[47h\u001b[2J\u001b[H")
        let proc = Process.Start(start_info)
        proc.WaitForExit()

        if proc.ExitCode <> 0 then
            Console.ReadKey(true) |> ignore
            state.StatusLine <- sprintf "(%i)" proc.ExitCode
        elif Console.GetCursorPosition() <> struct (0, 0) then
            Console.WriteLine("Press any key to return".ForeColor(0x666666))
            Console.ReadKey(true) |> ignore

        Console.Write("\u001b[47l\u001b[?1049h")

    let dispatch_internal_command (state: State, command: string) : unit =
        if command.StartsWith('!') then
            dispatch_shell_command(state, command.Substring(1))
        else

        let split = command.Split(" ", 2, StringSplitOptions.TrimEntries)
        let args = apply_substitutions(state, if split.Length < 2 then "" else split.[1])

        match split.[0] with
        | "q"
        | "q!" -> state.Quit()
        | "up" -> state.NavigateUp()
        | "down" -> state.NavigateDown()
        | "expand" -> state.ExpandSelection()
        | "collapse" -> state.CollapseSelection()
        | "move_up" -> state.MoveSelectionUp()
        | "move_down" -> state.MoveSelectionDown()
        | "refresh_git" -> state.RefreshGit()
        | "delete" -> () // todo: implement
        | "add" when args <> "" -> state.AddFile(args)
        | "move" when args <> "" -> state.RenameSelection(args)
        | "set" when args <> "" -> state.SetConfig(args)
        | "bind" when args <> "" -> state.SetBinding(args)
        | "echo" -> state.Echo(args)
        | _ -> ()

    let register_default_binds (state: State) : unit =
        state.CommandBuffer.Bind("<Esc>", ":q<Enter>")
        state.CommandBuffer.Bind("h", ":collapse<Enter>")
        state.CommandBuffer.Bind("j", ":down<Enter>")
        state.CommandBuffer.Bind("k", ":up<Enter>")
        state.CommandBuffer.Bind("l", ":expand<Enter>")
        // todo: [ ] to jump next/previous sibling

        state.CommandBuffer.Bind(".", ":!echo $<Enter>")

        state.CommandBuffer.Bind(
            "<Enter>",
            ":!C:/Program^ Files/JetBrains/JetBrains^ Rider^ 2026.1/bin/rider64.exe nosplash $<Enter>"
        )

        state.CommandBuffer.Bind("<A-k>", ":move_up<Enter>")
        state.CommandBuffer.Bind("<A-j>", ":move_down<Enter>")

        state.CommandBuffer.Bind("<Left>", "h")
        state.CommandBuffer.Bind("<Down>", "j")
        state.CommandBuffer.Bind("<Up>", "k")
        state.CommandBuffer.Bind("<Right>", "l")
        state.CommandBuffer.Bind("<A-Up>", "<A-k>")
        state.CommandBuffer.Bind("<A-Down>", "<A-j>")

        state.CommandBuffer.Bind("a", "lj")
