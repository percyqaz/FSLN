open System
open System.IO
open FSLN.Shared
open FSLN

let get_fsln_config () : string seq =
    let user_profile_settings =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fsln")

    let local_directory_settings = Path.walk_tree_specific_file(".fsln")

    seq {
        if File.Exists(user_profile_settings) then
            yield! File.ReadAllLines(user_profile_settings)

        match local_directory_settings with
        | Some file when file <> user_profile_settings && File.Exists(file) -> yield! File.ReadAllLines(file)
        | _ -> ()
    }
    |> Seq.filter(String.IsNullOrWhiteSpace >> not)
    
let entry_point (config: string seq, workspace: Workspace) : unit =
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
