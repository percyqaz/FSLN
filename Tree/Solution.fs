namespace FSLN

open System
open System.IO
open Microsoft.Build.Construction

type Solution =
    {
        Name: string
        FullPath: string
        SolutionFile: SolutionFile
        Projects: ResizeArray<Project>
        mutable LastSeenUtc: int64
    }

    member this.HasExternalChange() : bool =
        let last_write =
            DateTimeOffset(File.GetLastWriteTimeUtc(this.FullPath)).ToUnixTimeSeconds()

        last_write > this.LastSeenUtc || this.Projects |> Seq.exists _.HasExternalChange()
