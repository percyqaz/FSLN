namespace Fernglas

open System
open System.IO
open System.Runtime.CompilerServices
open Microsoft.VisualBasic.FileIO

type StateCommands =

    [<Extension>]
    static member Exit(state: State) : unit = state.Running <- false

    [<Extension>]
    static member Echo(state: State, args: string) : unit =
        state.StatusLine <- state.ApplySubstitutions(args)

    [<Extension>]
    static member Alias(state: State, args: string) : unit =
        let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
        let source, target = split.[0], if split.Length > 1 then split.[1] else ""

        if source.Length > 0 && target.Length > 0 && source <> target then
            state.Keymap.Alias(source, target)
            state.StatusLine <- "Binding set."
        else
            state.StatusLine <- "Invalid binding."

    [<Extension>]
    static member BindCommand(state: State, args: string) : unit =
        let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
        let source, target = split.[0], if split.Length > 1 then split.[1] else ""

        if source.Length > 0 && target.Length > 0 && source <> target then
            state.Keymap.AliasCommand(source, target)
            state.StatusLine <- "Binding set."
        else
            state.StatusLine <- "Invalid binding."

    [<Extension>]
    static member BindShellCommand(state: State, args: string) : unit =
        let split = args.Split("=", 2, StringSplitOptions.TrimEntries)
        let source, target = split.[0], if split.Length > 1 then split.[1] else ""

        if source.Length > 0 && target.Length > 0 && source <> target then
            state.Keymap.AliasCommand(source, "!" + target)
            state.StatusLine <- "Binding set."
        else
            state.StatusLine <- "Invalid binding."

    [<Extension>]
    static member Search(state: State) : unit =
        state.SearchBufferFocused <- not state.SearchBufferFocused

    [<Extension>]
    static member Left(state: State) : unit = state.SplitPaneFocused <- false

    [<Extension>]
    static member Right(state: State) : unit =
        if state.SplitPane.IsSome then
            state.SplitPaneFocused <- true

    [<Extension>]
    static member MoveLeft(state: State) : unit =
        match state.SplitPane with
        | Some pane when state.SplitPaneFocused ->
            try
                match pane.Selected with
                | Some(Folder folder) ->
                    FileSystem.MoveDirectory(
                        Path.Combine(pane.Directory, folder),
                        Path.Combine(state.MainPane.Directory, folder)
                    )

                    pane.NavigateUp()
                    state.Refresh()
                    state.MainPane.TrySelectByName(folder + "/")
                    state.StatusLine <- sprintf "Moved '%s'" folder
                | Some(File file) ->
                    FileSystem.MoveFile(
                        Path.Combine(pane.Directory, file),
                        Path.Combine(state.MainPane.Directory, file)
                    )

                    pane.NavigateUp()
                    state.Refresh()
                    state.MainPane.TrySelectByName(file)
                    state.StatusLine <- sprintf "Moved '%s'" file
                | None -> ()
            with err ->
                state.StatusLine <- err.Message
        | _ -> ()

    [<Extension>]
    static member MoveRight(state: State) : unit =
        match state.SplitPane with
        | None ->
            match state.MainPane.Selected with
            | Some(Folder f) ->
                state.SplitPane <- Some(Pane.Create(Path.Combine(state.MainPane.Directory, f), false))
                state.SplitPaneFocused <- true
            | _ -> ()
        | Some pane when not state.SplitPaneFocused ->
            try
                match state.MainPane.Selected with
                | Some(Folder folder) ->
                    FileSystem.MoveDirectory(
                        Path.Combine(state.MainPane.Directory, folder),
                        Path.Combine(pane.Directory, folder)
                    )

                    state.MainPane.NavigateUp()
                    state.Refresh()
                    pane.TrySelectByName(folder + "/")
                    state.StatusLine <- sprintf "Moved '%s'" folder
                | Some(File file) ->
                    FileSystem.MoveFile(
                        Path.Combine(state.MainPane.Directory, file),
                        Path.Combine(pane.Directory, file)
                    )

                    state.MainPane.NavigateUp()
                    state.Refresh()
                    pane.TrySelectByName(file)
                    state.StatusLine <- sprintf "Moved '%s'" file
                | None -> ()
            with err ->
                state.StatusLine <- err.Message
        | _ -> ()

    [<Extension>]
    static member DispatchCommand(state: State, command: string) : unit =
        if command.StartsWith('!') then
            state.DispatchShell(command.Substring(1))
        else

        let split = command.Split(" ", 2, StringSplitOptions.TrimEntries)
        let args = if split.Length < 2 then "" else split.[1]

        let inline pane_cmd (action: Pane -> string option) =
            match action state.ActivePane with
            | Some message -> state.StatusLine <- message
            | None -> ()

        match split.[0] with
        | "q"
        | "q!"
        | "exit" -> state.Exit()
        | "echo" -> state.Echo(args)
        | "bind" -> state.Alias(args)
        | "bind_c" -> state.BindCommand(args)
        | "bind_s" -> state.BindShellCommand(args)
        | "refresh" -> state.ActivePane.Refresh()
        | "up" -> state.ActivePane.NavigateUp()
        | "down" -> state.ActivePane.NavigateDown()
        | "left" -> state.Left()
        | "right" -> state.Right()
        | "move_left" -> state.MoveLeft()
        | "move_right" -> state.MoveRight()
        | "ascend" -> state.ActivePane.Ascend()
        | "descend" -> state.ActivePane.Descend()
        | "go" -> pane_cmd(_.Go(args))
        | "open" -> pane_cmd(_.Open())
        | "close" -> pane_cmd(_.Close())
        | "delete" -> pane_cmd(_.Delete())
        | "rename" -> pane_cmd(_.Rename(args))
        | "copy" -> pane_cmd(_.Copy(args))
        | "add" -> pane_cmd(_.Add(args))
        | "search" -> state.Search()
        | _ -> state.StatusLine <- sprintf "Unrecognised command '%s'" split.[0]

    [<Extension>]
    static member DispatchMessage(state: State, text: string) : unit =
        if text.StartsWith(':') then
            state.DispatchCommand(text.Substring(1))
        else
            state.StatusLine <- sprintf "Unrecognised input: %s" text
