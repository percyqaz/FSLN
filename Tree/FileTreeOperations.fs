namespace FSLN

open System
open System.IO
open System.Runtime.CompilerServices
open Microsoft.Build.Construction
open FSLN

type FileTreeFileOperations =

    static let validate_name (name: string) : bool =
        name.Trim().TrimEnd('.').Replace("..", "") = name
        && String.forall (fun c -> Char.IsAsciiLetterOrDigit(c) || c = '.' || c = '_' || c = ' ') name

    static let rec resolve_path (parent: Parent, parts: string list) : Result<Parent * string, string> =
        match parts with
        | [ name ] -> if validate_name(name) then Ok(parent, name) else Error "Invalid file name"
        | path_segment :: remaining ->
            if path_segment = ".." then
                match parent with
                | Parent.Folder folder -> resolve_path(folder.Parent, remaining)
                | Parent.Project _ -> Error "Path is outside project!"

            elif validate_name(path_segment) then
                match parent.TryFindFolder(path_segment) with
                | Some existing_folder -> resolve_path(Parent.Folder(existing_folder), remaining)
                | None ->
                    let new_path =
                        match parent with
                        | Parent.Folder folder -> folder.FullPath
                        | Parent.Project project -> Path.get_directory_name(project.FullPath)
                        + "/"
                        + path_segment

                    let new_folder: FileTreeFolder =
                        {
                            Name = path_segment
                            Parent = parent
                            FullPath = new_path
                            Children = ResizeArray()
                        }

                    resolve_path(Parent.Folder(new_folder), remaining)
            else
                Error "Invalid path segment"

        | [] -> Error "empty parts passed!"

    static let rec find_lowest_neighbor (parent: Parent) : ProjectItemElement =
        let children = parent.Children

        if children.Count = 0 then
            match parent with
            | Parent.Project _ -> failwith "impossible"
            | Parent.Folder folder -> find_lowest_neighbor(folder.Parent)
        else
            let last_child = children.[children.Count - 1]

            match last_child with
            | FileTreeEntry.File file -> file.ProjectItemElement
            | FileTreeEntry.Folder folder -> find_lowest_neighbor(Parent.Folder(folder))

    static let insert_after_neighbor
        (project: Project, relative_path: string, neighbor: ProjectItemElement)
        : ProjectItemElement =
        let added_item = project.ProjectRootElement.AddItem("Compile", relative_path)
        let parent = added_item.Parent
        parent.RemoveChild(added_item)
        parent.InsertAfterChild(added_item, neighbor)
        added_item

    static let rec connect_to_tree (parent: Parent, item: FileTreeEntry) : unit =
        let children = parent.Children
        let parent_needs_connecting = children.Count = 0
        children.Add(item)

        if parent_needs_connecting then
            match parent with
            | Parent.Project _ -> assert false
            | Parent.Folder folder -> connect_to_tree(folder.Parent, Folder folder)

    static let rec remove_from_tree (parent: Parent, item: FileTreeEntry) : unit =
        let children = parent.Children
        children.Remove(item) |> ignore
        let parent_needs_removing = children.Count = 0

        if parent_needs_removing then
            match parent with
            | Parent.Project _ -> assert false
            | Parent.Folder folder -> remove_from_tree(folder.Parent, Folder folder)

    [<Extension>]
    static member TryAdd(project: Project, parent: Parent, new_name_or_path: string) : Result<unit, string> =
        let directory_parts =
            new_name_or_path.Replace('\\', '/').Split('/', StringSplitOptions.None) |> List.ofArray

        match resolve_path(parent, directory_parts) with
        | Error reason -> Error reason
        | Ok(new_parent, file_name) ->
            let new_parent_full_path =
                match new_parent with
                | Parent.Folder folder -> folder.FullPath
                | Parent.Project project -> Path.get_directory_name(project.FullPath)

            let added_item_full_path = new_parent_full_path + "/" + file_name

            if new_parent.TryFindFile(file_name).IsSome || File.Exists(added_item_full_path) then
                Error "File already exists"
            else

            let added_item_relative_path =
                added_item_full_path
                    .Replace(Path.get_directory_name(project.FullPath) + Path.AltDirectorySeparatorChar.ToString(), "")
                    .Replace('/', '\\')

            let added_project_item =
                insert_after_neighbor(project, added_item_relative_path, find_lowest_neighbor(new_parent))

            let tree_file =
                File
                    {
                        Name = file_name
                        FullPath = added_item_full_path
                        ProjectItemElement = added_project_item
                        Parent = new_parent
                    }

            Directory.CreateDirectory(Path.GetDirectoryName(added_item_full_path)) |> ignore
            File.Create(added_item_full_path).Dispose()
            project.Save()
            connect_to_tree(new_parent, tree_file)
            Ok()

    [<Extension>]
    static member TryMove(project: Project, file: FileTreeFile, new_name_or_path: string) : Result<unit, string> =
        let directory_parts =
            new_name_or_path.Replace('\\', '/').Split('/', StringSplitOptions.None) |> List.ofArray

        match resolve_path(file.Parent, directory_parts) with
        | Error reason -> Error reason
        | Ok(new_parent, file_name) ->
            let new_parent_full_path =
                match new_parent with
                | Parent.Folder folder -> folder.FullPath
                | Parent.Project project -> Path.get_directory_name(project.FullPath)

            let moved_item_full_path = new_parent_full_path + "/" + file_name

            if new_parent.TryFindFile(file_name).IsSome || File.Exists(moved_item_full_path) then
                Error "File already exists"
            else

            let moved_item_relative_path =
                moved_item_full_path
                    .Replace(Path.get_directory_name(project.FullPath) + Path.AltDirectorySeparatorChar.ToString(), "")
                    .Replace('/', '\\')

            let insertion_neighbor =
                if new_parent = file.Parent then file.ProjectItemElement else find_lowest_neighbor(new_parent)

            let new_project_item =
                insert_after_neighbor(project, moved_item_relative_path, insertion_neighbor)

            file.ProjectItemElement.Parent.RemoveChild(file.ProjectItemElement)

            let new_tree_file =
                File
                    {
                        Name = file_name
                        FullPath = moved_item_full_path
                        ProjectItemElement = new_project_item
                        Parent = new_parent
                    }

            Directory.CreateDirectory(Path.GetDirectoryName(moved_item_full_path)) |> ignore
            File.Move(file.FullPath, moved_item_full_path)
            project.Save()
            connect_to_tree(new_parent, new_tree_file)
            remove_from_tree(file.Parent, File file)
            Ok()

