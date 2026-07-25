namespace FSLN

open System
open System.Drawing

type Display(state: State) =

    let view = ScreenBuffer(Console.BufferHeight - 2)

    member inline private this.RenderFile
        (indent: string, icolor: Color, is_selected: bool, is_last: bool, file: FileTreeFile)
        : unit =
        let git_status = state.GitFileStatus(file.FullPath)
        let wt = git_status.WorkingTree <> Unchanged
        let status = if wt then git_status.WorkingTree else git_status.Index

        let color =
            match status with
            | Added
            | Untracked -> state.Theme.ColorsGit.Added
            | Deleted -> state.Theme.ColorsGit.Deleted
            | Unchanged -> state.Theme.ColorFile
            | _ -> state.Theme.ColorsGit.Modified

        let dirty_icon = if wt then state.Theme.IconGitWorkingTreeDirty.ToString() else ""

        let tree_marker =
            if is_last then state.Theme.TreeConnectors.Leaf else state.Theme.TreeConnectors.Branch

        let indent = indent + tree_marker.ForeColor(icolor)

        let line =
            sprintf
                "%c %s %s"
                state.Theme.IconFile
                (file.Name.ForeColor(color))
                (dirty_icon.ForeColor(state.Theme.ColorGitWorkingTreeDirty))

        view.Line(indent + (if is_selected then line.BackColor(state.Theme.ColorSelection) else line), is_selected)

    member inline private this.RenderFolder
        (indent: string, icolor: Color, is_selected: bool, is_expanded: bool, is_last: bool, folder: FileTreeFolder)
        : unit =
        let tree_marker =
            if is_last then state.Theme.TreeConnectors.Leaf else state.Theme.TreeConnectors.Branch

        let indent = indent + tree_marker.ForeColor(icolor)

        let expand_marker =
            if is_expanded then state.Theme.ExpandMarkers.Open else state.Theme.ExpandMarkers.Closed

        let line =
            sprintf
                "%c %s %s"
                state.Theme.IconFolder
                ((folder.Name + "/").ForeColor(state.Theme.ColorFolder).Bold())
                (expand_marker.ToString().ForeColor(state.Theme.ColorExpandIcon))

        view.Line(indent + (if is_selected then line.BackColor(state.Theme.ColorSelection) else line), is_selected)

    member inline private this.RenderProject(is_selected: bool, is_expanded: bool, project: Project) : unit =
        let expand_marker =
            if is_expanded then state.Theme.ExpandMarkers.Open else state.Theme.ExpandMarkers.Closed

        let line =
            sprintf
                "%c %s %s"
                state.Theme.IconProject
                (project.Name.ForeColor(state.Theme.ColorProject).Bold())
                (expand_marker.ToString().ForeColor(state.Theme.ColorExpandIcon))

        view.Line((if is_selected then line.BackColor(state.Theme.ColorSelection) else line), is_selected)

    member inline private this.RenderSolution(solution: Solution) : unit =
        let is_selected = state.IsSelected(solution)

        let line =
            sprintf "%c %s " state.Theme.IconSolution (solution.Name.ForeColor(state.Theme.ColorSolution).Bold())

        view.Line((if is_selected then line.BackColor(state.Theme.ColorSelection) else line), is_selected)

    member this.RenderNormalTree(nm: NormalMode) : unit =

        let rec display_entry (indent: string, icolor: Color, is_last: bool, entry: FileTreeEntry) : unit =
            match entry with
            | File file ->
                let is_selected = state.IsSelected(file)
                this.RenderFile(indent, icolor, is_selected, is_last, file)
            | Folder folder ->
                let is_selected = state.IsSelected(folder)
                let is_expanded = state.IsExpanded(folder)
                this.RenderFolder(indent, icolor, is_selected, is_expanded, is_last, folder)

                if is_expanded then
                    let inner_color =
                        if is_selected then state.Theme.ColorConnectorsFolder else state.Theme.ColorConnectorsDefault

                    let mutable i = 0

                    while i < folder.Children.Count do
                        let indent_symbol =
                            if is_last then
                                state.Theme.TreeConnectors.Empty
                            else
                                state.Theme.TreeConnectors.Vertical.ForeColor(icolor)

                        let child_is_last = i + 1 = folder.Children.Count
                        display_entry(indent + indent_symbol, inner_color, child_is_last, folder.Children.[i])
                        i <- i + 1

        let inline display_project (project: Project) : unit =
            let is_selected = state.IsSelected(project)
            let is_expanded = state.IsExpanded(project)
            this.RenderProject(is_selected, is_expanded, project)

            if is_expanded then
                let icolor =
                    if is_selected then state.Theme.ColorConnectorsProject else state.Theme.ColorConnectorsDefault

                let mutable i = 0

                while i < project.Children.Count do
                    display_entry("", icolor, i + 1 = project.Children.Count, project.Children.[i])
                    i <- i + 1

        this.RenderSolution(nm.Solution)

        for project in nm.Solution.Projects do
            display_project(project)

    member this.RenderSearchTree(sm: SearchMode) : unit =

        let rec display_entry (indent: string, icolor: Color, is_last: bool, entry: FilteredTreeEntry) : unit =
            match entry with
            | FFile file ->
                let is_selected = state.IsSelected(file.Original)
                this.RenderFile(indent, icolor, is_selected, is_last, file.Original)
            | FFolder folder ->
                let is_selected = state.IsSelected(folder.Original)
                let is_expanded = state.IsExpanded(folder.Original)
                this.RenderFolder(indent, icolor, is_selected, is_expanded, is_last, folder.Original)

                if is_expanded then
                    let inner_color =
                        if is_selected then state.Theme.ColorConnectorsFolder else state.Theme.ColorConnectorsDefault

                    let mutable i = 0

                    while i < folder.Children.Count do
                        let indent_symbol =
                            if is_last then
                                state.Theme.TreeConnectors.Empty
                            else
                                state.Theme.TreeConnectors.Vertical.ForeColor(icolor)

                        let child_is_last = i + 1 = folder.Children.Count
                        display_entry(indent + indent_symbol, inner_color, child_is_last, folder.Children.[i])
                        i <- i + 1

        let inline display_project (project: FilteredProject) : unit =
            let is_selected = state.IsSelected(project.Original)
            let is_expanded = state.IsExpanded(project.Original)
            this.RenderProject(is_selected, is_expanded, project.Original)

            if is_expanded then
                let icolor =
                    if is_selected then state.Theme.ColorConnectorsProject else state.Theme.ColorConnectorsDefault

                let mutable i = 0

                while i < project.Children.Count do
                    display_entry("", icolor, i + 1 = project.Children.Count, project.Children.[i])
                    i <- i + 1

        this.RenderSolution(sm.Tree.Original)

        for project in sm.Tree.Projects do
            display_project(project)

    member this.StatusLine() : string =

        let inline fmt_ahead_behind (leading_symbol: char, count: int option) =
            match count with
            | Some count -> (sprintf " %c%i" leading_symbol count)
            | None -> ""

        let inline dirty_files (status: GitStatus) : string =
            if status.WorkingTreeDirty > 0 then sprintf " *%i" status.WorkingTreeDirty
            elif status.IndexDirty > 0 then " *"
            else ""

        let git_status =
            match state.GitStatus with
            | Some status ->
                sprintf
                    "[%s%s%s]%s "
                    (status.Branch.ForeColor(0x8888ff).Bold())
                    (fmt_ahead_behind('+', status.Ahead).ForeColor(0x88FF88))
                    (fmt_ahead_behind('-', status.Behind).ForeColor(0xFF8888))
                    (dirty_files(status).ForeColor(state.Theme.ColorGitWorkingTreeDirty))
            | None -> ""

        git_status + state.StatusLine

    member this.BufferLine() : string =
        match state.ActiveBuffer with
        | ActiveBuffer.Command -> state.CommandBuffer.ToString().ForeColor(0x88FF88).Bold()
        | ActiveBuffer.Search -> "SEARCH: " + state.SearchBuffer.ToString().ForeColor(0x8888FF).Bold()

    member this.Redraw() : unit =

        view.Height <- Console.BufferHeight - 2

        match state.Mode with
        | Mode.Normal nm -> this.RenderNormalTree(nm)
        | Mode.Search sm -> this.RenderSearchTree(sm)

        view.Draw()

        Console.WriteLine(this.StatusLine().ClearRestOfLine())
        Console.Write(this.BufferLine().ClearRestOfLine())
