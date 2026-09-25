namespace Fernglas

open System

module Fernglas =

    let create_default_keymap () : Keymap =
        let keymap = Keymap()
        let bind key command = keymap.AliasCommand(key, command)
        let alias key other_key = keymap.Alias(key, other_key)

        bind "h" "left"
        bind "j" "down"
        bind "k" "up"
        bind "l" "right"
        bind Keymap.ESC "close"
        bind Keymap.ENTER "open"
        alias "r" ":rename "
        alias "+" ":add "
        alias "=" "+"
        bind "dd" "delete"
        bind (Keymap.SpecialKey("Tab")) "search"
        bind (Keymap.SpecialKey("A-h")) "move_left"
        bind (Keymap.SpecialKey("A-j")) "descend"
        bind (Keymap.SpecialKey("A-k")) "ascend"
        bind (Keymap.SpecialKey("A-l")) "move_right"

        alias (Keymap.SpecialKey("Down")) "j"
        alias (Keymap.SpecialKey("Up")) "k"

        keymap

    let loop (working_directory: string, config: string seq) : unit =
        let state = State.Create(working_directory, create_default_keymap())
        let render = View(state)

        state.CommandBuffer.Append(config)
        state.CommandBuffer.Dispatch(state.DispatchMessage, state.Keymap)

        Console.Write(AnsiCodes.EnterSecondScreen)
        let input_thread = InputThread()

        while state.Running do
            render.Redraw()

            match input_thread.TryReadKey(2000) with
            | true, input ->
                state.AddKey(input)
                state.CommandBuffer.Dispatch(state.DispatchMessage, state.Keymap)
            | false, _ -> state.Refresh()

        Console.Write(AnsiCodes.LeaveSecondScreen + AnsiCodes.CursorVisible)
