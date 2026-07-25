namespace FSLN

open System.Drawing
open System.Runtime.CompilerServices

type AnsiCodes =

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
    
    static member CURSOR_TO_ORIGIN = "\u001b[H"
    static member CURSOR_INVISIBLE = "\u001b[?25l"
    static member CURSOR_VISIBLE = "\u001b[?25h"
    static member ENTER_SECOND_SCREEN = "\u001b[?1049h"
    static member LEAVE_SECOND_SCREEN = "\u001b[?1049l"
    static member CLEAR_SCREEN = "\u001b[2J"
    static member SAVE_SCREEN = "\u001b[?47h"
    static member RESTORE_SCREEN = "\u001b[?47l"