type FileTreeReorderOperations =

    static let swap_files_in_project (above_files: ProjectItemElement seq, below_files: ProjectItemElement seq) : unit =
        let first_above_file = Seq.head above_files
        let parent = first_above_file.Parent

        for file in below_files do
            parent.RemoveChild(file)
            parent.InsertBeforeChild(file, first_above_file)

    static let merge_folders_if_needed
        (entry_one: FileTreeEntry, entry_two: FileTreeEntry, siblings: ResizeArray<FileTreeEntry>)
        : unit =
        match entry_one, entry_two with
        | Folder a, Folder b when a.FullPath = b.FullPath ->
            a.Children.AddRange(b.Children |> Seq.map _.WithParent(Parent.Folder(a)))
            siblings.Remove(entry_two) |> ignore
        | _ -> ()

    [<Extension>]
    static member MoveUp(project: Project, file: FileTreeFile) : unit =
        let siblings = file.Parent.Children
        let folder_pos = siblings.IndexOf(File file)

        if folder_pos > 0 then
            siblings.RemoveAt(folder_pos)
            siblings.Insert(folder_pos - 1, File file)

            let swapped_with = siblings.[folder_pos]

            match swapped_with with
            | Folder other_folder ->
                swap_files_in_project(
                    other_folder.EnumerateFiles() |> Seq.map _.ProjectItemElement,
                    [ file.ProjectItemElement ]
                )
            | File other_file -> swap_files_in_project([ other_file.ProjectItemElement ], [ file.ProjectItemElement ])

            if folder_pos + 1 < siblings.Count then
                merge_folders_if_needed(siblings.[folder_pos], siblings.[folder_pos + 1], siblings)

            project.Save()

    [<Extension>]
    static member MoveDown(project: Project, file: FileTreeFile) : unit =
        let siblings = file.Parent.Children
        let folder_pos = siblings.IndexOf(File file)

        if folder_pos + 1 < siblings.Count then
            siblings.RemoveAt(folder_pos)
            siblings.Insert(folder_pos + 1, File file)

            let swapped_with = siblings.[folder_pos]

            match swapped_with with
            | Folder other_folder ->
                swap_files_in_project(
                    [ file.ProjectItemElement ],
                    other_folder.EnumerateFiles() |> Seq.map _.ProjectItemElement
                )
            | File other_file -> swap_files_in_project([ file.ProjectItemElement ], [ other_file.ProjectItemElement ])

            if folder_pos >= 1 then
                merge_folders_if_needed(siblings.[folder_pos - 1], siblings.[folder_pos], siblings)

            project.Save()

    [<Extension>]
    static member MoveUp(project: Project, folder: FileTreeFolder) : unit =
        let siblings = folder.Parent.Children
        let folder_pos = siblings.IndexOf(Folder folder)

        if folder_pos > 0 then
            siblings.RemoveAt(folder_pos)
            siblings.Insert(folder_pos - 1, Folder folder)

            let swapped_with = siblings.[folder_pos]

            match swapped_with with
            | Folder other_folder ->
                swap_files_in_project(
                    other_folder.EnumerateFiles() |> Seq.map _.ProjectItemElement,
                    folder.EnumerateFiles() |> Seq.map _.ProjectItemElement
                )
            | File other_file ->
                swap_files_in_project(
                    [ other_file.ProjectItemElement ],
                    folder.EnumerateFiles() |> Seq.map _.ProjectItemElement
                )

            if folder_pos >= 2 then
                merge_folders_if_needed(siblings.[folder_pos - 1], siblings.[folder_pos - 2], siblings)

            if folder_pos + 1 < siblings.Count then
                merge_folders_if_needed(siblings.[folder_pos], siblings.[folder_pos + 1], siblings)

            project.Save()

    [<Extension>]
    static member MoveDown(project: Project, folder: FileTreeFolder) : unit =
        let siblings = folder.Parent.Children
        let folder_pos = siblings.IndexOf(Folder folder)

        if folder_pos + 1 < siblings.Count then
            siblings.RemoveAt(folder_pos)
            siblings.Insert(folder_pos + 1, Folder folder)

            let swapped_with = siblings.[folder_pos]

            match swapped_with with
            | Folder other_folder ->
                swap_files_in_project(
                    folder.EnumerateFiles() |> Seq.map _.ProjectItemElement,
                    other_folder.EnumerateFiles() |> Seq.map _.ProjectItemElement
                )
            | File other_file ->
                swap_files_in_project(
                    folder.EnumerateFiles() |> Seq.map _.ProjectItemElement,
                    [ other_file.ProjectItemElement ]
                )

            if folder_pos + 2 < siblings.Count then
                merge_folders_if_needed(siblings.[folder_pos + 1], siblings.[folder_pos + 2], siblings)

            if folder_pos >= 1 then
                merge_folders_if_needed(siblings.[folder_pos - 1], siblings.[folder_pos], siblings)

            project.Save()
