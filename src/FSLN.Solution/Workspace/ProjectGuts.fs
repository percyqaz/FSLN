namespace FSLN

type ProjectGuts =
    | FileSystem of FileSystemProject
    | FSharp of FSharpProject

    member this.BaseDirectory = this.BaseDirectory

    member this.ProjectFilePath: string =
        match this with
        | FileSystem fs -> fs.ProjectFilePath
        | FSharp fs -> fs.ProjectFilePath

    member this.Save() : unit =
        match this with
        | FSharp fs -> fs.RootElement.Save()
        | FileSystem fs -> fs.Ordering.Save()
