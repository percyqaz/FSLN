namespace FSLN

type NormalMode =
    {
        Solution: Solution
        mutable Selected: Selection
    }

    member this.Reload() : NormalMode =
        let solution =
            SolutionLoader.read_solution_file(this.Solution.Ordering, this.Solution.FullPath)

        { Solution = solution; Selected = Selection.Solution(this.Solution) } // todo: recover selection

    member this.AutoReload() : NormalMode =
        if this.Solution.HasExternalChange() then this.Reload() else this
