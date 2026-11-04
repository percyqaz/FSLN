namespace FSLN.Controller

open System
open System.Text
open FSLN.Controller
open XTerm

[<Struct>]
type TerminalDimensions =
    {
        Width: int
        Height: int
        LeftWidth: int
        RightWidth: int
    }

    member this.PanelHeight = this.Height - 1

type PaneFocus =
    | LeftPane
    | RightPane

type View =
    {
        Config: ViewConfig
        mutable Left: Process
        mutable Right: Process option
        mutable FocusedPane: PaneFocus
        mutable Height: int
        mutable Width: int
    }

    member this.Focused: Process =
        match this.FocusedPane, this.Right with
        | RightPane, Some right -> right
        | _ -> this.Left

    member this.GetDimensions() : TerminalDimensions =
        let width = max 2 Console.WindowWidth
        let height = max 2 Console.WindowHeight
        let left_width = if this.Right.IsSome then max 1 (width / 2) else width
        let right_width = if this.Right.IsSome then max 1 (width - left_width) else 0

        {
            Width = width
            Height = height
            LeftWidth = left_width
            RightWidth = right_width
        }

    member this.AutoResize() : bool =
        let dimensions = this.GetDimensions()

        if dimensions.Width <> this.Width || dimensions.Height <> this.Height then
            this.Width <- dimensions.Width
            this.Height <- dimensions.Height
            this.Left.Resize(dimensions.LeftWidth, dimensions.PanelHeight)
            this.Right |> Option.iter(_.Resize(dimensions.RightWidth, dimensions.PanelHeight))
            true
        else
            false

    member this.Render(builder: StringBuilder, status_line: string) : StringBuilder =

        let append_terminal (builder: StringBuilder) (terminal: Terminal) (left: int) =
            let buffer = terminal.Buffer
            let mutable previous_style = ""

            for row = 0 to terminal.Rows - 1 do
                let line_index = buffer.YDisp + row
                let line = buffer.Lines.[line_index]
                builder.Append($"\u001b[{row + 1};{left + 1}H") |> ignore

                if isNull line then
                    builder.Append(' ', terminal.Cols) |> ignore
                else
                    for column = 0 to terminal.Cols - 1 do
                        let cell = line.[column]

                        if cell.Width <> 0 then
                            let style = cell.Style()

                            if style <> previous_style then
                                builder.Append("\u001b[").Append(style).Append('m') |> ignore
                                previous_style <- style

                            builder.Append(cell.Content) |> ignore

            builder.Append("\u001b[0m") |> ignore

        builder.Append("\u001b[0m\u001b[?25l") |> ignore
        let left_cols = this.Left.Terminal.Cols

        append_terminal builder this.Left.Terminal 0
        this.Right |> Option.iter(fun right_panel -> append_terminal builder right_panel.Terminal left_cols)

        builder.Append($"\u001b[{this.Height};1H\u001b[0m") |> ignore
        builder.Append(' ', this.Width) |> ignore

        let visible_status =
            if status_line.Length > this.Width then status_line.Substring(0, this.Width) else status_line

        builder.Append($"\u001b[{this.Height};1H\u001b[2m").Append(visible_status).Append("\u001b[0m") |> ignore

        if this.Focused = this.Left && this.Left.Terminal.CursorVisible then
            let buffer = this.Left.Terminal.Buffer
            builder.Append($"\u001b[{buffer.Y + 1};{buffer.X + 1}H\u001b[?25h") |> ignore

        elif Some this.Focused = this.Right && this.Right.Value.Terminal.CursorVisible then
            let buffer = this.Right.Value.Terminal.Buffer
            let left_cols = this.Left.Terminal.Cols
            builder.Append($"\u001b[{buffer.Y + 1};{left_cols + buffer.X + 1}H\u001b[?25h") |> ignore

        builder
