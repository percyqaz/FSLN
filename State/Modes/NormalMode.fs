namespace FSLN

type NormalMode =
    {
        Solution: Solution
        mutable Selected: Selection
    }

    member this.Reload(workspace: Workspace) : NormalMode =
        let solution = workspace.ReloadSolution()
        { Solution = solution; Selected = Selection.Solution(this.Solution) } // todo: recover selection

    member this.AutoReload(workspace: Workspace) : NormalMode =
        if this.Solution.HasExternalChange() then this.Reload(workspace) else this
