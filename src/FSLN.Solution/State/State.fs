namespace FSLN.Solution

open System
open FSLN.Shared

[<RequireQualifiedAccess>]
type ActiveBuffer =
    | Command
    | Search

type State =
    {
        mutable Running: bool
        Workspace: Workspace
        mutable GitStatus: GitStatus option
        mutable Expanded: Set<string>
        mutable Mode: Mode
        Keymap: Keymap
        CommandBuffer: CommandBuffer
        SearchBuffer: TextBuffer
        mutable ActiveBuffer: ActiveBuffer
        mutable StatusLine: string
        mutable Theme: Theme
        Editors: Editors
    }

    member this.IsExpanded(folder: FileTreeFolder) : bool = this.Expanded.Contains(folder.FullPath)

    member this.IsExpanded(project: Project) : bool =
        this.Expanded.Contains(project.FullPath)

    member this.IsSelected(file: FileTreeFile) : bool = this.Mode.Selection.Equals(file)
    member this.IsSelected(folder: FileTreeFolder) : bool = this.Mode.Selection.Equals(folder)
    member this.IsSelected(project: Project) : bool = this.Mode.Selection.Equals(project)
    member this.IsSelected(solution: Solution) : bool = this.Mode.Selection.Equals(solution)

    member this.GitFileStatus(file: string) : GitFileStatus =
        let inline default_status () =
            { Index = Unchanged; WorkingTree = Unchanged }

        match this.GitStatus with
        | Some status ->
            match status.Files.TryGetValue(file) with
            | true, result -> result
            | false, _ -> default_status()
        | None -> default_status()

    member this.AddKey(input: ConsoleKeyInfo) : unit =
        match this.ActiveBuffer with
        | ActiveBuffer.Command -> this.CommandBuffer.AddKey(input)
        | ActiveBuffer.Search ->
            if this.SearchBuffer.TryAddKey(input) then
                this.Mode <- this.Mode.Update(this.SearchBuffer.ToString(), this.GitStatus)
            else
                this.ActiveBuffer <- ActiveBuffer.Command

    static member Create(workspace: Workspace) : State =
        let solution = workspace.ReloadSolution()

        {
            Running = true
            Workspace = workspace
            GitStatus = GitStatus.Fetch(workspace.RootPath)
            Expanded = Set.empty
            Mode = Mode.Normal({ Solution = solution; Selected = Selection.Solution(solution) })
            Keymap = Keymap()
            CommandBuffer = CommandBuffer()
            SearchBuffer = TextBuffer()
            ActiveBuffer = ActiveBuffer.Command
            StatusLine = ""
            Theme = Theme.Default
            Editors = Editors.Default
        }
