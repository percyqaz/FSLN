namespace FSLN

open System
open System.IO

type Solution =
    {
        Name: string
        FullPath: string
        Ordering: OrderFile
        Projects: ResizeArray<Project>
        mutable LastSeenUtc: int64
    }

    member this.HasExternalChange() : bool =
        let last_write =
            DateTimeOffset(File.GetLastWriteTimeUtc(this.FullPath)).ToUnixTimeSeconds()

        last_write > this.LastSeenUtc || this.Projects |> Seq.exists _.HasExternalChange()
