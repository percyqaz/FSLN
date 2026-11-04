namespace FSLN.Controller

open System
open System.Collections.Concurrent
open System.IO
open System.Text
open System.Threading.Tasks
open FSLN.Controller

type State =
    private
        {
            mutable Running: bool
            Config: Config
            Stack: ResizeArray<ViewConfig>
            ProcessTracker: ProcessTracker
            CommandQueue: ConcurrentQueue<PipeCommand>
            mutable CurrentView: View | null
            mutable StatusLine: string
        }

    member private this.CreateViewAsync(config: ViewConfig) : Task<View> =
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

    member private this.StartNextViewAsync() : Task =
        let destroy_current_view () =
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
                let! view = this.CreateViewAsync(config)
                do! destroy_current_view()
                this.CurrentView <- view
                this.Render()
        }

    member this.OpenViewAsync(config: ViewConfig) : Task =
        task {
            this.Stack.Add(config)
            do! this.StartNextViewAsync()
        }

    member this.HandleCloseAsync(depth: int) : Task =
        task {
            for _ = 1 to depth do
                this.Stack.RemoveAt(this.Stack.Count - 1)
                do! this.StartNextViewAsync()

            if depth = 0 then
                do! this.StartNextViewAsync()

            this.Render()
        }

    member this.Render() : unit =
        lock
            this
            (fun () ->
                if this.Stack.Count > 0 then
                    Console.Write(this.CurrentView.Render(StringBuilder(), this.StatusLine).ToString())
            )

    member this.RunAsync(starting_view: string) : Task =

        let main_loop () =
            task {
                let current_view = this.CurrentView
                current_view.Left.UpdateState()
                current_view.Right |> Option.iter(_.UpdateState())

                if Console.KeyAvailable then
                    let key = Console.ReadKey(true)

                    match key.KeyChar with
                    | 'K' ->
                        match this.Config.ResolveViewConfig(Some this.CurrentView.Config, "diff") with
                        | Ok config -> do! this.OpenViewAsync(config)
                        | Error reason ->
                            this.StatusLine <- reason
                            this.Render()
                    | 'h' when key.Modifiers &&& ConsoleModifiers.Alt <> ConsoleModifiers.None ->
                        current_view.FocusedPane <- LeftPane
                        this.Render()
                    | 'l' when key.Modifiers &&& ConsoleModifiers.Alt <> ConsoleModifiers.None ->
                        current_view.FocusedPane <- if current_view.Right.IsSome then RightPane else LeftPane
                        this.Render()
                    | _ -> current_view.Focused.SendKey(key)

                if current_view.Left.State.IsExited then
                    do! this.HandleCloseAsync(snd current_view.Config.Left)
                else
                    match current_view.Right with
                    | Some pane when pane.State.IsExited ->
                        do! this.HandleCloseAsync(snd current_view.Config.Right.Value)
                    | _ -> ()

                if current_view.AutoResize() then
                    this.Render()

                while not(this.CommandQueue.IsEmpty) do
                    match this.CommandQueue.TryDequeue() with
                    | true, cmd -> do! this.DispatchCommandAsync(cmd)
                    | false, _ -> ()
            }

        task {
            match this.Config.ResolveViewConfig(None, starting_view) with
            | Ok config -> do! this.OpenViewAsync(config)
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
                    this.HandleCloseAsync(this.Stack.Count).GetAwaiter().GetResult()
            finally
                cancel_handler.Dispose()
                Console.Write("\u001b[0m\u001b[?25h\u001b[?1049l")
                Console.CursorVisible <- old_cursor
        }

    member this.DispatchCommandAsync(command: PipeCommand) : Task =
        task {
            this.StatusLine <- sprintf "%i: %s" command.SourcePID command.Command
            this.Render()
        }

    member this.QueueCommand(command: PipeCommand) : unit = this.CommandQueue.Enqueue(command)

    static member TryCreate() : Result<State, string> =

        let config =
            try
                let config = Config()

                for line in FSLN.Shared.ConfigFile.FromUserFile(".fsln", "views").Commands do
                    match config.ParseConfigLine(line) with
                    | Ok() -> ()
                    | Error reason -> failwithf "Error: %s\nAt line: %s" reason line

                Ok config
            with error ->
                Error $"Could not read controller configuration: {error.Message}"

        match config with
        | Error reason -> Error reason
        | Ok config ->
            Ok
                {
                    Running = true
                    Config = config
                    Stack = ResizeArray()
                    ProcessTracker = ProcessTracker.Create()
                    CommandQueue = ConcurrentQueue()
                    CurrentView = null
                    StatusLine = ""
                }

    interface IState with
        override this.Rerender() : unit = this.Render()

        override this.IsVisible(proc: Process) : bool =
            if this.Stack.Count > 0 && not(isNull(this.CurrentView)) then
                let v = this.CurrentView
                v.Left = proc || v.Right = Some proc
            else
                false
