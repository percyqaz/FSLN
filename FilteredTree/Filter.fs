namespace FSLN

open System

[<AbstractClass>]
type Filter() =

    abstract member Apply: FileTreeFile -> bool

    member private this.Apply(entry: FileTreeEntry, parent: FParent) : FilteredTreeEntry option =
        match entry with
        | Folder folder ->
            let ffolder = { Original = folder; Parent = parent; Children = ResizeArray() }

            for child in folder.Children |> Seq.choose(fun c -> this.Apply(c, FParent.FFolder(ffolder))) do
                ffolder.Children.Add(child)

            if ffolder.Children.Count > 0 then Some(FFolder ffolder) else None
        | File file -> if this.Apply(file) then Some(FFile { Original = file; Parent = parent }) else None

    member this.Apply(project: Project) : FilteredProject option =
        let fproject = { Original = project; Children = ResizeArray() }

        for child in project.Children |> Seq.choose(fun c -> this.Apply(c, FParent.FProject(fproject))) do
            fproject.Children.Add(child)

        if fproject.Children.Count > 0 then Some(fproject) else None

    member this.Apply(solution: Solution) : FilteredSolution =
        { Original = solution; Projects = solution.Projects |> Seq.choose this.Apply |> ResizeArray }

type FileNameFilter(search: string) =
    inherit Filter()

    override this.Apply(file: FileTreeFile) : bool =
        file.Name.Contains(search, StringComparison.InvariantCultureIgnoreCase)

type GitChangedFilter(search: string, status: GitStatus) =
    inherit Filter()

    override this.Apply(file: FileTreeFile) : bool =
        status.Files.ContainsKey(file.FullPath)
        && file.Name.Contains(search, StringComparison.InvariantCultureIgnoreCase)
