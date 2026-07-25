namespace FSLN

open System.Runtime.CompilerServices
open FSLN

type StateNavigationCommands =

    static let previous (siblings: ResizeArray<'T>, child: 'T) : 'T option =
        let index = siblings.IndexOf(child)
        if index > 0 then Some siblings.[index - 1] else None

    static let next (siblings: ResizeArray<'T>, child: 'T) : 'T option =
        let index = siblings.IndexOf(child)
        if index + 1 < siblings.Count then Some siblings.[index + 1] else None

    static let rec bottom_child_tree (state: State, entry: FileTreeEntry) : Selection =
        match entry with
        | File file -> Selection.File(file)
        | Folder folder ->
            if state.IsExpanded(folder) then
                bottom_child_tree(state, folder.Children.[folder.Children.Count - 1])
            else
                Selection.Folder(folder)

    static let bottom_child_project (state: State, project: Project) : Selection =
        if state.IsExpanded(project) then
            bottom_child_tree(state, project.Children.[project.Children.Count - 1])
        else
            Selection.Project(project)

    [<TailCall>]
    static let rec find_next_in_tree (state: State, current: FileTreeEntry) : Selection option =
        match next(current.Parent.Children, current) with
        | Some(File file_below) -> Some(Selection.File(file_below))
        | Some(Folder folder_below) -> Some(Selection.Folder(folder_below))
        | None ->
            match current.Parent with
            | Parent.Project project ->
                match next(state.Solution.Projects, project) with
                | Some project_below -> Some(Selection.Project(project_below))
                | None -> None
            | Parent.Folder folder -> find_next_in_tree(state, Folder folder)

    [<Extension>]
    static member NavigateUp(state: State) : unit =
        state.Selected <-
            match state.Selected with
            | Selection.Solution _ -> state.Selected
            | Selection.Project project ->
                match previous(state.Solution.Projects, project) with
                | Some project_above -> bottom_child_project(state, project_above)
                | None -> Selection.Solution(state.Solution)
            | Selection.Folder folder ->
                match previous(folder.Parent.Children, Folder folder) with
                | Some entry_above -> bottom_child_tree(state, entry_above)
                | None ->
                    match folder.Parent with
                    | Parent.Folder parent -> Selection.Folder(parent)
                    | Parent.Project project -> Selection.Project(project)
            | Selection.File file ->
                match previous(file.Parent.Children, File file) with
                | Some entry_above -> bottom_child_tree(state, entry_above)
                | None ->
                    match file.Parent with
                    | Parent.Folder parent_folder -> Selection.Folder(parent_folder)
                    | Parent.Project parent_project -> Selection.Project(parent_project)

    [<Extension>]
    static member NavigateDown(state: State) : unit =
        state.Selected <-
            match state.Selected with
            | Selection.Solution solution -> Selection.Project(solution.Projects.[0])
            | Selection.Project project ->
                if state.IsExpanded(project) then
                    match project.Children.[0] with
                    | File child_file -> Selection.File(child_file)
                    | Folder child_folder -> Selection.Folder(child_folder)
                else
                    match next(state.Solution.Projects, project) with
                    | Some project_below -> Selection.Project(project_below)
                    | None -> Selection.Project(project)
            | Selection.Folder folder ->
                if state.IsExpanded(folder) then
                    match folder.Children.[0] with
                    | File child_file -> Selection.File(child_file)
                    | Folder child_folder -> Selection.Folder(child_folder)
                else
                    find_next_in_tree(state, Folder folder) |> Option.defaultValue state.Selected
            | Selection.File file -> find_next_in_tree(state, File file) |> Option.defaultValue state.Selected

    [<Extension>]
    static member NavigateOut(state: State) : unit =
        state.Selected <-
            match state.Selected with
            | Selection.Solution solution -> Selection.Solution(solution)
            | Selection.Project _ -> Selection.Solution(state.Solution)
            | Selection.Folder folder ->
                match folder.Parent with
                | Parent.Folder parent_folder -> Selection.Folder(parent_folder)
                | Parent.Project parent_project -> Selection.Project(parent_project)
            | Selection.File file ->
                match file.Parent with
                | Parent.Folder parent_folder -> Selection.Folder(parent_folder)
                | Parent.Project parent_project -> Selection.Project(parent_project)

    [<Extension>]
    static member ExpandSelection(state: State) : unit =
        match state.Selected with
        | Selection.Solution _ -> ()
        | Selection.Project project -> state.Expanded <- state.Expanded.Add(project.FullPath)
        | Selection.Folder folder -> state.Expanded <- state.Expanded.Add(folder.FullPath)
        | Selection.File _ -> ()

    [<Extension>]
    [<TailCall>]
    static member CollapseSelection(state: State) : unit =
        let rec collapse_selected () : unit =
            match state.Selected with
            | Selection.Solution _ -> ()
            | Selection.Project project ->
                state.Expanded <- state.Expanded.Remove(project.FullPath)

                for subfolder in project.EnumerateSubfolders() do
                    state.Expanded <- state.Expanded.Remove(subfolder.FullPath)
            | Selection.Folder folder ->
                if state.IsExpanded(folder) then
                    state.Expanded <- state.Expanded.Remove(folder.FullPath)

                    for subfolder in folder.EnumerateSubfolders() do
                        state.Expanded <- state.Expanded.Remove(subfolder.FullPath)
                else
                    state.NavigateOut()
                    collapse_selected()
            | Selection.File _ ->
                state.NavigateOut()
                collapse_selected()

        collapse_selected()

    [<Extension>]
    static member MoveSelectionUp(state: State) : unit =
        match state.Selected with
        | Selection.Solution _ -> ()
        | Selection.Project _ -> ()
        | Selection.Folder folder -> folder.ParentProject.MoveUp(folder)
        | Selection.File file -> file.ParentProject.MoveUp(file)

    [<Extension>]
    static member MoveSelectionDown(state: State) : unit =
        match state.Selected with
        | Selection.Solution _ -> ()
        | Selection.Project _ -> ()
        | Selection.Folder folder -> folder.ParentProject.MoveDown(folder)
        | Selection.File file -> file.ParentProject.MoveDown(file)
