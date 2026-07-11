namespace fsln

open System
open System.Drawing
open System.Runtime.CompilerServices

type AnsiStringExtensions =
    
    [<Extension>]
    static member ForeColor(text: string, foreground: Color) : string =
        sprintf "\u001b[38;2;%d;%d;%dm%s\u001b[39m" foreground.R foreground.G foreground.B text
        
    [<Extension>]
    static member ForeColor(text: string, foreground: int) : string =
        text.ForeColor(Color.FromArgb(foreground))
        
    [<Extension>]
    static member BackColor(text: string, background: Color) : string =
        sprintf "\u001b[48;2;%d;%d;%dm%s\u001b[49m" background.R background.G background.B text
        
    [<Extension>]
    static member BackColor(text: string, background: int) : string =
        text.BackColor(Color.FromArgb(background))
        
    [<Extension>]
    static member Bold(text: string) : string =
        sprintf "\u001b[1m%s\u001b[22m" text

type InteractiveDisplay(state: InteractiveState) =

    // todo: buffer the draw
    
    member inline private this.RenderFile(indent: string, file: FileTreeFile) : unit =
        let is_selected = state.Selected = Selection.File file
        let line = sprintf "  %s%s  " indent file.Name
        Console.WriteLine(if is_selected then line.BackColor(0x333300) else line)
            
    member inline private this.RenderFolder(indent: string, folder: FileTreeFolder) : unit =
        let is_selected = state.Selected = Selection.Folder folder
        let is_expanded = state.IsExpanded(folder)
        let expand_marker = if is_expanded then "-" else "+"
        let line = sprintf "%s %s%s  " (expand_marker.ForeColor(0x444488)) indent ((folder.Name + "/").ForeColor(0xFFFF88).Bold())
        Console.WriteLine(if is_selected then line.BackColor(0x333300) else line)
            
    member inline private this.RenderProject(project: Project) : unit =
        let is_selected = state.Selected = Selection.Project project
        let is_expanded = state.IsExpanded(project)
        let expand_marker = if is_expanded then "-" else "+"
        let line = sprintf "%s %s " (expand_marker.ForeColor(0x444488)) (project.Name.ForeColor(0xFF00FF).Bold())
        Console.WriteLine(if is_selected then line.BackColor(0x333300) else line)
            
    member inline private this.RenderSolution(solution: Solution) : unit =
        let is_selected = state.Selected = Selection.Solution solution
        let line = sprintf " %s " (solution.Name.ForeColor(0xFFDDFF).Bold())
        Console.WriteLine(if is_selected then line.BackColor(0x333300) else line)
    
    member this.RenderTree() : unit =
        let rec display_entry (depth: int, entry: FileTreeEntry) : unit =
            let indent = String.replicate depth "  "
            match entry with
            | File file -> this.RenderFile(indent, file)
                    
            | Folder folder ->
                this.RenderFolder(indent, folder)
                    
                if state.IsExpanded(folder) then
                    for e in folder.Children do
                        display_entry(depth + 1, e)
                
        let inline display_project (project: Project) : unit =
            this.RenderProject(project)
            if state.IsExpanded(project) then
                for f in project.Children do
                    display_entry(0, f)
            
        this.RenderSolution(state.Solution)
        for project in state.Solution.Projects do
            display_project(project)
    
    member this.Redraw() : unit =
        Console.Clear()
        this.RenderTree()
        if state.Buffer <> "" then
            printfn "%s" state.Buffer
        else
            printfn "%s" state.StatusLine