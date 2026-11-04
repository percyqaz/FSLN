namespace FSLN.Solution

open System
open System.IO
open System.Runtime.CompilerServices
open FSLN.Shared

type StateCommands =

    [<Extension>]
    static member SetConfig(state: State, args: string) : unit =
        let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
        let key, value = split.[0], if split.Length > 1 then split.[1] else ""

        match state.Theme.Set(key, value) with
        | Ok new_theme ->
            state.Theme <- new_theme
            state.StatusLine <- ""
        | Error reason -> state.StatusLine <- reason

    [<Extension>]
    static member SetEditor(state: State, args: string) : unit =
        let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
        let filetype, value = split.[0], if split.Length > 1 then split.[1] else ""

        if filetype.Length > 0 && value.Length > 0 then
            match state.Editors.Set(filetype, value) with
            | Ok() -> state.StatusLine <- ""
            | Error reason -> state.StatusLine <- reason

    [<Extension>]
    static member SetBinding(state: State, args: string) : unit =
        let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
        let source, target = split.[0], if split.Length > 1 then split.[1] else ""

        if source.Length > 0 && target.Length > 0 && source <> target then
            state.Keymap.Alias(source, target)
            state.StatusLine <- "Binding set."
        else
            state.StatusLine <- "Invalid binding."

    [<Extension>]
    static member SetCommandBinding(state: State, args: string) : unit =
        let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
        let source, target = split.[0], if split.Length > 1 then split.[1] else ""

        if source.Length > 0 && target.Length > 0 && source <> target then
            state.Keymap.Alias(source, sprintf ":%s<Enter>" target)
            state.StatusLine <- "Binding set."
        else
            state.StatusLine <- "Invalid binding."

    [<Extension>]
    static member SetShellBinding(state: State, args: string) : unit =
        let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
        let source, target = split.[0], if split.Length > 1 then split.[1] else ""

        if source.Length > 0 && target.Length > 0 && source <> target then
            state.Keymap.Alias(source, sprintf ":!%s<Enter>" target)
            state.StatusLine <- "Binding set."
        else
            state.StatusLine <- "Invalid binding."

    [<Extension>]
    static member Edit(state: State) : unit =
        let filetype =
            match state.Mode.Selection with
            | Selection.Folder _ -> "/"
            | Selection.File file -> Path.GetExtension(file.FullPath).ToLower()
            | Selection.Project project -> Path.GetExtension(project.FullPath).ToLower()
            | Selection.Solution sln -> Path.GetExtension(sln.FullPath).ToLower()

        let editor = state.Editors.Get(filetype)
        if editor.Contains(" $") then state.DispatchShell(editor) else state.DispatchShell(editor + " $")

    [<Extension>]
    static member Echo(state: State, args: string) : unit = state.StatusLine <- args

    [<Extension>]
    static member Quit(state: State) : unit = state.Running <- false

    [<Extension>]
    static member Search(state: State) : unit =
        state.ActiveBuffer <- ActiveBuffer.Search

    [<Extension>]
    static member ToggleGitMode(state: State) : unit =
        state.Mode <- state.Mode.ToggleGitMode(state.GitStatus)

    [<Extension>]
    static member ReloadGit(state: State) : unit =
        state.GitStatus <- GitStatus.Fetch(state.Workspace.RootPath)
        state.Mode <- state.Mode.Update(state.SearchBuffer.ToString(), state.GitStatus)

    [<Extension>]
    static member Reload(state: State) : unit =
        state.Mode <- state.Mode.Reload(state.Workspace)

    [<Extension>]
    static member AutoReload(state: State) : unit =
        state.Mode <- state.Mode.AutoReload(state.Workspace)

    [<Extension>]
    static member NavigateUp(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.NavigateUp(state)
        | Mode.Search sm -> sm.NavigateUp(state)
        | Mode.Git gm -> gm.NavigateUp(state)

    [<Extension>]
    static member NavigateDown(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.NavigateDown(state)
        | Mode.Search sm -> sm.NavigateDown(state)
        | Mode.Git gm -> gm.NavigateDown(state)

    [<Extension>]
    static member NavigateOut(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.NavigateOut()
        | Mode.Search sm -> sm.NavigateOut()
        | Mode.Git gm -> gm.NavigateOut()

    [<Extension>]
    static member CollapseSelection(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.CollapseSelection(state)
        | Mode.Search sm -> sm.CollapseSelection(state)
        | Mode.Git gm -> gm.CollapseSelection(state)

    [<Extension>]
    static member ExpandAll(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.ExpandAll(state)
        | Mode.Search sm -> sm.ExpandAll(state)
        | Mode.Git gm -> gm.ExpandAll(state)

    [<Extension>]
    static member ExpandSelection(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.ExpandSelection(state)
        | Mode.Search sm -> sm.ExpandSelection(state)
        | Mode.Git gm -> gm.ExpandSelection(state)

    [<Extension>]
    static member MoveSelectionUp(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.MoveSelectionUp()
        | _ -> state.StatusLine <- "Tree cannot be reordered in this mode."

    [<Extension>]
    static member MoveSelectionDown(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.MoveSelectionDown()
        | _ -> state.StatusLine <- "Tree cannot be reordered in this mode."

    [<Extension>]
    static member AddFile(state: State, args: string) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.AddFile(state, args)
        | _ -> state.StatusLine <- "Files cannot be added in this mode."

    [<Extension>]
    static member RenameSelection(state: State, args: string) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.RenameSelection(state, args)
        | _ -> state.StatusLine <- "Files cannot be moved in this mode."

    [<Extension>]
    static member DispatchCommand(state: State, command: string) : unit =
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
        | "edit" -> state.Edit()
        | _ -> ()

    [<Extension>]
    static member DispatchMessage(state: State, message: string) : unit =
        if message.StartsWith(':') then
            state.DispatchCommand(message.Substring(1))
        else
            state.StatusLine <- sprintf "Unrecognised message: '%s'" message
