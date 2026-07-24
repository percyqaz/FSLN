namespace FSLN

open System
open System.Diagnostics

module Commands =

    let inline apply_substitutions (state: InteractiveState, command: string) : string =
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

    let dispatch_shell_command (state: InteractiveState, command: string) : unit =

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

    let dispatch_internal_command (state: InteractiveState, command: string) : unit =
        if command.StartsWith('!') then
            dispatch_shell_command(state, command.Substring(1))
        else

        let split = command.Split(" ", 2, StringSplitOptions.TrimEntries)
        let args = apply_substitutions(state, if split.Length < 2 then "" else split.[1])

        match split.[0] with
        | "q"
        | "q!" -> state.Running <- false
        | "up" -> state.Selected <- InteractiveState.navigate_up(state)
        | "down" -> state.Selected <- InteractiveState.navigate_down(state)
        | "expand" -> InteractiveState.expand_selected(state)
        | "collapse" -> InteractiveState.collapse_selected(state)
        | "move_up" -> InteractiveState.move_selection_up(state)
        | "move_down" -> InteractiveState.move_selection_down(state)
        | "echo" -> state.StatusLine <- args
        | "refresh_git" -> state.GitStatus <- GitStatus.Fetch()
        | "delete" -> () // todo: implement
        | "add" when args <> "" ->
            match state.Selected.ParentProject, state.Selected.ToParent() with
            | Some project, Some parent ->
                match project.TryAdd(parent, args) with
                | Ok() -> state.StatusLine <- "Created file!"
                | Error reason -> state.StatusLine <- reason
            | _ -> ()
        | "move" when args <> "" ->
            match state.Selected with
            | Selection.File file ->
                match file.ParentProject.TryMove(file, args) with
                | Ok() -> state.StatusLine <- "Moved file!"
                | Error reason -> state.StatusLine <- reason
            | _ -> ()
        | "set" ->
            let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
            let key, value = split.[0], if split.Length > 1 then split.[1] else ""

            match state.Theme.Set(key, value) with
            | Ok new_theme ->
                state.Theme <- new_theme
                state.StatusLine <- ""
            | Error reason -> state.StatusLine <- reason
        | "bind" ->
            let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
            let source, target = split.[0], if split.Length > 1 then split.[1] else ""

            if source.Length > 0 && target.Length > 0 && source <> target then
                state.CommandBuffer.Bind(source, target)
                state.StatusLine <- "Binding set."
            else
                state.StatusLine <- "Invalid binding."

        | _ -> ()

    let register_default_binds (state: InteractiveState) : unit =
        state.CommandBuffer.Bind("<Esc>", ":q<Enter>")
        state.CommandBuffer.Bind("h", ":collapse<Enter>")
        state.CommandBuffer.Bind("j", ":down<Enter>")
        state.CommandBuffer.Bind("k", ":up<Enter>")
        state.CommandBuffer.Bind("l", ":expand<Enter>")

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
// todo: [ ] to jump next/previous sibling
