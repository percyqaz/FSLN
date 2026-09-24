namespace Fernglas

open System

type View(state: State) =

    let view = ScreenBuffer(Console.BufferHeight - 3)

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

    member this.Redraw() : unit =
        Console.Write(AnsiCodes.CursorToOrigin)

        let tagline =
            let loc = state.Directory.ForeColor(0xFF8888)
            sprintf "%s (%i)" loc state.FilteredEntries.Length

        Console.WriteLine(tagline.ClearRestOfLine())

        this.RenderEntries()

        Console.WriteLine(
            "Fernglas ".ForeColor(0xFFFF88).Bold() + state.StatusLine.ForeColor(0x444444).ClearRestOfLine()
        )

        if state.SearchBufferFocused then
            Console.Write(("SEARCH: " + state.SearchBuffer.ToString()).ForeColor(0x8888FF).Bold().ClearRestOfLine())
        else
            Console.Write(state.CommandBuffer.ToString().ForeColor(0x88FF88).Bold().ClearRestOfLine())
