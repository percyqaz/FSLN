open System
open System.IO
open FSLN.Shared
open FSLN.Solution

let get_fsln_config () : string seq =
    let global_config = ConfigFile.FromUserFile(".fsln", "solution")
    
    match Path.walk_tree_specific_file(".fsln") with
    | Some local_config_path ->
        let local_config = ConfigFile.FromPath(local_config_path, "solution")
        seq { yield! global_config.Commands; yield! local_config.Commands }
    | None ->
        global_config.Commands

let entry_point (config: string seq, workspace: Workspace) : unit =
    let state = State.Create(workspace)
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
        | false, _ ->
            state.ReloadGit()
            state.AutoReload()

    Console.Write(AnsiCodes.LeaveSecondScreen)

[<EntryPoint>]
let main (argv: string array) : int =
    let init = argv.Length > 0 && argv.[0].ToLower() = "init"

    match Workspace.TryDetect() with
    | Some _ when init ->
        printfn "Cannot init, a solution or workspace already exists here!"
        1
    | Some workspace ->
        Directory.SetCurrentDirectory(workspace.RootPath)
        entry_point(get_fsln_config(), workspace)
        0
    | None when init ->
        Workspace.Init()
        printfn "Initialised a workspace here!"
        0
    | None ->
        printfn "No solution or workspace detected here!"
        1
