namespace Fernglas

open System
open System.IO

type View(state: State) =

    let view = ScreenBuffer(Console.BufferHeight - 3)

    do State.DirectoryChanged.Publish.Add(fun () -> view.ScrollToTop())

    member this.RenderEntry(entry: FileSystemEntry) : string =
        match entry with
        | Folder f -> (f + "/").ForeColor(0xFFFF88).Bold()
        | File f ->
            let git_status = state.GitFileStatus(Path.Combine(state.Directory, f))
            let wt = git_status.WorkingTree <> Unchanged
            let status = if wt then git_status.WorkingTree else git_status.Index

            let color =
                match status with
                | Added
                | Untracked -> 0x88ff88
                | Deleted -> 0xff8888
                | Unchanged -> 0xdddddd
                | _ -> 0x88ffff

            let dirty_icon = if wt then " *".ForeColor(0x444444) else ""

            f.ForeColor(color) + dirty_icon

    member this.RenderEntries() : unit =
        view.Height <- Console.BufferHeight - 3
        let selected = state.Selected

        for entry in state.FilteredEntries do
            let is_selected = selected = Some entry
            let line = this.RenderEntry(entry)
            let fmt_line = if is_selected then line.BackColor(0x666633) else line
            view.Line(fmt_line.ClearRestOfLine(), is_selected)

        view.Draw()

    member this.StatusLine() : string =

        let inline fmt_ahead_behind (leading_symbol: char, count: int option) =
            match count with
            | Some count -> (sprintf " %c%i" leading_symbol count)
            | None -> ""

        let inline dirty_files (status: GitStatus) : string =
            if status.WorkingTreeDirty > 0 then sprintf " *%i" status.WorkingTreeDirty
            elif status.IndexDirty > 0 then " *"
            else ""

        let git_status =
            match state.GitStatus with
            | Some status ->
                sprintf
                    "[%s%s%s]%s "
                    (status.Branch.ForeColor(0x8888ff).Bold())
                    (fmt_ahead_behind('+', status.Ahead).ForeColor(0x88FF88))
                    (fmt_ahead_behind('-', status.Behind).ForeColor(0xFF8888))
                    (dirty_files(status).ForeColor(0x444444))
            | None -> ""

        git_status + state.StatusLine.ForeColor(0x444444)

    member this.Redraw() : unit =
        let search_query = state.SearchBuffer.ToString()

        let entries =
            if search_query <> "" then
                (sprintf "[%s: %i results]" search_query state.FilteredEntries.Length).ForeColor(0x8888FF)
            else
                sprintf "[%i entries]" state.FilteredEntries.Length

        let location =
            state.Directory.Replace("\\", " > ").Replace("/", " > ").ForeColor(0x88FFFF)

        let tagline = sprintf "%s %s" location entries

        Console.Write(AnsiCodes.CursorInvisible + AnsiCodes.CursorToOrigin)
        Console.WriteLine(tagline.ClearRestOfLine())
        this.RenderEntries()
        Console.WriteLine("Fernglas ".ForeColor(0xFFCC88).Bold() + this.StatusLine().ClearRestOfLine())
        Console.Write(state.CommandBuffer.ToString().ForeColor(0x88FF88).Bold().ClearRestOfLine())
        Console.Write(AnsiCodes.CursorVisible)
