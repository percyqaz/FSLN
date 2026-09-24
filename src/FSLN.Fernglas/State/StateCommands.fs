namespace Fernglas

open System
open System.IO
open System.Runtime.CompilerServices

type StateCommands =

    [<Extension>]
    static member Exit(state: State) : unit = state.Running <- false

    [<Extension>]
    static member NavigateUp(state: State) : unit =
        let item_count = state.FilteredEntries.Length

        match state.Selection with
        | -1 -> state.Selection <- if item_count > 0 then item_count - 1 else -1
        | index -> state.Selection <- if index = 0 then item_count - 1 else index - 1

    [<Extension>]
    static member NavigateDown(state: State) : unit =
        let item_count = state.FilteredEntries.Length

        match state.Selection with
        | -1 -> state.Selection <- if item_count > 0 then 0 else -1
        | index -> state.Selection <- if index + 1 >= item_count then 0 else index + 1

    [<Extension>]
    static member Open(state: State) : unit =
        match state.Selected with
        | Some(Folder f) ->
            state.Directory <- Path.Combine(state.Directory, f)
            state.Refresh()
        | Some(File _) -> state.StatusLine <- "Opening files not yet supported"
        | None -> ()

    [<Extension>]
    static member Ascend(state: State) : unit =
        let new_dir = Path.GetDirectoryName(state.Directory)

        if new_dir <> null then
            state.Directory <- new_dir
            state.Refresh()

    [<Extension>]
    static member Descend(state: State) : unit =
        match state.Selected with
        | Some(Folder f) ->
            state.Directory <- Path.Combine(state.Directory, f)
            state.Refresh()
        | _ -> ()

    [<Extension>]
    static member Search(state: State) : unit =
        state.SearchBufferFocused <- not state.SearchBufferFocused

    [<Extension>]
    static member DispatchCommand(state: State, command: string) : unit =
        let split = command.Split(" ", 2, StringSplitOptions.TrimEntries)
        let args = if split.Length < 2 then "" else split.[1]
        ignore(args)

        match split.[0] with
        | "q"
        | "q!"
        | "exit" -> state.Exit()
        | "up" -> state.NavigateUp()
        | "down" -> state.NavigateDown()
        | "open" -> state.Open()
        | "ascend" -> state.Ascend()
        | "descend" -> state.Descend()
        | "search" -> state.Search()
        | _ -> state.StatusLine <- sprintf "Unrecognised command '%s'" split.[0]

    [<Extension>]
    static member DispatchMessage(state: State, text: string) : unit =
        if text.StartsWith(':') then
            state.DispatchCommand(text.Substring(1))
        else
            state.StatusLine <- sprintf "Unrecognised input: %s" text
