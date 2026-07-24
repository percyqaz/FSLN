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