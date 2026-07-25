namespace FSLN

[<RequireQualifiedAccess>]
type FSelection =
    | FFile of FilteredTreeFile
    | FFolder of FilteredTreeFolder
    | FProject of FilteredProject
    | FSolution of FilteredSolution

    member this.ToSelection() : Selection =
        match this with
        | FFile file -> Selection.File(file.Original)
        | FFolder folder -> Selection.Folder(folder.Original)
        | FProject project -> Selection.Project(project.Original)
        | FSolution solution -> Selection.Solution(solution.Original)

    static member Find(selection: Selection, tree: FilteredSolution) : FSelection =

        let inline search_matching_folder (folder: FileTreeFolder) : FilteredTreeFolder option =
            tree.Projects |> Seq.collect _.EnumerateSubfolders() |> Seq.tryFind(fun f -> f.Original = folder)

        let inline search_matching_file (file: FileTreeFile) : FilteredTreeFile option =
            tree.Projects |> Seq.collect _.EnumerateFiles() |> Seq.tryFind(fun f -> f.Original = file)

        match selection with
        | Selection.Solution _ -> FSelection.FSolution(tree)
        | Selection.Project project ->
            match tree.Projects |> Seq.tryFind(fun p -> p.Original = project) with
            | Some p -> FSelection.FProject(p)
            | None -> FSelection.FSolution(tree)
        | Selection.Folder folder ->
            match search_matching_folder(folder) with
            | Some f -> FSelection.FFolder(f)
            | None -> FSelection.FSolution(tree)
        | Selection.File file ->
            match search_matching_file(file) with
            | Some f -> FSelection.FFile(f)
            | None -> FSelection.FSolution(tree)
