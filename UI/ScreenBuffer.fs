namespace FSLN

open System
open System.Text

type ScreenBuffer(height: int) =

    let lines = ResizeArray()
    let mutable cursor = 0
    let mutable scroll_position = 0

    member val ScrollOff = 6 with get, set
    member val LinesBelow = 1 with get, set
    member val Height = height with get, set

    member this.CursorHere() : unit = cursor <- lines.Count

    member this.Line(line: string) : unit = lines.Add(line)

    member this.Line(line: string, cursor_here: bool) : unit =
        if cursor_here then
            this.CursorHere()

        this.Line(line)

    member this.Draw() : unit =
        let sb = StringBuilder().Append("\u001b[H")

        let top_of_requested_view = max 0 (cursor - this.ScrollOff)

        if top_of_requested_view < scroll_position then
            scroll_position <- top_of_requested_view

        let bottom_of_requested_view =
            min (lines.Count - 1 + this.LinesBelow) (cursor + this.ScrollOff)

        if bottom_of_requested_view - this.Height + 1 > scroll_position then
            scroll_position <- bottom_of_requested_view - this.Height + 1

        let mutable index = scroll_position

        for i = 1 to this.Height do
            let line = if index < lines.Count then lines.[index] else ""
            sb.AppendLine(line.ClearRestOfLine()) |> ignore
            index <- index + 1

        Console.Write(sb.ToString())

        lines.Clear()
