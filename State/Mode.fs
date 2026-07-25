namespace FSLN

type NormalMode =
    {
        mutable Solution: Solution
        mutable Selected: Selection
    }

    member this.Reload() : unit =
        this.Solution <- SolutionLoader.read_solution_file(this.Solution.FullPath)
        this.Selected <- Selection.Solution(this.Solution)

    member this.AutoReload() : unit =
        if this.Solution.HasExternalChange() then
            this.Reload()

type SearchMode =
    {
        Query: string
        Tree: FilteredSolution
        mutable Selected: FSelection
    }

    member this.Update(search: string) : SearchMode =
        if search = this.Query then
            this
        else
            let filtered = FileNameFilter(search).Apply(this.Tree.Original)
            { Query = search; Tree = FileNameFilter(search).Apply(this.Tree.Original); Selected = FSelection.FSolution(filtered) }

    member this.ToNormalMode() : NormalMode =
        { Solution = this.Tree.Original; Selected = this.Selected.ToSelection() }

    static member Create(nm: NormalMode, query: string) : SearchMode =
        let filtered = FileNameFilter(query).Apply(nm.Solution)
        { Query = query; Tree = filtered; Selected = FSelection.FSolution(filtered) }

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
