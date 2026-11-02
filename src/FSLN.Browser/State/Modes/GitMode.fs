namespace FSLN

type GitMode =
    {
        Query: string
        Status: GitStatus
        Solution: FilteredSolution
        mutable Selected: FSelection
    }

    member this.Reload(workspace: Workspace) : GitMode =
        let solution = workspace.ReloadSolution()
        let filtered = GitChangedFilter(this.Query, this.Status).Apply(solution)

        {
            Query = this.Query
            Status = this.Status
            Solution = filtered
            Selected = FSelection.Find(this.Selected.ToSelection(), filtered)
        }

    member this.AutoReload(workspace: Workspace) : GitMode =
        if this.Solution.Original.HasExternalChange() then this.Reload(workspace) else this

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
        member this.Selected
            with get (): FSelection = this.Selected
            and set (v: FSelection) = this.Selected <- v

        member this.Solution: FilteredSolution = this.Solution
