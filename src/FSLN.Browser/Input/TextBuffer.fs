namespace FSLN

open System

type TextBuffer() =

    let mutable buffer = ""
    override this.ToString() : string = buffer

    member this.TryAddKey(input: ConsoleKeyInfo) : bool =

        let inline is_displayable () : bool =
            input.KeyChar <> '\u0000'
            && input.Modifiers &&& ConsoleModifiers.Alt <> ConsoleModifiers.Alt
            && Char.IsAscii(input.KeyChar)
            && not(Char.IsWhiteSpace(input.KeyChar))

        let inline delete_last_key () : unit =
            if buffer <> "" then
                buffer <- buffer.Substring(0, buffer.Length - 1)

        if input.Key = ConsoleKey.Backspace then
            delete_last_key()
            true
        elif input.Modifiers &&& ConsoleModifiers.Control = ConsoleModifiers.Control then
            false
        elif input.Key = ConsoleKey.Escape then
            false
        elif input.Key = ConsoleKey.Spacebar then
            buffer <- buffer + " "
            true
        elif is_displayable() then
            buffer <- buffer + input.KeyChar.ToString()
            true
        else
            false
