namespace FSLN

type Editors =
    private
        {
            mutable Fallback: string
            mutable FileTypes: Map<string, string>
        }

    static member Default: Editors =
        { Fallback = "echo 'No editor set' && echo $ && false"; FileTypes = Map.empty }

    member this.Get(ext: string) : string =
        match this.FileTypes.TryFind(ext) with
        | Some editor -> editor
        | None -> this.Fallback

    member this.Set(ext: string, editor: string) : Result<unit, string> =
        if ext.StartsWith('.') || ext = "/" then
            this.FileTypes <- this.FileTypes.Add(ext.ToLower(), editor)
            Ok()
        elif ext.ToLower().StartsWith('d') then
            this.Fallback <- editor
            Ok()
        else
            Error "Invalid file type"
