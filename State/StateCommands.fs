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
    static member AddFile(state: State, args: string) : unit =
        match state.Selected.ParentProject, state.Selected.ToParent() with
        | Some project, Some parent ->
            match project.TryAdd(parent, args) with
            | Ok() -> state.StatusLine <- "Created file!"
            | Error reason -> state.StatusLine <- reason
        | _ -> ()

    [<Extension>]
    static member RenameSelection(state: State, args: string) : unit =
        match state.Selected with
        | Selection.File file ->
            match file.ParentProject.TryMove(file, args) with
            | Ok() -> state.StatusLine <- "Moved file!"
            | Error reason -> state.StatusLine <- reason
        // todo: support moving folders
        | _ -> ()

    [<Extension>]
    static member SetBinding(state: State, args: string) : unit =
        let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
        let source, target = split.[0], if split.Length > 1 then split.[1] else ""

        if source.Length > 0 && target.Length > 0 && source <> target then
            state.Buffers.CommandBuffer.Bind(source, target)
            state.StatusLine <- "Binding set."
        else
            state.StatusLine <- "Invalid binding."

    [<Extension>]
    static member Echo(state: State, args: string) : unit = state.StatusLine <- args

    [<Extension>]
    static member RefreshGit(state: State) : unit = state.GitStatus <- GitStatus.Fetch()

    [<Extension>]
    static member Quit(state: State) : unit = state.Running <- false
            
    [<Extension>]
    static member Search(state: State) : unit = state.Buffers.StartSearch()

    [<Extension>]
    static member Reload(state: State) : unit =
        state.Solution <- SolutionLoader.read_solution_file(state.Solution.FullPath)
        state.Selected <- Selection.Solution(state.Solution)

    [<Extension>]
    static member AutoReload(state: State) : unit =
        if state.Solution.HasExternalChange() then
            state.Reload()

