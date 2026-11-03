namespace FSLN.Controller

open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open FSLN.Controller.Config
open FSLN.Controller

type ProcessTrackerEntry = { mutable Process: Process; mutable RefCount: int }

type ProcessTracker =
    private
        {
            Processes: Dictionary<string, ProcessTrackerEntry>
            Gate: SemaphoreSlim
            Loops: ResizeArray<Task * string>
            mutable HasShutdown: bool
        }

    static member Create() : ProcessTracker =
        {
            Processes = Dictionary()
            Gate = new SemaphoreSlim(1, 1)
            Loops = ResizeArray()
            HasShutdown = false
        }

    member this.Acquire(config: ProcessConfig, state: IState) : Task<Process> =
        let create_proc () =
            task {
                let! process_instance = Process.StartAsync(config)
                this.Loops.Add(process_instance.StartRenderLoop(state), config.Command)
                return process_instance
            }

        task {
            do! this.Gate.WaitAsync()

            try
                match this.Processes.TryGetValue(config.Command) with
                | true, entry ->
                    if entry.Process.HasExitSignal then
                        entry.Process.Destroy()
                        let! new_process = create_proc()
                        entry.Process <- new_process

                    entry.RefCount <- entry.RefCount + 1
                    return entry.Process
                | false, _ ->
                    let! new_process = create_proc()
                    this.Processes.[config.Command] <- { Process = new_process; RefCount = 1 }
                    return new_process
            finally
                this.Gate.Release() |> ignore
        }

    member this.Release(proc: Process) : Task =
        task {
            do! this.Gate.WaitAsync()

            try
                match this.Processes.TryGetValue(proc.Config.Command) with
                | true, entry ->
                    entry.RefCount <- entry.RefCount - 1

                    if entry.RefCount = 0 then
                        entry.Process.Destroy()
                        this.Processes.Remove(proc.Config.Command) |> ignore
                | false, _ -> () // Can be double-called in execeptional cases but not a clean exit
            finally
                this.Gate.Release() |> ignore
        }

    member this.WaitForExit() : Task =
        task {
            try
                do! Task.WhenAll(this.Loops |> Seq.map fst |> Array.ofSeq)
            with _ ->
                ()
        }
