namespace FSLN

[<RequireQualifiedAccess>]
type FParent =
    | FProject of FilteredProject
    | FFolder of FilteredTreeFolder

    member this.Children =
        match this with
        | FProject project -> project.Children
        | FFolder folder -> folder.Children

and FilteredTreeFile = { Original: FileTreeFile; Parent: FParent }

and FilteredTreeFolder =
    {
        Original: FileTreeFolder
        Parent: FParent
        Children: ResizeArray<FilteredTreeEntry>
    }

    member this.EnumerateFiles() : FilteredTreeFile seq =
        seq {
            for child in this.Children do
                match child with
                | FFile file -> yield file
                | FFolder folder -> yield! folder.EnumerateFiles()
        }

    member this.EnumerateSubfolders() : FilteredTreeFolder seq =
        seq {
            for child in this.Children do
                match child with
                | FFile _ -> ()
                | FFolder folder ->
                    yield folder
                    yield! folder.EnumerateSubfolders()
        }

and FilteredTreeEntry =
    | FFile of FilteredTreeFile
    | FFolder of FilteredTreeFolder

    member this.Parent =
        match this with
        | FFile file -> file.Parent
        | FFolder folder -> folder.Parent


and FilteredProject =
    {
        Original: Project
        Children: ResizeArray<FilteredTreeEntry>
    }

    member this.EnumerateFiles() : FilteredTreeFile seq =
        seq {
            for child in this.Children do
                match child with
                | FFile file -> yield file
                | FFolder folder -> yield! folder.EnumerateFiles()
        }

    member this.EnumerateSubfolders() : FilteredTreeFolder seq =
        seq {
            for child in this.Children do
                match child with
                | FFile _ -> ()
                | FFolder folder ->
                    yield folder
                    yield! folder.EnumerateSubfolders()
        }

type FilteredSolution = { Original: Solution; Projects: ResizeArray<FilteredProject> }
