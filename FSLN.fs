namespace FSLN

open System

module FSLN =

    let loop (config: string seq, solution: Solution) : unit =
        let state = State.Create(solution)
        let input_thread = InputThread()
        let command_dispatcher = CommandDispatcher(state, input_thread)

        command_dispatcher.DispatchInitialCommandsOnState(config)

        let render = Display(state)
        input_thread.Start()

        Console.Write("\u001b[?1049h")

        while state.Running do
            render.Redraw()

            match input_thread.TryReadKey(2000) with
            | true, input ->
                state.Buffers.AddKey(input)
                command_dispatcher.DispatchCommandsOnState()
            | false, _ ->
                state.ReloadGit()
                state.AutoReload()

        Console.Write("\u001b[?1049l")

        input_thread.Dispose()
