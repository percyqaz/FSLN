namespace Fernglas

open System
open System.IO
open System.Diagnostics
open System.Runtime.CompilerServices

type private ShellExtensions =

    [<Extension>]
    static member private ApplySubstitutions(state: State, command: string) : string =
        let full_path =
            match state.Selected with
            | Some(File f)
            | Some(Folder f) -> Path.Combine(state.Directory, f)
            | None -> state.Directory

        command
            .Replace("$$", '\uFFFD'.ToString())
            .Replace(
                "$GITPATH",
                match state.GitStatus with
                | Some gs -> Path.GetRelativePath(gs.RootPath, state.Directory)
                | None -> ""
            )
            .Replace("$", full_path)
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
