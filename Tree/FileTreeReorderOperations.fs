namespace FSLN

open System.Runtime.CompilerServices
open Microsoft.Build.Construction
open FSLN

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
                    other_folder.EnumerateFiles() |> Seq.map _.ProjectItemElement.Value,
                    [ file.ProjectItemElement.Value ]
                )
            | File other_file ->
                swap_files_in_project([ other_file.ProjectItemElement.Value ], [ file.ProjectItemElement.Value ])

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
                    [ file.ProjectItemElement.Value ],
                    other_folder.EnumerateFiles() |> Seq.map _.ProjectItemElement.Value
                )
            | File other_file ->
                swap_files_in_project([ file.ProjectItemElement.Value ], [ other_file.ProjectItemElement.Value ])

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
                    other_folder.EnumerateFiles() |> Seq.map _.ProjectItemElement.Value,
                    folder.EnumerateFiles() |> Seq.map _.ProjectItemElement.Value
                )
            | File other_file ->
                swap_files_in_project(
                    [ other_file.ProjectItemElement.Value ],
                    folder.EnumerateFiles() |> Seq.map _.ProjectItemElement.Value
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
                    folder.EnumerateFiles() |> Seq.map _.ProjectItemElement.Value,
                    other_folder.EnumerateFiles() |> Seq.map _.ProjectItemElement.Value
                )
            | File other_file ->
                swap_files_in_project(
                    folder.EnumerateFiles() |> Seq.map _.ProjectItemElement.Value,
                    [ other_file.ProjectItemElement.Value ]
                )

            if folder_pos + 2 < siblings.Count then
                merge_folders_if_needed(siblings.[folder_pos + 1], siblings.[folder_pos + 2], siblings)

            if folder_pos >= 1 then
                merge_folders_if_needed(siblings.[folder_pos - 1], siblings.[folder_pos], siblings)

            project.Save()

    [<Extension>]
    static member MoveUp(solution: Solution, project: Project) : unit =
        let siblings = solution.Projects
        let pos = siblings.IndexOf(project)

        if pos > 0 then
            siblings.RemoveAt(pos)
            siblings.Insert(pos - 1, project)

            solution.Ordering.StorePreservingOrder(siblings |> Seq.map _.FullPath)
            solution.Ordering.PlaceBefore([ project.FullPath ], siblings.[pos].FullPath)
            solution.Ordering.Save()

    [<Extension>]
    static member MoveDown(solution: Solution, project: Project) : unit =
        let siblings = solution.Projects
        let pos = siblings.IndexOf(project)

        if pos + 1 < siblings.Count then
            siblings.RemoveAt(pos)
            siblings.Insert(pos + 1, project)

            solution.Ordering.StorePreservingOrder(siblings |> Seq.map _.FullPath)
            solution.Ordering.PlaceAfter([ project.FullPath ], siblings.[pos].FullPath)
            solution.Ordering.Save()
