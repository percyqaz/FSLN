namespace Fernglas

open System

module Fernglas =

    let loop (working_directory: string, config: string seq) : unit =
        let state = State.Create(working_directory, Keymap())
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
