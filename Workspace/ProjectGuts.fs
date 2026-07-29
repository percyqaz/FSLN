namespace FSLN

type ProjectGuts =
    | FileSystem of FileSystemProject
    | FSharp of FSharpProject

    member this.BaseDirectory = this.BaseDirectory

    member this.Save() : unit =
        match this with
        | FSharp fs -> fs.RootElement.Save()
        | FileSystem fs -> fs.Ordering.Save()
