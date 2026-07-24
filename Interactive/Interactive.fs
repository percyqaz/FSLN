namespace FSLN

open System

module Interactive =

    let loop (config: string seq, solution: Solution) : unit =
        let rec state =
            InteractiveState.Create(solution, fun c -> Commands.dispatch_internal_command(state, c))

        Commands.register_default_binds(state)
        state.CommandBuffer.DispatchInitialCommands(config)

        let render = InteractiveDisplay(state)
        let input_thread = InputThread()
        input_thread.Start()

        Console.Write("\u001b[?1049h")

        while state.Running do
            render.Redraw()

            match input_thread.TryReadKey(2000) with
            | true, input ->
                state.CommandBuffer.AddKey(input)
                state.CommandBuffer.DispatchCommands()
            | false, _ ->
                state.GitStatus <- GitStatus.Fetch()

                if state.Solution.HasExternalChange() then
                    state.Solution <- SolutionLoader.read_solution_file(state.Solution.FullPath)
                    state.Selected <- Selection.Solution(state.Solution)

        Console.Write("\u001b[?1049l")

        input_thread.Dispose()
