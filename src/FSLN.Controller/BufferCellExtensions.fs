namespace FSLN.Controller

open System
open System.Runtime.CompilerServices

type BufferCellExtensions =

    [<Extension>]
    static member Style(cell: XTerm.Buffer.BufferCell) : string =

        let color_sequence (mode: int) (color: int) (foreground: bool) =
            let code = if foreground then "38" else "48"

            match mode with
            | 0 when color >= 256 -> if foreground then "39" else "49"
            | 0 -> $"{code};5;{color}"
            | _ ->
                let red = (color >>> 16) &&& 255
                let green = (color >>> 8) &&& 255
                let blue = color &&& 255
                $"{code};2;{red};{green};{blue}"

        let attributes = cell.Attributes
        let codes = ResizeArray<string>()
        codes.Add("0")
        codes.Add(color_sequence (attributes.GetFgColorMode()) (attributes.GetFgColor()) true)
        codes.Add(color_sequence (attributes.GetBgColorMode()) (attributes.GetBgColor()) false)

        if attributes.IsBold() then
            codes.Add("1")

        if attributes.IsDim() then
            codes.Add("2")

        if attributes.IsItalic() then
            codes.Add("3")

        if attributes.IsUnderline() then
            codes.Add("4")

        if attributes.IsBlink() then
            codes.Add("5")

        if attributes.IsInverse() then
            codes.Add("7")

        if attributes.IsInvisible() then
            codes.Add("8")

        if attributes.IsStrikethrough() then
            codes.Add("9")

        if attributes.IsOverline() then
            codes.Add("53")

        String.Join(";", codes)