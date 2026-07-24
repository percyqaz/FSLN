namespace FSLN

open System

exception BufferTooLongException of string

[<AbstractClass>]
type CommandBuffer() =

    [<Literal>]
    let LT_LOOKALIKE = "＜"

    [<Literal>]
    let GT_LOOKALIKE = "＞"

    [<Literal>]
    let ARBITRARY_BUFFER_LIMIT = 4096 // For when binds create an infinite loop of expansion

    let special (s: string) = LT_LOOKALIKE + s + GT_LOOKALIKE

    let ENTER = special("Enter")
    let ESC = special("Esc")

    let mutable buffer = ""
    let mutable keymap: ResizeArray<string * string> = ResizeArray()

    override this.ToString() : string = buffer

    member this.Bind(string: string, target: string) : unit =
        let inline special (s: string) = s.Replace("<", "＜").Replace(">", "＞")
        keymap.Insert(0, (special string, special target))

    abstract member DispatchCommand: string -> unit

    member this.AddKey(input: ConsoleKeyInfo) : unit =

        let inline is_displayable () : bool =
            input.KeyChar <> '\u0000' && Char.IsAscii(input.KeyChar) && not(Char.IsWhiteSpace(input.KeyChar))

        let inline format_displayable_input () : string =
            if input.Modifiers &&& ConsoleModifiers.Alt = ConsoleModifiers.Alt then
                special(sprintf "A-%c" input.KeyChar)
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

            special(sprintf "%s%s" alt key)

        let inline delete_last_keystroke () : unit =
            if buffer.EndsWith(GT_LOOKALIKE) then
                let p = buffer.LastIndexOf(LT_LOOKALIKE)

                if p >= 0 then buffer <- buffer.Substring(0, p) else buffer <- buffer.Substring(0, buffer.Length - 1)
            elif buffer <> "" then
                buffer <- buffer.Substring(0, buffer.Length - 1)

        if input.Key = ConsoleKey.Backspace then
            delete_last_keystroke()
        elif input.Modifiers &&& ConsoleModifiers.Control = ConsoleModifiers.Control then
            ()
        elif input.Key = ConsoleKey.Escape then
            buffer <- buffer + ESC
        elif input.Key = ConsoleKey.Spacebar then
            buffer <- buffer + " "
        elif is_displayable() then
            buffer <- buffer + format_displayable_input()
        elif input.Key <> ConsoleKey.None then
            buffer <- buffer + format_special_key()

    member this.DispatchCommands() : unit =

        let inline consume_buffer (shorthand: string, target: string) : unit =
            if buffer.StartsWith(shorthand) then
                buffer <- target + buffer.Substring(shorthand.Length)

        let inline handle_keymap_and_commands () : unit =
            for bind_source, bind_target in keymap do
                consume_buffer(bind_source, bind_target)

            if buffer.EndsWith(ESC) then
                buffer <- ""

            elif buffer.StartsWith(":") && buffer.Contains(ENTER) then
                let end_of_command = buffer.IndexOf(ENTER)
                let command = buffer.Substring(1, end_of_command - 1)
                this.DispatchCommand(command)
                buffer <- buffer.Substring(end_of_command + ENTER.Length)

        let mutable previous_buffer = buffer
        handle_keymap_and_commands()

        while previous_buffer <> buffer do
            if buffer.Length > ARBITRARY_BUFFER_LIMIT then
                raise(BufferTooLongException(buffer))
            else
                previous_buffer <- buffer
                handle_keymap_and_commands()

    member this.DispatchInitialCommands(config: string seq) : unit =
        buffer <- String.concat ENTER config + ENTER
        this.DispatchCommands()
