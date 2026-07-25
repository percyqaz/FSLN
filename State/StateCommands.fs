namespace FSLN

open System
open System.Runtime.CompilerServices
open FSLN

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
    static member SetBinding(state: State, args: string) : unit =
        let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
        let source, target = split.[0], if split.Length > 1 then split.[1] else ""

        if source.Length > 0 && target.Length > 0 && source <> target then
            state.CommandBuffer.Bind(source, target)
            state.StatusLine <- "Binding set."
        else
            state.StatusLine <- "Invalid binding."

    [<Extension>]
    static member Echo(state: State, args: string) : unit = state.StatusLine <- args

    [<Extension>]
    static member Quit(state: State) : unit = state.Running <- false

    [<Extension>]
    static member Search(state: State) : unit =
        state.ActiveBuffer <- ActiveBuffer.Search

    [<Extension>]
    static member ReloadGit(state: State) : unit = state.GitStatus <- GitStatus.Fetch()

    [<Extension>]
    static member Reload(state: State) : unit =
        state.Mode <-
            match state.Mode with
            | Mode.Normal nm -> Mode.Normal(nm.Reload())
            | Mode.Search sm -> Mode.Search(sm.Reload())

    [<Extension>]
    static member AutoReload(state: State) : unit =
        state.Mode <-
            match state.Mode with
            | Mode.Normal nm -> Mode.Normal(nm.AutoReload())
            | Mode.Search sm -> Mode.Search(sm.AutoReload())

    [<Extension>]
    static member NavigateUp(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.NavigateUp(state)
        | Mode.Search sm -> sm.NavigateUp(state)

    [<Extension>]
    static member NavigateDown(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.NavigateDown(state)
        | Mode.Search sm -> sm.NavigateDown(state)

    [<Extension>]
    static member NavigateOut(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.NavigateOut()
        | Mode.Search sm -> sm.NavigateOut()

    [<Extension>]
    static member CollapseSelection(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.CollapseSelection(state)
        | Mode.Search sm -> sm.CollapseSelection(state)

    [<Extension>]
    static member ExpandSelection(state: State) : unit =
        match state.Mode with
        | Mode.Normal nm -> nm.ExpandSelection(state)
        | Mode.Search sm -> sm.ExpandSelection(state)

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
