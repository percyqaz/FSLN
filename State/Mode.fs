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

type SearchMode =
    {
        Query: string
        Tree: FilteredSolution
        mutable Selected: FSelection
    }

    member this.Reload() : SearchMode =
        let solution = SolutionLoader.read_solution_file(this.Tree.Original.FullPath)
        let filtered = FileNameFilter(this.Query).Apply(solution)

        { Query = this.Query; Tree = filtered; Selected = FSelection.Find(this.Selected.ToSelection(), filtered) }

    member this.AutoReload() : SearchMode =
        if this.Tree.Original.HasExternalChange() then this.Reload() else this

    member this.Update(query: string) : SearchMode =
        if query = this.Query then
            this
        else
            let filtered = FileNameFilter(query).Apply(this.Tree.Original)

            { Query = query; Tree = filtered; Selected = FSelection.Find(this.Selected.ToSelection(), filtered) }

    member this.ToNormalMode() : NormalMode =
        { Solution = this.Tree.Original; Selected = this.Selected.ToSelection() }

    static member Create(nm: NormalMode, query: string) : SearchMode =
        let filtered = FileNameFilter(query).Apply(nm.Solution)
        { Query = query; Tree = filtered; Selected = FSelection.Find(nm.Selected, filtered) }

[<RequireQualifiedAccess>]
type Mode =
    | Normal of NormalMode
    | Search of SearchMode

    member this.Solution: Solution =
        match this with
        | Normal nm -> nm.Solution
        | Search sm -> sm.Tree.Original

    member this.Selection: Selection =
        match this with
        | Normal nm -> nm.Selected
        | Search sm -> sm.Selected.ToSelection()

    member this.SearchUpdated(query: string) : Mode =
        match this with
        | Normal nm -> if query <> "" then Search(SearchMode.Create(nm, query)) else Normal(nm)
        | Search sm -> if query <> "" then Search(sm.Update(query)) else Normal(sm.ToNormalMode())
