namespace Fernglas

open System

type State =
    {
        mutable Running: bool
        MainPane: Pane
        CommandBuffer: CommandBuffer
        mutable SearchBufferFocused: bool
        mutable StatusLine: string
        Keymap: Keymap
    }

    member this.ActivePane: Pane = this.MainPane

    static member Create(path: string, keymap: Keymap) : State =
        {
            Running = true
            MainPane = Pane.Create(path, true)
            CommandBuffer = CommandBuffer()
            SearchBufferFocused = false
            StatusLine = ""
            Keymap = keymap
        }

    member this.Refresh() : unit = this.MainPane.Refresh()
    // option.iter this.SidePane _.REfresh()

    member this.AddKey(input: ConsoleKeyInfo) : unit =
        if this.SearchBufferFocused then
            if this.MainPane.SearchBuffer.TryAddKey(input) then
                this.MainPane.UpdateSearchResults(this.MainPane.Selected)
            else
                this.SearchBufferFocused <- false
        else
            this.CommandBuffer.AddKey(input)
