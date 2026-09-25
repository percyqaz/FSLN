namespace Fernglas

open System

exception BufferTooLongException of string

type CommandBuffer() =

    [<Literal>]
    static let ARBITRARY_BUFFER_LIMIT = 4096 // For when binds create an infinite loop of expansion

    let mutable buffer = ""

    override this.ToString() : string = buffer

    member this.AddKey(input: ConsoleKeyInfo) : unit =

        let inline is_displayable () : bool =
            input.KeyChar <> '\u0000' && Char.IsAscii(input.KeyChar) && not(Char.IsWhiteSpace(input.KeyChar))

        let inline format_displayable_input () : string =
            if input.Modifiers &&& ConsoleModifiers.Alt = ConsoleModifiers.Alt then
                Keymap.SpecialKey(sprintf "A-%c" input.KeyChar)
            else
                input.KeyChar.ToString()

        let inline format_special_key () : string =
            let key =
                match input.Key with
                | ConsoleKey.LeftArrow -> "Left"
                | ConsoleKey.RightArrow -> "Right"
                | ConsoleKey.UpArrow -> "Up"
                | ConsoleKey.DownArrow -> "Down"
                | otherwise -> otherwise.ToString()

            let alt =
                if input.Modifiers &&& ConsoleModifiers.Alt = ConsoleModifiers.Alt then "A-" else ""

            Keymap.SpecialKey(sprintf "%s%s" alt key)

        let inline delete_last_keystroke () : unit =
            if buffer.EndsWith(Keymap.GT) then
                let p = buffer.LastIndexOf(Keymap.LT)

                if p >= 0 then buffer <- buffer.Substring(0, p) else buffer <- buffer.Substring(0, buffer.Length - 1)
            elif buffer <> "" then
                buffer <- buffer.Substring(0, buffer.Length - 1)

        if input.Key = ConsoleKey.Backspace then
            delete_last_keystroke()
        elif input.Modifiers &&& ConsoleModifiers.Control = ConsoleModifiers.Control then
            ()
        elif input.Key = ConsoleKey.Escape then
            buffer <- buffer + Keymap.ESC
        elif input.Key = ConsoleKey.Spacebar then
            buffer <- buffer + " "
        elif is_displayable() then
            buffer <- buffer + format_displayable_input()
        elif input.Key <> ConsoleKey.None then
            buffer <- buffer + format_special_key()

    member this.Dispatch(handle_message: string -> unit, keymap: Keymap) : unit =

        let inline consume_keymap (keymap: Keymap) : bool =
            let mutable unfold_made = false

            for bind_source, bind_target in keymap do
                if buffer.StartsWith(bind_source) then
                    buffer <- bind_target + buffer.Substring(bind_source.Length)
                    unfold_made <- true

            unfold_made

        let inline handle_keymap_and_messages () : unit =
            while consume_keymap(keymap) && buffer.Length <= ARBITRARY_BUFFER_LIMIT do
                ()

            if buffer.EndsWith(Keymap.ESC) then
                buffer <- ""

            elif buffer.Contains(Keymap.ENTER) then
                let end_of_message = buffer.IndexOf(Keymap.ENTER)
                let command = buffer.Substring(0, end_of_message)
                buffer <- buffer.Substring(end_of_message + Keymap.ENTER.Length)
                handle_message(command)

        let mutable previous_buffer = buffer
        handle_keymap_and_messages()

        while previous_buffer <> buffer do
            if buffer.Length > ARBITRARY_BUFFER_LIMIT then
                raise(BufferTooLongException(buffer))
            else
                previous_buffer <- buffer
                handle_keymap_and_messages()

    member this.Append(lines: string seq) : unit =
        buffer <- String.concat Keymap.ENTER lines + Keymap.ENTER
