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
        | Some(Folder folder) -> state.ChangeDirectory(Path.Combine(state.Directory, folder), true)
        | Some(File _) -> state.StatusLine <- "Opening files not yet supported"
        | None -> ()

    [<Extension>]
    static member Close(state: State) : unit =
        match state.PopHistory() with
        | Some h ->
            state.ChangeDirectory(h.Directory, false)
            state.TrySelectByName(h.SelectedName)
        | None -> state.StatusLine <- "No further history to jump back to"

    [<Extension>]
    static member Go(state: State, path: string) : unit =
        let inline go_absolute (path: string) =
            if Directory.Exists(path) then
                state.ChangeDirectory(path, true)
            else
                state.StatusLine <- sprintf "No such directory '%s'" path

        if Path.IsPathRooted(path) then
            go_absolute(path)

        elif path.StartsWith('%') then
            let split = path.Substring(1).Split('/', StringSplitOptions.TrimEntries)

            match Enum.TryParse<Environment.SpecialFolder>(split.[0]) with
            | true, special_folder ->
                let mutable path = Environment.GetFolderPath(special_folder)

                for i = 1 to split.Length - 1 do
                    path <- Path.Combine(path, split.[i])

                go_absolute(path)
            | false, _ -> state.StatusLine <- sprintf "Unrecognised special folder '%s'" path

        else
            let split = path.Split('/', StringSplitOptions.TrimEntries)
            let mutable path = state.Directory

            for i = 0 to split.Length - 1 do
                path <- Path.Combine(path, split.[i])

            go_absolute(path)

    [<Extension>]
    static member Ascend(state: State) : unit =
        let new_dir = Path.GetDirectoryName(state.Directory)
        let old_folder = Path.GetFileName(state.Directory)

        if new_dir <> null then
            state.ChangeDirectory(new_dir, false)
            state.TrySelectByName(old_folder + "/")

    [<Extension>]
    static member Descend(state: State) : unit =
        match state.Selected with
        | Some(Folder folder) -> state.ChangeDirectory(Path.Combine(state.Directory, folder), true)
        | _ -> ()

    [<Extension>]
    static member Delete(state: State) : unit =
        let has_git_repo = false
        let delete_to_recycle_bin = OperatingSystem.IsWindows() && not has_git_repo

        let inline delete_file (path: string) : unit =
            if delete_to_recycle_bin then
                FileSystem.DeleteFile(
                    path,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin,
                    UICancelOption.ThrowException
                )
            else
                File.Delete(path)

        let inline delete_folder (path: string) : unit =
            if delete_to_recycle_bin then
                FileSystem.DeleteDirectory(
                    path,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin,
                    UICancelOption.ThrowException
                )
            else
                Directory.Delete(path, true)

        match state.Selected with
        | Some(File file) ->
            let path = Path.Combine(state.Directory, file)

            try
                delete_file(path)
                state.Refresh()
                state.StatusLine <- sprintf "Deleted '%s'" file
            with err ->
                state.StatusLine <- err.Message

        | Some(Folder folder) ->
            let path = Path.Combine(state.Directory, folder)

            try
                delete_folder(path)
                state.Refresh()
                state.StatusLine <- sprintf "Deleted '%s'" folder
            with err ->
                state.StatusLine <- err.Message

        | None -> ()

    [<Extension>]
    static member Rename(state: State, new_name: string) : unit =
        match state.Selected with
        | Some(File file) ->
            let path = Path.Combine(state.Directory, file)

            try
                FileSystem.RenameFile(path, new_name)
                state.Refresh()
                state.TrySelectByName(new_name)
                state.StatusLine <- sprintf "Renamed '%s' -> '%s'" file new_name
            with err ->
                state.StatusLine <- err.Message

        | Some(Folder folder) ->
            let path = Path.Combine(state.Directory, folder)

            try
                FileSystem.RenameDirectory(path, new_name)
                state.Refresh()
                state.TrySelectByName(new_name + "/")
                state.StatusLine <- sprintf "Renamed '%s' -> '%s'" folder new_name
            with err ->
                state.StatusLine <- err.Message

        | None -> ()

    [<Extension>]
    static member Copy(state: State, new_name: string) : unit =
        match state.Selected with
        | Some(File file) ->
            let path = Path.Combine(state.Directory, file)

            try
                FileSystem.CopyFile(path, new_name)
                state.Refresh()
                state.TrySelectByName(new_name)
                state.StatusLine <- sprintf "Copied '%s' -> '%s'" file new_name
            with err ->
                state.StatusLine <- err.Message

        | Some(Folder folder) ->
            let path = Path.Combine(state.Directory, folder)

            try
                FileSystem.CopyDirectory(path, new_name)
                state.Refresh()
                state.TrySelectByName(new_name + "/")
                state.StatusLine <- sprintf "Copied '%s' -> '%s'" folder new_name
            with err ->
                state.StatusLine <- err.Message

        | None -> ()

    [<Extension>]
    static member Add(state: State, name: string) : unit =
        try
            if name.EndsWith('/') then
                FileSystem.CreateDirectory(Path.Combine(state.Directory, name.TrimEnd('/')))
            else
                File.Create(Path.Combine(state.Directory, name)).Dispose()

            state.Refresh()
            state.TrySelectByName(name)
            state.StatusLine <- sprintf "Created '%s'" name
        with err ->
            state.StatusLine <- err.Message

    [<Extension>]
    static member Search(state: State) : unit =
        state.SearchBufferFocused <- not state.SearchBufferFocused

    [<Extension>]
    static member DispatchCommand(state: State, command: string) : unit =
        if command.StartsWith('!') then
            state.DispatchShell(command.Substring(1))
        else

        let split = command.Split(" ", 2, StringSplitOptions.TrimEntries)
        let args = if split.Length < 2 then "" else split.[1]

        match split.[0] with
        | "q"
        | "q!"
        | "exit" -> state.Exit()
        | "echo" -> state.Echo(args)
        | "bind" -> state.Alias(args)
        | "bind_c" -> state.BindCommand(args)
        | "bind_s" -> state.BindShellCommand(args)
        | "refresh" -> state.Refresh()
        | "go" -> state.Go(args)
        | "up" -> state.NavigateUp()
        | "down" -> state.NavigateDown()
        | "open" -> state.Open()
        | "close" -> state.Close()
        | "ascend" -> state.Ascend()
        | "descend" -> state.Descend()
        | "delete" -> state.Delete()
        | "rename" -> state.Rename(args)
        | "copy" -> state.Copy(args)
        | "add" -> state.Add(args)
        | "search" -> state.Search()
        | _ -> state.StatusLine <- sprintf "Unrecognised command '%s'" split.[0]

    [<Extension>]
    static member DispatchMessage(state: State, text: string) : unit =
        if text.StartsWith(':') then
            state.DispatchCommand(text.Substring(1))
        else
            state.StatusLine <- sprintf "Unrecognised input: %s" text
