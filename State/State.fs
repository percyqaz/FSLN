namespace FSLN

open System
open System.Runtime.CompilerServices
open FSLN

[<RequireQualifiedAccess>]
type ActiveBuffer =
    | Command
    | Search

type State =
    {
        mutable Running: bool
        mutable GitStatus: GitStatus option
        mutable Expanded: Set<string>
        mutable Mode: Mode
        CommandBuffer: CommandBuffer
        SearchBuffer: TextBuffer
        mutable ActiveBuffer: ActiveBuffer
        mutable StatusLine: string
        mutable Theme: Theme
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

    static member Create(solution: Solution) : State =
        {
            Running = true
            GitStatus = GitStatus.Fetch()
            Expanded = Set.empty
            Mode = Mode.Normal({ Solution = solution; Selected = Selection.Solution(solution) })
            CommandBuffer = CommandBuffer().RegisterDefaultBinds()
            SearchBuffer = TextBuffer()
            ActiveBuffer = ActiveBuffer.Command
            StatusLine = ""
            Theme = Theme.Default
        }

    [<Extension>]
    static member private RegisterDefaultBinds(buffer: CommandBuffer) : CommandBuffer =
        buffer.Bind("<Esc>", ":q<Enter>")

        buffer.Bind("h", ":collapse<Enter>")
        buffer.Bind("j", ":down<Enter>")
        buffer.Bind("k", ":up<Enter>")
        buffer.Bind("l", ":expand<Enter>")
        buffer.Bind("<A-k>", ":move_up<Enter>")
        buffer.Bind("<A-j>", ":move_down<Enter>")
        buffer.Bind("<Tab>", ":search<Enter>")
        buffer.Bind(".", ":echo $<Enter>")

        buffer.Bind("<Left>", "h")
        buffer.Bind("<Down>", "j")
        buffer.Bind("<Up>", "k")
        buffer.Bind("<Right>", "l")
        buffer.Bind("<A-Up>", "<A-k>")
        buffer.Bind("<A-Down>", "<A-j>")
        // todo: [ ] to jump next/previous sibling

        buffer.Bind(
            "<Enter>",
            ":!C:/Program^ Files/JetBrains/JetBrains^ Rider^ 2026.1/bin/rider64.exe nosplash $<Enter>"
        )

        buffer.Bind("a", "lj")
        buffer
