namespace FSLN

open System.Runtime.CompilerServices
open FSLN

type State =
    {
        mutable Running: bool
        mutable Solution: Solution
        mutable GitStatus: GitStatus option
        mutable Expanded: Set<string>
        mutable Selected: Selection
        Buffers: BufferManager
        mutable StatusLine: string
        mutable Theme: Theme
    }

    member this.IsExpanded(folder: FileTreeFolder) : bool = this.Expanded.Contains(folder.FullPath)

    member this.IsExpanded(project: Project) : bool =
        this.Expanded.Contains(project.FullPath)

    member this.GitFileStatus(file: string) : GitFileStatus =
        let inline default_status () =
            { Index = Unchanged; WorkingTree = Unchanged }

        match this.GitStatus with
        | Some status ->
            match status.Files.TryGetValue(file) with
            | true, result -> result
            | false, _ -> default_status()
        | None -> default_status()

    [<Extension>]
    static member private RegisterDefaultBinds(buffers: BufferManager) : BufferManager =
        let buffer = buffers.CommandBuffer
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
        
        buffers

    static member Create(solution: Solution) : State =
        {
            Running = true
            Solution = solution
            GitStatus = GitStatus.Fetch()
            Expanded = Set.empty
            Selected = Selection.Solution(solution)
            Buffers = BufferManager.Create().RegisterDefaultBinds()
            StatusLine = ""
            Theme = Theme.Default
        }
