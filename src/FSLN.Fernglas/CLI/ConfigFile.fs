namespace Fernglas

open System
open System.Collections.Generic
open System.IO

type ConfigFile =
    {
        Directives: Dictionary<string, string>
        Commands: ResizeArray<string>
    }

    member this.LoadFile(path: string) : unit =
        let lines =
            seq {
                if File.Exists(path) then
                    yield! File.ReadAllLines(path)
            }

        for line in lines do
            if line.StartsWith('#') then
                let directive = line.Substring(1).Split('=', 2, StringSplitOptions.TrimEntries)
                this.Directives.Add(directive.[0].ToLower(), if directive.Length < 2 then "" else directive.[1])
            elif line.Trim() <> "" then
                this.Commands.Add(line)

    member this.LoadUserFile(name: string) : unit =
        let path =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), name)

        this.LoadFile(path)

    static member Create() : ConfigFile =
        { Directives = Dictionary<_, _>(); Commands = ResizeArray<_>() }

    static member FromFile(path: string) : ConfigFile =
        let file = ConfigFile.Create()
        file.LoadFile(path)
        file

    static member FromUserFile(name: string) : ConfigFile =
        let file = ConfigFile.Create()
        file.LoadUserFile(name)
        file
