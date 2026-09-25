namespace Fernglas

open System
open System.IO

type View(state: State) =

    let main_view = ScreenBuffer(Console.BufferHeight - 3)
    let split_view = ScreenBuffer(Console.BufferHeight - 3)
    let mutable pane_width = Console.BufferWidth / 2

    do
        Pane.MainDirectoryChanged.Publish.Add(fun () -> main_view.ScrollToTop())
        Pane.SplitDirectoryChanged.Publish.Add(fun () -> split_view.ScrollToTop())

    member this.Transform(pane: Pane) : string -> string =
        if pane.IsMain then (fun s -> s + "\n") else (fun s -> AnsiCodes.CursorRight(pane_width) + s + "\n")

    member this.RenderEntry(pane: Pane, entry: FileSystemEntry) : string =
        match entry with
        | Folder f -> (f + "/").PadRight(pane_width).ForeColor(0xFFFF88).Bold()
        | File f ->
            let git_status = pane.GitFileStatus(Path.Combine(pane.Directory, f))
            let wt = git_status.WorkingTree <> Unchanged
            let status = if wt then git_status.WorkingTree else git_status.Index

            let color =
                match status with
                | Added
                | Untracked -> 0x88ff88
                | Deleted -> 0xff8888
                | Unchanged -> 0xdddddd
                | _ -> 0x88ffff

            let dirty_icon = if wt then " *" else ""

            f.ForeColor(color) + dirty_icon.PadRight(max 0 (pane_width - f.Length)).ForeColor(0x444444)

    member this.RenderEntries(pane: Pane) : unit =
        let view = if pane.IsMain then main_view else split_view
        view.Height <- Console.BufferHeight - 4
        let selected = pane.Selected

        for entry in pane.FilteredEntries do
            let is_selected = selected = Some entry
            let line = this.RenderEntry(pane, entry)
            let fmt_line = if is_selected then line.BackColor(0x666633) else line
            view.Line(fmt_line, is_selected)

        view.RenderToArray("".PadRight(pane_width))
        |> Seq.map(this.Transform(pane))
        |> String.concat ""
        |> Console.Write

    member this.PaneHeader(pane: Pane, is_focused: bool) : string =
        let location = pane.Directory.Replace("\\", " > ").Replace("/", " > ")
        let line = location.PadRight(pane_width).ForeColor(0x88FFFF)
        if is_focused then line.BackColor(0x333322) else line.BackColor(0x222222)

    member this.PaneFooter(pane: Pane, is_focused: bool) : string =

        let inline fmt_ahead_behind (leading_symbol: char, count: int option) =
            match count with
            | Some count when count < 10 -> (sprintf " %c%i" leading_symbol count)
            | Some count -> (sprintf "%c%i" leading_symbol count)
            | None -> sprintf " %c0" leading_symbol

        let inline dirty_files (status: GitStatus) : string =
            if status.WorkingTreeDirty > 0 then sprintf " *%- 2i" status.WorkingTreeDirty
            elif status.IndexDirty > 0 then " *  "
            else "    "

        let git_status, git_status_width =
            match pane.GitStatus with
            | Some status ->
                sprintf
                    "[%s%s%s]%s"
                    (status.Branch.ForeColor(0x8888ff).Bold())
                    (fmt_ahead_behind('+', status.Ahead).ForeColor(0x88FF88))
                    (fmt_ahead_behind('-', status.Behind).ForeColor(0xFF8888))
                    (dirty_files(status).ForeColor(0x444444)),
                12 + status.Branch.Length
            | None -> "", 0

        let search_query = pane.SearchBuffer.ToString()

        let entries, entries_width =
            if search_query <> "" then
                let line = sprintf "- %s: %i results - " search_query pane.FilteredEntries.Length
                line.ForeColor(0x8888FF), line.Length
            else
                let line = sprintf "- %i entries - " pane.FilteredEntries.Length
                line, line.Length

        let padding = "".PadRight(max 0 (pane_width - git_status_width - entries_width))
        let line = sprintf "%s%s%s" git_status padding entries
        if is_focused then line.BackColor(0x333322) else line.BackColor(0x222222)

    member this.RenderPane(pane: Pane) : unit =
        let is_focused = pane.IsMain <> state.SplitPaneFocused
        let transform = this.Transform(pane)
        Console.Write(transform(this.PaneHeader(pane, is_focused)))
        this.RenderEntries(pane)
        Console.Write(transform(this.PaneFooter(pane, is_focused)))

    member this.Redraw() : unit =
        Console.Write(AnsiCodes.CursorInvisible + AnsiCodes.CursorToOrigin)

        pane_width <- if state.SplitPane.IsSome then Console.BufferWidth / 2 else Console.BufferWidth
        this.RenderPane(state.MainPane)

        match state.SplitPane with
        | Some side_pane ->
            Console.Write(AnsiCodes.CursorToOrigin)
            this.RenderPane(side_pane)
        | None -> ()

        let status_line =
            "Fernglas ".ForeColor(0xFFCC88).Bold() + state.StatusLine.ForeColor(0x444444)

        let buffer =
            if state.SearchBufferFocused then
                "-- SEARCH MODE --"
            else
                state.CommandBuffer.ToString() + AnsiCodes.CursorVisible

        Console.WriteLine(status_line.ClearRestOfLine())
        Console.Write(buffer.ForeColor(0x88FF88).Bold().ClearRestOfLine())
