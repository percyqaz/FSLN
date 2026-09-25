namespace Fernglas

open System

type State =
    {
        mutable Running: bool
        MainPane: Pane
        SidePane: Pane option
        CommandBuffer: CommandBuffer
        mutable SearchBufferFocused: bool
        mutable SidePaneFocused: bool
        mutable StatusLine: string
        Keymap: Keymap
    }

    member this.ActivePane: Pane =
        if this.SidePaneFocused then this.SidePane.Value else this.MainPane

    static member Create(path: string, keymap: Keymap) : State =
        {
            Running = true
            MainPane = Pane.Create(path, true)
            SidePane = None
            CommandBuffer = CommandBuffer()
            SearchBufferFocused = false
            SidePaneFocused = false
            StatusLine = ""
            Keymap = keymap
        }

    member this.Refresh() : unit =
        this.MainPane.Refresh()
        this.SidePane |> Option.iter _.Refresh()

    member this.AddKey(input: ConsoleKeyInfo) : unit =
        if this.SearchBufferFocused then
            if this.MainPane.SearchBuffer.TryAddKey(input) then
                this.MainPane.UpdateSearchResults(this.MainPane.Selected)
            else
                this.SearchBufferFocused <- false
        else
            this.CommandBuffer.AddKey(input)
