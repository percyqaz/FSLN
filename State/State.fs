namespace FSLN

open FSLN

type State =
    {
        mutable Running: bool
        mutable Solution: Solution
        mutable GitStatus: GitStatus option
        mutable Expanded: Set<string>
        mutable Selected: Selection
        CommandBuffer: CommandBuffer
        mutable StatusLine: string
        mutable Theme: Theme
    }

    member this.IsExpanded(folder: FileTreeFolder) : bool = this.Expanded.Contains(folder.FullPath)

    member this.IsExpanded(project: Project) : bool =
        this.Expanded.Contains(project.FullPath)

    member this.GitFileStatus(file: string) : GitFileStatus =
        let inline default_status () =
            { Index = Unchanged; WorkingTree = Unchanged }

        match this.GitStatus with
        | Some status ->
            match status.Files.TryGetValue(file) with
            | true, result -> result
            | false, _ -> default_status()
        | None -> default_status()

    static member Create(solution: Solution) : State =
        {
            Running = true
            Solution = solution
            GitStatus = GitStatus.Fetch()
            Expanded = Set.empty
            Selected = Selection.Solution(solution)
            CommandBuffer = CommandBuffer()
            StatusLine = ""
            Theme = Theme.Default
        }
