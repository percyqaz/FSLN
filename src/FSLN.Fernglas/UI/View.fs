namespace Fernglas

open System

type View(state: State) =

    let view = ScreenBuffer(Console.BufferHeight - 3)

    do State.DirectoryChanged.Publish.Add(fun () -> view.ScrollToTop())

    member this.RenderEntries() : unit =
        view.Height <- Console.BufferHeight - 3
        let selected = state.Selected

        for entry in state.FilteredEntries do
            let is_selected = selected = Some entry

            let line =
                match entry with
                | Folder f -> (f + "/").ForeColor(0xFFFF88).Bold()
                | File f -> f

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

        Console.Write(AnsiCodes.CursorToOrigin)
        Console.WriteLine(tagline.ClearRestOfLine())
        this.RenderEntries()
        Console.WriteLine("Fernglas ".ForeColor(0xFFFF88).Bold() + this.StatusLine().ClearRestOfLine())
        Console.Write(state.CommandBuffer.ToString().ForeColor(0x88FF88).Bold().ClearRestOfLine())
