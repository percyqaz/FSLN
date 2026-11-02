namespace FSLN

[<RequireQualifiedAccess>]
type Selection =
    | File of FileTreeFile
    | Folder of FileTreeFolder
    | Project of Project
    | Solution of Solution

    member this.ParentProject: Project option =
        match this with
        | File file -> Some file.ParentProject
        | Folder folder -> Some folder.ParentProject
        | Project project -> Some project
        | Solution _ -> None

    member this.FullPath: string =
        match this with
        | File file -> file.FullPath
        | Folder folder -> folder.FullPath
        | Project project -> project.FullPath
        | Solution solution -> solution.FullPath

    member this.ToParent() : Parent option =
        match this with
        | File file -> Some file.Parent
        | Folder folder -> Some(Parent.Folder(folder))
        | Project project -> Some(Parent.Project(project))
        | Solution _ -> None

    static member Find(selection: Selection, tree: Solution) : Selection =

        let inline search_matching_folder (folder: FileTreeFolder) : FileTreeFolder option =
            tree.Projects |> Seq.collect _.EnumerateSubfolders() |> Seq.tryFind(fun f -> f.FullPath = folder.FullPath)

        let inline search_matching_file (file: FileTreeFile) : FileTreeFile option =
            tree.Projects |> Seq.collect _.EnumerateFiles() |> Seq.tryFind(fun f -> f.FullPath = file.FullPath)

        match selection with
        | Selection.Solution _ -> Selection.Solution(tree)
        | Selection.Project project ->
            match tree.Projects |> Seq.tryFind(fun p -> p.FullPath = project.FullPath) with
            | Some p -> Selection.Project(p)
            | None -> Selection.Solution(tree)
        | Selection.Folder folder ->
            match search_matching_folder(folder) with
            | Some f -> Selection.Folder(f)
            | None -> Selection.Solution(tree)
        | Selection.File file ->
            match search_matching_file(file) with
            | Some f -> Selection.File(f)
            | None -> Selection.Solution(tree)

    member this.Equals(file: FileTreeFile) : bool = this = File file
    member this.Equals(folder: FileTreeFolder) : bool = this = Folder folder
    member this.Equals(project: Project) : bool = this = Project project
    member this.Equals(solution: Solution) : bool = this = Solution solution
