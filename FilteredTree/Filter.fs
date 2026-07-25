namespace FSLN

open System

[<AbstractClass>]
type Filter() =

    abstract member Apply: FileTreeFile -> bool

    member this.Apply(entry: FileTreeEntry) : FilteredTreeEntry option =
        match entry with
        | Folder folder ->
            let filtered_children = folder.Children |> Seq.choose this.Apply |> Array.ofSeq
            if filtered_children.Length > 0 then Some(FFolder { Original = folder; Children = filtered_children }) else None
        | File file -> if this.Apply(file) then Some(FFile { Original = file }) else None

    member this.Apply(project: Project) : FilteredProject option =
        let filtered_children = project.Children |> Seq.choose this.Apply |> Array.ofSeq
        if filtered_children.Length > 0 then Some({ Original = project; Children = filtered_children }) else None

    member this.Apply(solution: Solution) : FilteredSolution =
        { Original = solution; Projects = solution.Projects |> Seq.choose this.Apply |> Array.ofSeq }

type FileNameFilter(search: string) =
    inherit Filter()

    override this.Apply(file: FileTreeFile) : bool =
        file.Name.Contains(search, StringComparison.InvariantCultureIgnoreCase)

type GitChangedFilter(search: string, status: GitStatus) =
    inherit Filter()

    override this.Apply(file: FileTreeFile) : bool =
        status.Files.ContainsKey(file.FullPath)
        && file.Name.Contains(search, StringComparison.InvariantCultureIgnoreCase)
