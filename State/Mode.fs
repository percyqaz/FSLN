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

type SearchMode = { mutable Tree: FilteredSolution; mutable Selected: FSelection }

[<RequireQualifiedAccess>]
type Mode =
    | Normal of NormalMode
    | Search of SearchMode

    member this.Selection: Selection =
        match this with
        | Normal nm -> nm.Selected
        | Search sm -> sm.Selected.ToSelection()
