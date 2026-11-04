namespace FSLN.Shared

open System
open System.IO

type ConfigFile =
    {
        Commands: ResizeArray<string>
    }

    static member FromPath(path: string, tag: string) : ConfigFile =
        let commands = ResizeArray()

        let lines =
            seq {
                if File.Exists(path) then
                    yield! File.ReadAllLines(path)
            }

        let mutable region_applies = true

        for line in lines do
            if line.StartsWith('@') then
                let tags =
                    line
                        .Substring(1)
                        .Trim()
                        .Split(' ', StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries)

                region_applies <- Array.contains tag tags
            elif line.Trim() <> "" && region_applies then
                commands.Add(line)

        { Commands = commands }

    static member FromUserFile(name: string, tag: string) : ConfigFile =
        let path =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), name)

        ConfigFile.FromPath(path, tag)
