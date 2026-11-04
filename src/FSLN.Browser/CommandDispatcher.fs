namespace FSLN

open System

type CommandDispatcher(state: State) =

    member private this.OpenEditor(editor: string) : unit =
        if editor.Contains(" $") then state.DispatchShell(editor) else state.DispatchShell(editor + " $")

    member this.DispatchCommand(command: string) : unit =
        if command.StartsWith('!') then
            state.DispatchShell(command.Substring(1))
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
        | "echo" -> state.Echo(state.ApplySubstitutions(args))
        | "edit" -> state.Edit(this.OpenEditor)
        | _ -> ()

    member this.DispatchMessage(message: string) : unit =
        if message.StartsWith(':') then
            this.DispatchCommand(message.Substring(1))
        else
            state.StatusLine <- sprintf "Unrecognised message: '%s'" message

    member this.DispatchCommandsOnState() : unit =
        state.CommandBuffer.Dispatch(this.DispatchMessage, state.Keymap)

    member this.DispatchInitialCommandsOnState(config: string seq) : unit =
        state.CommandBuffer.Append(config)
        this.DispatchCommandsOnState()
