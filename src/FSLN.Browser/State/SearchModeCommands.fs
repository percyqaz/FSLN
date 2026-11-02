namespace FSLN

open System.Runtime.CompilerServices
open FSLN

type SearchModeCommands =

    static let previous (siblings: ResizeArray<'T>, child: 'T) : 'T option =
        let index = siblings.IndexOf(child)
        if index > 0 then Some siblings.[index - 1] else None

    static let next (siblings: ResizeArray<'T>, child: 'T) : 'T option =
        let index = siblings.IndexOf(child)
        if index + 1 < siblings.Count then Some siblings.[index + 1] else None

    static let rec bottom_child_tree (state: State, entry: FilteredTreeEntry) : FSelection =
        match entry with
        | FFile file -> FSelection.FFile(file)
        | FFolder folder ->
            if state.IsExpanded(folder.Original) then
                bottom_child_tree(state, folder.Children.[folder.Children.Count - 1])
            else
                FSelection.FFolder(folder)

    static let bottom_child_project (state: State, project: FilteredProject) : FSelection =
        if state.IsExpanded(project.Original) then
            bottom_child_tree(state, project.Children.[project.Children.Count - 1])
        else
            FSelection.FProject(project)

    [<TailCall>]
    static let rec find_next_in_tree (sm: ISearchMode, current: FilteredTreeEntry) : FSelection option =
        match next(current.Parent.Children, current) with
        | Some(FFile file_below) -> Some(FSelection.FFile(file_below))
        | Some(FFolder folder_below) -> Some(FSelection.FFolder(folder_below))
        | None ->
            match current.Parent with
            | FParent.FProject project ->
                match next(sm.Solution.Projects, project) with
                | Some project_below -> Some(FSelection.FProject(project_below))
                | None -> None
            | FParent.FFolder folder -> find_next_in_tree(sm, FFolder folder)

    [<Extension>]
    static member NavigateUp(nm: ISearchMode, state: State) : unit =
        nm.Selected <-
            match nm.Selected with
            | FSelection.FSolution _ -> nm.Selected
            | FSelection.FProject project ->
                match previous(nm.Solution.Projects, project) with
                | Some project_above -> bottom_child_project(state, project_above)
                | None -> FSelection.FSolution(nm.Solution)
            | FSelection.FFolder folder ->
                match previous(folder.Parent.Children, FFolder folder) with
                | Some entry_above -> bottom_child_tree(state, entry_above)
                | None ->
                    match folder.Parent with
                    | FParent.FFolder parent -> FSelection.FFolder(parent)
                    | FParent.FProject project -> FSelection.FProject(project)
            | FSelection.FFile file ->
                match previous(file.Parent.Children, FFile file) with
                | Some entry_above -> bottom_child_tree(state, entry_above)
                | None ->
                    match file.Parent with
                    | FParent.FFolder parent_folder -> FSelection.FFolder(parent_folder)
                    | FParent.FProject parent_project -> FSelection.FProject(parent_project)

    [<Extension>]
    static member NavigateDown(sm: ISearchMode, state: State) : unit =
        sm.Selected <-
            match sm.Selected with
            | FSelection.FSolution solution ->
                if solution.Projects.Count > 0 then
                    FSelection.FProject(solution.Projects.[0])
                else
                    FSelection.FSolution(solution)
            | FSelection.FProject project ->
                if state.IsExpanded(project.Original) then
                    match project.Children.[0] with
                    | FFile child_file -> FSelection.FFile(child_file)
                    | FFolder child_folder -> FSelection.FFolder(child_folder)
                else
                    match next(sm.Solution.Projects, project) with
                    | Some project_below -> FSelection.FProject(project_below)
                    | None -> FSelection.FProject(project)
            | FSelection.FFolder folder ->
                if state.IsExpanded(folder.Original) then
                    match folder.Children.[0] with
                    | FFile child_file -> FSelection.FFile(child_file)
                    | FFolder child_folder -> FSelection.FFolder(child_folder)
                else
                    find_next_in_tree(sm, FFolder folder) |> Option.defaultValue sm.Selected
            | FSelection.FFile file -> find_next_in_tree(sm, FFile file) |> Option.defaultValue sm.Selected

    [<Extension>]
    static member NavigateOut(sm: ISearchMode) : unit =
        sm.Selected <-
            match sm.Selected with
            | FSelection.FSolution solution -> FSelection.FSolution(solution)
            | FSelection.FProject _ -> FSelection.FSolution(sm.Solution)
            | FSelection.FFolder folder ->
                match folder.Parent with
                | FParent.FFolder parent_folder -> FSelection.FFolder(parent_folder)
                | FParent.FProject parent_project -> FSelection.FProject(parent_project)
            | FSelection.FFile file ->
                match file.Parent with
                | FParent.FFolder parent_folder -> FSelection.FFolder(parent_folder)
                | FParent.FProject parent_project -> FSelection.FProject(parent_project)

    [<Extension>]
    static member ExpandAll(sm: ISearchMode, state: State) : unit =
        match sm.Selected with
        | FSelection.FSolution _ ->
            for project in sm.Solution.Projects do
                state.Expanded <- state.Expanded.Add(project.Original.FullPath)

                for folder in project.EnumerateSubfolders() do
                    state.Expanded <- state.Expanded.Add(folder.Original.FullPath)
        | FSelection.FProject project ->
            state.Expanded <- state.Expanded.Add(project.Original.FullPath)

            for folder in project.EnumerateSubfolders() do
                state.Expanded <- state.Expanded.Add(folder.Original.FullPath)
        | FSelection.FFolder folder ->
            state.Expanded <- state.Expanded.Add(folder.Original.FullPath)

            for folder in folder.EnumerateSubfolders() do
                state.Expanded <- state.Expanded.Add(folder.Original.FullPath)
        | FSelection.FFile _ -> ()

    [<Extension>]
    static member ExpandSelection(sm: ISearchMode, state: State) : unit =
        match sm.Selected with
        | FSelection.FSolution _ -> ()
        | FSelection.FProject project -> state.Expanded <- state.Expanded.Add(project.Original.FullPath)
        | FSelection.FFolder folder -> state.Expanded <- state.Expanded.Add(folder.Original.FullPath)
        | FSelection.FFile _ -> ()

    [<Extension>]
    [<TailCall>]
    static member CollapseSelection(sm: ISearchMode, state: State) : unit =
        let rec collapse_selected () : unit =
            match sm.Selected with
            | FSelection.FSolution _ -> ()
            | FSelection.FProject project ->
                state.Expanded <- state.Expanded.Remove(project.Original.FullPath)

                for subfolder in project.Original.EnumerateSubfolders() do
                    state.Expanded <- state.Expanded.Remove(subfolder.FullPath)
            | FSelection.FFolder folder ->
                if state.IsExpanded(folder.Original) then
                    state.Expanded <- state.Expanded.Remove(folder.Original.FullPath)

                    for subfolder in folder.Original.EnumerateSubfolders() do
                        state.Expanded <- state.Expanded.Remove(subfolder.FullPath)
                else
                    sm.NavigateOut()
                    collapse_selected()
            | FSelection.FFile _ ->
                sm.NavigateOut()
                collapse_selected()

        collapse_selected()
