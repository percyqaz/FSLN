namespace FSLN

open System
open FSLN.Shared

module FSLN =

    let loop (config: string seq, workspace: Workspace) : unit =
        let state = State.Create(workspace)
        let render = Display(state)
        
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
            | false, _ ->
                state.ReloadGit()
                state.AutoReload()

        Console.Write(AnsiCodes.LeaveSecondScreen)
