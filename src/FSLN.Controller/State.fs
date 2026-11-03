namespace FSLN.Controller

open System
open System.IO
open System.Text
open System.Threading.Tasks
open FSLN.Controller.Config
open FSLN.Controller

type State =
    {
        mutable Running: bool
        Config: Config
        Stack: ResizeArray<ViewConfig>
        ProcessTracker: ProcessTracker
        mutable CurrentView: View | null
        mutable StatusLine: string
    }

    static member TryCreate() : Result<State, string> =
        
        let find_controller_config_path () =
            let rec search_from (directory: DirectoryInfo) =
                let config_path = Path.Combine(directory.FullName, ".fsln-controller")

                if File.Exists(config_path) then
                    Some config_path
                else
                    match directory.Parent with
                    | null -> None
                    | parent -> search_from parent

            search_from(DirectoryInfo(Environment.CurrentDirectory))

        let config =
            let config = Config()
            
            match find_controller_config_path() with
            | None -> Error "Could not find `.fsln-controller` in the current directory or any parent directory."
            | Some config_path ->
                try
                    for i, line in File.ReadAllLines(config_path) |> Seq.indexed do
                        match config.ParseConfigLine(line) with
                        | Ok() -> ()
                        | Error reason -> failwithf "Line %i: %s" i reason
                        
                    Ok config
                with error ->
                    Error $"Could not read controller configuration `{config_path}`: {error.Message}"

        match config with
        | Error reason -> Error reason
        | Ok config ->
            Ok
                {
                    Running = true
                    Config = config
                    Stack = ResizeArray()
                    ProcessTracker = ProcessTracker.Create()
                    CurrentView = null
                    StatusLine = ""
                }

    member private this.CreateView(config: ViewConfig) : Task<View> =
        task {
            let! left = this.ProcessTracker.Acquire(fst config.Left, this)

            let! right =
                task {
                    match config.Right with
                    | None -> return None
                    | Some right ->
                        let! pane = this.ProcessTracker.Acquire(fst right, this)
                        return Some pane
                }

            let view =
                {
                    Config = config
                    Left = left
                    Right = right
                    FocusedPane = LeftPane
                    Height = 0
                    Width = 0
                }

            view.AutoResize() |> ignore
            return view
        }

    member private this.StartNextView() : Task =
        let destroy_current_view() =
            task {
                let closing_view = this.CurrentView
                if not(isNull(closing_view)) then 
                    do! this.ProcessTracker.Release(closing_view.Left)
                    
                    match closing_view.Right with
                    | Some proc -> do! this.ProcessTracker.Release(proc)
                    | None -> ()
            }
        
        task {
            if this.Stack.Count = 0 then
                this.Running <- false
                do! destroy_current_view()
            else
                let config = this.Stack.[this.Stack.Count - 1]
                let! view = this.CreateView(config)
                do! destroy_current_view()
                this.CurrentView <- view
                this.Render()
        }

    member this.OpenView(config: ViewConfig) : Task =
        task {
            this.Stack.Add(config)
            do! this.StartNextView()
        }
        
    member this.AfterClose(depth: int) : Task =
        task {
            for _ = 1 to depth do
                this.Stack.RemoveAt(this.Stack.Count - 1)
                do! this.StartNextView()
                
            if depth = 0 then
                do! this.StartNextView()
                
            this.Render()
        }

    member this.Render() : unit =
        lock
            this
            (fun () ->
                if this.Stack.Count > 0 then
                    Console.Write(this.CurrentView.Render(StringBuilder(), this.StatusLine).ToString())
            )

    member this.Run() : Task =

        let main_loop() =
            task {
                let current_view = this.CurrentView
                current_view.Left.UpdateState()
                current_view.Right |> Option.iter(_.UpdateState())

                if Console.KeyAvailable then
                    let key = Console.ReadKey(true)

                    match key.KeyChar with
                    | 'K' ->
                        match this.Config.ResolveViewConfig(Some this.CurrentView.Config, "diff") with
                        | Ok config -> do! this.OpenView(config)
                        | Error reason -> this.StatusLine <- reason; this.Render()
                    | 'h' when key.Modifiers &&& ConsoleModifiers.Alt <> ConsoleModifiers.None ->
                        current_view.FocusedPane <- LeftPane
                        this.Render()
                    | 'l' when key.Modifiers &&& ConsoleModifiers.Alt <> ConsoleModifiers.None ->
                        current_view.FocusedPane <- if current_view.Right.IsSome then RightPane else LeftPane
                        this.Render()
                    | _ -> current_view.Focused.SendKey(key)

                if current_view.Left.State.IsExited then
                    do! this.AfterClose(snd current_view.Config.Left)
                else
                    match current_view.Right with
                    | Some pane when pane.State.IsExited ->
                        do! this.AfterClose(snd current_view.Config.Right.Value)
                    | _ -> ()

                if current_view.AutoResize() then
                    this.Render()
            }

        task {
            match this.Config.ResolveViewConfig(None, "main") with
            | Ok config -> do! this.OpenView(config)
            | Error reason -> failwithf "No 'main' view defined: %s" reason
            
            let old_cursor = Console.CursorVisible
            Console.Write("\u001b[?1049h\u001b[2J\u001b[H\u001b[?25l")
            Console.CursorVisible <- false
            
            let cancel_handler =
                Console.CancelKeyPress.Subscribe(fun args ->
                    args.Cancel <- true
                    this.CurrentView.Focused.SendInterrupt()
                )

            try
                try
                    this.Render()

                    while this.Running do
                        do! main_loop()
                        do! Task.Delay(8)
                        
                    cancel_handler.Dispose()
                    do! this.ProcessTracker.WaitForExit()
                with _ ->
                    this.AfterClose(this.Stack.Count).GetAwaiter().GetResult()
            finally
                cancel_handler.Dispose()
                Console.Write("\u001b[0m\u001b[?25h\u001b[?1049l")
                Console.CursorVisible <- old_cursor
        }

    interface IState with
        override this.Rerender() : unit = this.Render()
        override this.IsVisible(proc: Process) : bool =
            if this.Stack.Count > 0 && not(isNull(this.CurrentView)) then
                let v = this.CurrentView
                v.Left = proc || v.Right = Some proc
            else false