namespace FSLN

type NormalMode =
    {
        Solution: Solution
        mutable Selected: Selection
    }

    member this.Reload() : NormalMode =
        let solution = SolutionLoader.read_solution_file(this.Solution.FullPath)
        { Solution = solution; Selected = Selection.Solution(this.Solution) } // todo: recover selection

    member this.AutoReload() : NormalMode =
        if this.Solution.HasExternalChange() then this.Reload() else this

[<Interface>]
type ISearchMode =
    abstract member Solution: FilteredSolution with get
    abstract member Selected: FSelection with get, set

type SearchMode =
    {
        Query: string
        Solution: FilteredSolution
        mutable Selected: FSelection
    }

    member this.Reload() : SearchMode =
        let solution = SolutionLoader.read_solution_file(this.Solution.Original.FullPath)
        let filtered = FileNameFilter(this.Query).Apply(solution)

        { Query = this.Query; Solution = filtered; Selected = FSelection.Find(this.Selected.ToSelection(), filtered) }

    member this.AutoReload() : SearchMode =
        if this.Solution.Original.HasExternalChange() then this.Reload() else this

    member this.Update(query: string) : SearchMode =
        if query = this.Query then
            this
        else
            let filtered = FileNameFilter(query).Apply(this.Solution.Original)
            { Query = query; Solution = filtered; Selected = FSelection.Find(this.Selected.ToSelection(), filtered) }

    member this.ToNormalMode() : NormalMode =
        { Solution = this.Solution.Original; Selected = this.Selected.ToSelection() }

    static member Create(nm: NormalMode, query: string) : SearchMode =
        let filtered = FileNameFilter(query).Apply(nm.Solution)
        { Query = query; Solution = filtered; Selected = FSelection.Find(nm.Selected, filtered) }

    interface ISearchMode with
        member this.Selected: FSelection = this.Selected
        member this.set_Selected(v: FSelection) : unit = this.Selected <- v
        member this.Solution: FilteredSolution = this.Solution

type GitMode =
    {
        Query: string
        Status: GitStatus
        Solution: FilteredSolution
        mutable Selected: FSelection
    }

    member this.Reload() : GitMode =
        let solution = SolutionLoader.read_solution_file(this.Solution.Original.FullPath)
        let filtered = GitChangedFilter(this.Query, this.Status).Apply(solution)

        {
            Query = this.Query
            Status = this.Status
            Solution = filtered
            Selected = FSelection.Find(this.Selected.ToSelection(), filtered)
        }

    member this.AutoReload() : GitMode =
        if this.Solution.Original.HasExternalChange() then this.Reload() else this

    member this.Update(query: string, git_status: GitStatus) : GitMode =
        if
            query = this.Query
            && git_status.IndexDirty = this.Status.IndexDirty
            && git_status.WorkingTreeDirty = this.Status.WorkingTreeDirty
        then
            this
        else
            let filtered = GitChangedFilter(query, git_status).Apply(this.Solution.Original)

            {
                Query = query
                Status = git_status
                Solution = filtered
                Selected = FSelection.Find(this.Selected.ToSelection(), filtered)
            }

    member this.ToNormalMode() : NormalMode =
        { Solution = this.Solution.Original; Selected = this.Selected.ToSelection() }

    static member Create(nm: NormalMode, query: string, git_status: GitStatus) : GitMode =
        let filtered = GitChangedFilter(query, git_status).Apply(nm.Solution)

        {
            Query = query
            Status = git_status
            Solution = filtered
            Selected = FSelection.Find(nm.Selected, filtered)
        }

    interface ISearchMode with
        member this.Selected: FSelection = this.Selected
        member this.set_Selected(v: FSelection) : unit = this.Selected <- v
        member this.Solution: FilteredSolution = this.Solution

[<RequireQualifiedAccess>]
type Mode =
    | Normal of NormalMode
    | Search of SearchMode
    | Git of GitMode

    member this.Solution: Solution =
        match this with
        | Normal nm -> nm.Solution
        | Search sm -> sm.Solution.Original
        | Git gm -> gm.Solution.Original

    member this.Selection: Selection =
        match this with
        | Normal nm -> nm.Selected
        | Search sm -> sm.Selected.ToSelection()
        | Git gm -> gm.Selected.ToSelection()

    member this.ToggleGitMode(git_status: GitStatus option) : Mode =
        match git_status with
        | Some git_status ->
            match this with
            | Normal nm -> Git(GitMode.Create(nm, "", git_status))
            | Search sm -> Git(GitMode.Create(sm.ToNormalMode(), sm.Query, git_status))
            | Git gm ->
                if gm.Query <> "" then
                    Search(SearchMode.Create(gm.ToNormalMode(), gm.Query))
                else
                    Normal(gm.ToNormalMode())
        | None -> this

    member this.Update(query: string, git_status: GitStatus option) : Mode =
        match this with
        | Normal nm -> if query <> "" then Search(SearchMode.Create(nm, query)) else Normal(nm)
        | Search sm -> if query <> "" then Search(sm.Update(query)) else Normal(sm.ToNormalMode())
        | Git gm ->
            match git_status with
            | Some git_status -> Git(gm.Update(query, git_status))
            | None -> Normal(gm.ToNormalMode())

    member this.Reload() : Mode =
        match this with
        | Mode.Normal nm -> Mode.Normal(nm.Reload())
        | Mode.Search sm -> Mode.Search(sm.Reload())
        | Mode.Git gm -> Mode.Git(gm.Reload())

    member this.AutoReload() : Mode =
        match this with
        | Mode.Normal nm -> Mode.Normal(nm.AutoReload())
        | Mode.Search sm -> Mode.Search(sm.AutoReload())
        | Mode.Git gm -> Mode.Git(gm.AutoReload())
