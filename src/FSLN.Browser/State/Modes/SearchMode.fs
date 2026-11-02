namespace FSLN

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

    member this.Reload(workspace: Workspace) : SearchMode =
        let solution = workspace.ReloadSolution()
        let filtered = FileNameFilter(this.Query).Apply(solution)
        { Query = this.Query; Solution = filtered; Selected = FSelection.Find(this.Selected.ToSelection(), filtered) }

    member this.AutoReload(workspace: Workspace) : SearchMode =
        if this.Solution.Original.HasExternalChange() then this.Reload(workspace) else this

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
