namespace FSLN

open System.Runtime.CompilerServices
open FSLN

type FileTreeReorderOperations =

    static let merge_folders_if_needed
        (entry_one: FileTreeEntry, entry_two: FileTreeEntry, siblings: ResizeArray<FileTreeEntry>)
        : unit =
        match entry_one, entry_two with
        | Folder a, Folder b when a.FullPath = b.FullPath ->
            a.Children.AddRange(b.Children |> Seq.map _.WithParent(Parent.Folder(a)))
            siblings.Remove(entry_two) |> ignore
        | _ -> ()

    static let swap_fsharp_order (above: FileTreeFile seq, below: FileTreeFile seq) : unit =
        let first_above_file = (Seq.head above).ProjectItemElement.Value
        let parent = first_above_file.Parent

        for file in below |> Seq.map _.ProjectItemElement.Value do
            parent.RemoveChild(file)
            parent.InsertBeforeChild(file, first_above_file)

    static let swap_file_system_order (ordering: OrderFile, above: FileTreeEntry, below: FileTreeEntry) : unit =
        let siblings = above.Parent.Children
        ordering.StorePreservingOrder(siblings |> Seq.map _.FullPath)
        ordering.PlaceBefore([ below.FullPath ], above.FullPath)

    [<Extension>]
    static member private SwapOrder(project: Project, above: FileTreeEntry, below: FileTreeEntry) : unit =
        match project.Guts with
        | FileSystem fs -> swap_file_system_order(fs.Ordering, above, below)
        | FSharp _ ->
            let inline as_file_seq (entry: FileTreeEntry) : FileTreeFile seq =
                match entry with
                | File file -> [ file ]
                | Folder folder -> folder.EnumerateFiles()

            swap_fsharp_order(as_file_seq(above), as_file_seq(below))

    [<Extension>]
    static member MoveUp(project: Project, file: FileTreeFile) : unit =
        let siblings = file.Parent.Children
        let folder_pos = siblings.IndexOf(File file)

        if folder_pos > 0 then
            siblings.RemoveAt(folder_pos)
            siblings.Insert(folder_pos - 1, File file)

            let swapped_with = siblings.[folder_pos]
            project.SwapOrder(swapped_with, File file)

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
            project.SwapOrder(File file, swapped_with)

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
            project.SwapOrder(swapped_with, Folder folder)

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
            project.SwapOrder(Folder folder, swapped_with)

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
