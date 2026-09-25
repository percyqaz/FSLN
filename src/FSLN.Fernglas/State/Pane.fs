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

[<CustomEquality>]
[<NoComparison>]
[<Struct>]
type HistoryEntry =
    {
        Directory: string
        SelectedName: string
    }

    override this.Equals(other: obj) : bool =
        match other with
        | :? HistoryEntry as h -> h.Directory = this.Directory
        | _ -> false

    override this.GetHashCode() : int = this.Directory.GetHashCode()

type Pane =
    {
        mutable Directory: string
        mutable GitStatus: GitStatus option
        mutable Entries: FileSystemEntry array
        mutable FilteredEntries: FileSystemEntry array
        mutable Selection: int
        mutable History: HistoryEntry list
        IsMain: bool
        SearchBuffer: TextBuffer
    }

    static member val MainDirectoryChanged = Event<unit>()
    static member val SplitDirectoryChanged = Event<unit>()

    member this.Selected: FileSystemEntry option =
        if this.Selection < 0 then None else Some this.FilteredEntries.[this.Selection]

    member this.GitFileStatus(file: string) : GitFileStatus =
        let inline default_status () =
            { Index = Unchanged; WorkingTree = Unchanged }

        match this.GitStatus with
        | Some status ->
            match status.Files.TryGetValue(file.Replace("\\", "/")) with
            | true, result -> result
            | false, _ -> default_status()
        | None -> default_status()

    member this.TrySelectByName(name: string) : unit =
        match this.FilteredEntries |> Array.tryFindIndex(fun f -> f.Name = name) with
        | Some i -> this.Selection <- i
        | None -> ()

    static member Create(path: string, is_main_pane: bool) : Pane =
        let pane =
            {
                Directory = path
                GitStatus = None
                Entries = [||]
                FilteredEntries = [||]
                Selection = -1
                History = []
                IsMain = is_main_pane
                SearchBuffer = TextBuffer()
            }

        pane.ChangeDirectory(path, false)
        pane

    member internal this.UpdateSearchResults(previous_selection: FileSystemEntry option) : unit =
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

    member this.AppendHistory() : unit =
        let to_add =
            { Directory = this.Directory; SelectedName = this.Selected |> Option.map _.Name |> Option.defaultValue "" }

        let is_duplicate = List.contains to_add this.History

        if not is_duplicate then
            this.History <- to_add :: this.History

    member this.PopHistory() : HistoryEntry option =
        match this.History with
        | h :: hs ->
            this.History <- hs
            Some h
        | _ -> None

    member this.TopHistory() : HistoryEntry option = List.tryHead this.History

    member this.ChangeDirectory(path: string, include_in_history: bool) : unit =

        if include_in_history then
            this.AppendHistory()
        else
            match this.TopHistory() with
            | Some h when h.Directory = path -> ignore(this.PopHistory())
            | _ -> ()

        this.Directory <- path
        this.SearchBuffer.Clear()

        if this.IsMain then
            Directory.SetCurrentDirectory(this.Directory)
            let user_profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            File.WriteAllText(Path.Combine(user_profile, ".fernglas_location"), path)
            this.Refresh()
            Pane.MainDirectoryChanged.Trigger()
        else
            Pane.SplitDirectoryChanged.Trigger()
