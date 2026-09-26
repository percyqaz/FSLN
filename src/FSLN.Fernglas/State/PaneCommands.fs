namespace Fernglas

open System
open System.IO
open System.Runtime.CompilerServices
open Microsoft.VisualBasic.FileIO

type PaneCommands =

    [<Extension>]
    static member NavigateUp(pane: Pane) : unit =
        let item_count = pane.FilteredEntries.Length

        match pane.Selection with
        | -1 -> pane.Selection <- if item_count > 0 then item_count - 1 else -1
        | index -> pane.Selection <- if index = 0 then item_count - 1 else index - 1

    [<Extension>]
    static member NavigateDown(pane: Pane) : unit =
        let item_count = pane.FilteredEntries.Length

        match pane.Selection with
        | -1 -> pane.Selection <- if item_count > 0 then 0 else -1
        | index -> pane.Selection <- if index + 1 >= item_count then 0 else index + 1

    [<Extension>]
    static member Open(pane: Pane) : string option =
        match pane.Selected with
        | Some(Folder folder) ->
            pane.ChangeDirectory(Path.Combine(pane.Directory, folder), true)
            None
        | Some(File _) -> Some("Opening files not yet supported")
        | None -> None

    [<Extension>]
    static member Back(pane: Pane) : bool =
        match pane.PopHistory() with
        | Some h ->
            pane.ChangeDirectory(h.Directory, false)
            pane.TrySelectByName(h.SelectedName)
            true
        | None -> false

    [<Extension>]
    static member Go(pane: Pane, path: string) : string option =
        let inline go_absolute (path: string) =
            if Directory.Exists(path) then
                pane.ChangeDirectory(path, true)
                None
            else
                Some(sprintf "No such directory '%s'" path)

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
            | false, _ -> Some(sprintf "Unrecognised special folder '%s'" path)

        else
            let split = path.Split('/', StringSplitOptions.TrimEntries)
            let mutable path = pane.Directory

            for i = 0 to split.Length - 1 do
                path <- Path.Combine(path, split.[i])

            go_absolute(path)

    [<Extension>]
    static member Ascend(pane: Pane) : unit =
        let new_dir = Path.GetDirectoryName(pane.Directory)
        let old_folder = Path.GetFileName(pane.Directory)

        if new_dir <> null then
            pane.ChangeDirectory(new_dir, false)
            pane.TrySelectByName(old_folder + "/")

    [<Extension>]
    static member Descend(pane: Pane) : unit =
        match pane.Selected with
        | Some(Folder folder) -> pane.ChangeDirectory(Path.Combine(pane.Directory, folder), true)
        | _ -> ()

    [<Extension>]
    static member Delete(pane: Pane) : string option =
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

        match pane.Selected with
        | Some(File file) ->
            let path = Path.Combine(pane.Directory, file)

            try
                delete_file(path)
                pane.NavigateUp()
                pane.Refresh()
                Some(sprintf "Deleted '%s'" file)
            with err ->
                Some(err.Message)

        | Some(Folder folder) ->
            let path = Path.Combine(pane.Directory, folder)

            try
                delete_folder(path)
                pane.NavigateUp()
                pane.Refresh()
                Some(sprintf "Deleted '%s'" folder)
            with err ->
                Some(err.Message)

        | None -> None

    [<Extension>]
    static member Rename(pane: Pane, new_name: string) : string option =
        match pane.Selected with
        | Some(File file) ->
            let path = Path.Combine(pane.Directory, file)

            try
                FileSystem.RenameFile(path, new_name)
                pane.Refresh()
                pane.TrySelectByName(new_name)
                Some(sprintf "Renamed '%s' -> '%s'" file new_name)
            with err ->
                Some(err.Message)

        | Some(Folder folder) ->
            let path = Path.Combine(pane.Directory, folder)

            try
                FileSystem.RenameDirectory(path, new_name)
                pane.Refresh()
                pane.TrySelectByName(new_name + "/")
                Some(sprintf "Renamed '%s' -> '%s'" folder new_name)
            with err ->
                Some(err.Message)

        | None -> None

    [<Extension>]
    static member Copy(pane: Pane, new_name: string) : string option =
        match pane.Selected with
        | Some(File file) ->
            let path = Path.Combine(pane.Directory, file)

            try
                FileSystem.CopyFile(path, new_name)
                pane.Refresh()
                pane.TrySelectByName(new_name)
                Some(sprintf "Copied '%s' -> '%s'" file new_name)
            with err ->
                Some(err.Message)

        | Some(Folder folder) ->
            let path = Path.Combine(pane.Directory, folder)

            try
                FileSystem.CopyDirectory(path, new_name)
                pane.Refresh()
                pane.TrySelectByName(new_name + "/")
                Some(sprintf "Copied '%s' -> '%s'" folder new_name)
            with err ->
                Some(err.Message)

        | None -> None

    [<Extension>]
    static member Add(pane: Pane, name: string) : string option =
        try
            if name.EndsWith('/') then
                FileSystem.CreateDirectory(Path.Combine(pane.Directory, name.TrimEnd('/')))
            else
                File.Create(Path.Combine(pane.Directory, name)).Dispose()

            pane.Refresh()
            pane.TrySelectByName(name)
            Some(sprintf "Created '%s'" name)
        with err ->
            Some(err.Message)
