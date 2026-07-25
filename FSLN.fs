namespace FSLN

open System

module FSLN =

    let loop (config: string seq, solution: Solution) : unit =
        let state = State.Create(solution)
        let input_thread = InputThread()
        let command_dispatcher = CommandDispatcher(state, input_thread)

        CommandDispatcher.RegisterDefaultBinds(state)
        state.CommandBuffer.DispatchInitialCommands(config, command_dispatcher.DispatchCommand)

        let render = Display(state)
        input_thread.Start()

        Console.Write("\u001b[?1049h")

        while state.Running do
            render.Redraw()

            match input_thread.TryReadKey(2000) with
            | true, input ->
                state.CommandBuffer.AddKey(input)
                state.CommandBuffer.DispatchCommands(command_dispatcher.DispatchCommand)
            | false, _ ->
                state.RefreshGit()

                if state.Solution.HasExternalChange() then
                    state.Reload()

        Console.Write("\u001b[?1049l")

        input_thread.Dispose()
