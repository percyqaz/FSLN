namespace Fernglas

open System
open System.IO

type FileSystemEntry =
    | Folder of name: string
    | File of name: string

    member this.Name: string =
        match this with
        | Folder f -> f + "/"
        | File f -> f

type State =
    {
        mutable Running: bool
        mutable Directory: string
        mutable GitStatus: GitStatus option
        mutable Entries: FileSystemEntry array
        mutable FilteredEntries: FileSystemEntry array
        mutable Selection: int
        CommandBuffer: CommandBuffer
        SearchBuffer: TextBuffer
        mutable SearchBufferFocused: bool
        mutable StatusLine: string
        Keymap: Keymap
    }

    member this.Selected: FileSystemEntry option =
        if this.Selection < 0 then None else Some this.FilteredEntries.[this.Selection]

    member this.GitFileStatus(file: string) : GitFileStatus =
        let inline default_status () =
            { Index = Unchanged; WorkingTree = Unchanged }

        match this.GitStatus with
        | Some status ->
            match status.Files.TryGetValue(file) with
            | true, result -> result
            | false, _ -> default_status()
        | None -> default_status()

    member this.TrySelectByName(name: string) : unit =
        match this.FilteredEntries |> Array.tryFindIndex(fun f -> f.Name = name) with
        | Some i -> this.Selection <- i
        | None -> ()

    static member Create(path: string, keymap: Keymap) : State =
        let entries =
            seq {
                for folder in Directory.EnumerateDirectories(path) do
                    yield Folder(Path.GetFileName(folder))

                for file in Directory.EnumerateFiles(path) do
                    yield File(Path.GetFileName(file))
            }
            |> Array.ofSeq

        {
            Running = true
            Directory = path
            GitStatus = GitStatus.Fetch()
            Entries = entries
            FilteredEntries = entries
            Selection = -1
            CommandBuffer = CommandBuffer()
            SearchBuffer = TextBuffer()
            SearchBufferFocused = false
            StatusLine = ""
            Keymap = keymap
        }

    member private this.UpdateSearchResults(previous_selection: FileSystemEntry option) : unit =
        let query = this.SearchBuffer.ToString()

        this.FilteredEntries <-
            this.Entries |> Array.filter _.Name.Contains(query, StringComparison.InvariantCultureIgnoreCase)

        this.Selection <-
            match previous_selection with
            | Some s -> Array.IndexOf(this.FilteredEntries, s)
            | None -> -1

    member this.RefreshGit() : unit = this.GitStatus <- GitStatus.Fetch()

    member this.Refresh() : unit =
        let previous_selection = this.Selected

        Directory.SetCurrentDirectory(this.Directory)
        this.RefreshGit()

        this.Entries <-
            seq {
                for folder in Directory.EnumerateDirectories(this.Directory) do
                    yield Folder(Path.GetFileName(folder))

                for file in Directory.EnumerateFiles(this.Directory) do
                    yield File(Path.GetFileName(file))
            }
            |> Array.ofSeq

        this.UpdateSearchResults(previous_selection)

    member this.AddKey(input: ConsoleKeyInfo) : unit =
        if this.SearchBufferFocused then
            if this.SearchBuffer.TryAddKey(input) then
                this.UpdateSearchResults(this.Selected)
            else
                this.SearchBufferFocused <- false
        else
            this.CommandBuffer.AddKey(input)
