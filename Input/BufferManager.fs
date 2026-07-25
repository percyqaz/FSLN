namespace FSLN

open System

[<RequireQualifiedAccess>]
type ActiveBuffer =
    | Command
    | Search
// todo: Custom that can be forwarded to commands

type BufferManager =
    {
        CommandBuffer: CommandBuffer
        SearchBuffer: TextBuffer
        mutable Active: ActiveBuffer
    }

    static member Create() : BufferManager =
        { CommandBuffer = CommandBuffer(); SearchBuffer = TextBuffer(); Active = ActiveBuffer.Command }

    member this.AddKey(input: ConsoleKeyInfo) : unit =
        match this.Active with
        | ActiveBuffer.Command -> this.CommandBuffer.AddKey(input)
        | ActiveBuffer.Search ->
            if not(this.SearchBuffer.TryAddKey(input)) then
                this.Active <- ActiveBuffer.Command

    member this.StartSearch() : unit = this.Active <- ActiveBuffer.Search

    override this.ToString() : string =
        match this.Active with
        | ActiveBuffer.Command -> this.CommandBuffer.ToString()
        | ActiveBuffer.Search -> "SEARCH: " + this.SearchBuffer.ToString()
