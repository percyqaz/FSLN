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
