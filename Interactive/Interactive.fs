namespace FSLN

module Interactive =

    let loop (config: string seq, solution: Solution) : unit =
        let state = InteractiveState.Create(solution)
        Commands.register_default_binds(state)

        state.Buffer <- String.concat InputBuffer.ENTER config + InputBuffer.ENTER
        InputBuffer.dispatch_keybindings(state)

        let render = InteractiveDisplay(state)

        System.Console.Write("\u001b[?1049h")

        while state.Running do
            render.Redraw()
            InputBuffer.key_to_buffer(state)
            InputBuffer.dispatch_keybindings(state)

        System.Console.Write("\u001b[?1049l")
