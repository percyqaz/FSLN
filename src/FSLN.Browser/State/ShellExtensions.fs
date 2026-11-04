namespace FSLN

open System
open System.IO
open System.Diagnostics
open System.Runtime.CompilerServices
open FSLN.Shared

type private ShellExtensions =

    [<Extension>]
    static member ApplySubstitutions(state: State, command: string) : string =
        command
            .Replace("$$", '\uFFFD'.ToString())
            .Replace("$SOLUTION", state.Mode.Solution.FullPath)
            .Replace(
                "$GITPATH",
                match state.GitStatus with
                | Some gs -> Path.GetRelativePath(gs.RootPath, state.Mode.Selection.FullPath)
                | None -> ""
            )
            .Replace(
                "$PROJECT",
                match state.Mode.Selection.ParentProject with
                | Some project -> project.FullPath
                | None -> ""
            )
            .Replace("$", state.Mode.Selection.FullPath)
            .Replace('\uFFFD', '$')

    [<Extension>]
    static member DispatchShell(state: State, command: string) : unit =
        let shell, args =
            if OperatingSystem.IsWindows() then
                "cmd.exe", "/c " + state.ApplySubstitutions(command)
            else
                "/bin/sh", "-c \"" + state.ApplySubstitutions(command) + "\""

        let start_info = ProcessStartInfo(shell, args)

        Console.Write(
            AnsiCodes.LeaveSecondScreen + AnsiCodes.SaveScreen + AnsiCodes.ClearScreen + AnsiCodes.CursorToOrigin
        )

        let proc = Process.Start(start_info)

        let ctrl_c_handler =
            Console.CancelKeyPress.Subscribe(fun ev ->
                if not(command.StartsWith("vim")) then
                    ev.Cancel <- true
                    proc.Kill()
                    Console.WriteLine("Process force-killed!".ForeColor(0xFF8888))
            )

        proc.WaitForExit()
        ctrl_c_handler.Dispose()

        if proc.ExitCode <> 0 then
            Console.ReadKey(true) |> ignore
            state.StatusLine <- sprintf "(%i)" proc.ExitCode

        elif Console.GetCursorPosition() <> struct (0, 0) then
            Console.WriteLine("Press any key to return".ForeColor(0x666666))
            Console.ReadKey(true) |> ignore

        Console.Write(AnsiCodes.RestoreScreen + AnsiCodes.EnterSecondScreen)
