namespace FSLN

type ProjectGuts =
    | FileSystem of FileSystemProject
    | FSharp of FSharpProject

    member this.BaseDirectory = this.BaseDirectory

    member this.Save() : unit =
        match this with
        | FSharp d -> d.RootElement.Save()
        | FileSystem _ -> ()
