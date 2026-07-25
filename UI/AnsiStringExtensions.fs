namespace FSLN

open System.Drawing
open System.Runtime.CompilerServices

type AnsiStringExtensions =

    [<Extension>]
    static member ForeColor(text: string, foreground: Color) : string =
        sprintf "\u001b[38;2;%d;%d;%dm%s\u001b[39m" foreground.R foreground.G foreground.B text

    [<Extension>]
    static member ForeColor(text: string, foreground: int) : string =
        text.ForeColor(Color.FromArgb(foreground))

    [<Extension>]
    static member BackColor(text: string, background: Color) : string =
        sprintf "\u001b[48;2;%d;%d;%dm%s\u001b[49m" background.R background.G background.B text

    [<Extension>]
    static member BackColor(text: string, background: int) : string =
        text.BackColor(Color.FromArgb(background))

    [<Extension>]
    static member Bold(text: string) : string = sprintf "\u001b[1m%s\u001b[22m" text

    [<Extension>]
    static member ClearRestOfLine(text: string) : string = sprintf "%s\u001b[K" text
