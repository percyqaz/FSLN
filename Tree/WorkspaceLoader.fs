namespace FSLN

open System
open System.IO
open System.Runtime.CompilerServices
open Microsoft.Build.Evaluation
open Microsoft.Build.Construction
open FSLN

type WorkspaceLoader =

    static member LoadDotnetProject(name: string, project_path: string) : Project =

        let project_path = Path.normalise(project_path)
        let project_guts = FSharpProject.Create(project_path)

        let project =
            {
                Name = name
                FullPath = project_path
                Guts = FSharp project_guts
                Children = ResizeArray<FileTreeEntry>()
                LastSeenUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }

        let inline create_folder (target: Parent, folder_name: string) =
            let parent_path =
                match target with
                | Parent.Project _ -> project_guts.BaseDirectory
                | Parent.Folder folder -> folder.FullPath

            let new_folder_path =
                Path.Combine(parent_path, folder_name).Replace('\\', Path.AltDirectorySeparatorChar)

            {
                Name = folder_name
                FullPath = new_folder_path
                Children = ResizeArray()
                Parent = target
            }

        let rec merge_trees (target: Parent, segments: string list, file_path: string, element: ProjectItemElement) =
            match segments with
            | [] ->
                target.Children.Add(
                    File
                        {
                            Name = Path.GetFileName(file_path)
                            FullPath = file_path
                            ProjectItemElement = Some element
                            Parent = target
                        }
                )
            | folder :: remaining when target.Children.Count > 0 ->
                let last = target.Children.[target.Children.Count - 1]

                match last with
                | Folder merge_folder when merge_folder.Name = folder ->
                    merge_trees(Parent.Folder(merge_folder), remaining, file_path, element)
                | _ ->
                    let new_folder = create_folder(target, folder)
                    target.Children.Add(Folder new_folder)
                    merge_trees(Parent.Folder(new_folder), remaining, file_path, element)

            | folder :: remaining ->
                let new_folder = create_folder(target, folder)
                target.Children.Add(Folder new_folder)
                merge_trees(Parent.Folder(new_folder), remaining, file_path, element)

        let inline is_relevant_element (property: ProjectItemElement) : bool =
            property.ElementName = "Compile"
            || property.ElementName = "None"
            || property.ElementName = "EmbeddedResource"

        let inline add_element (element: ProjectItemElement) : unit =

            let inline ensure_trailing_slash (path: string) : string =
                if path.EndsWith("/") then path else path + "/"

            let inline is_subdirectory (parent_path: string, child_path: string) =
                let parent_path = ensure_trailing_slash(parent_path)
                let child_path = ensure_trailing_slash(child_path)

                child_path <> parent_path && child_path.StartsWith(parent_path)

            let file_path =
                Path.normalise(Path.Combine(project_guts.BaseDirectory, element.Include))

            if is_subdirectory(project_guts.BaseDirectory, file_path) then
                let relative_path_segments =
                    Path
                        .get_directory_name(file_path)
                        .Replace(project_guts.BaseDirectory, "")
                        .Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                    |> List.ofArray

                merge_trees(Parent.Project(project), relative_path_segments, file_path, element)
            else
                printfn "'%s' is outside the project folder for '%s'!" file_path project_guts.BaseDirectory

        for item_group in project_guts.RootElement.ItemGroups do
            for element in item_group.Items do
                if is_relevant_element(element) then
                    add_element(element)

        project

    [<Extension>]
    static member ReloadSolution(workspace: Workspace) : Solution =
        let solution_file = SolutionFile.Parse(workspace.SolutionFile)

        ProjectCollection.GlobalProjectCollection.UnloadAllProjects()
        let projects_list = ResizeArray()

        for project in solution_file.ProjectsInOrder do
            if File.Exists(project.AbsolutePath) then
                projects_list.Add(WorkspaceLoader.LoadDotnetProject(project.ProjectName, project.AbsolutePath))
            else
                printfn "'%s' could not be found!" project.AbsolutePath

        workspace.Ordering.Sort(projects_list, _.FullPath)

        {
            Name = Path.GetFileNameWithoutExtension(workspace.SolutionFile)
            FullPath = workspace.SolutionFile
            Ordering = workspace.Ordering
            SolutionFile = solution_file
            Projects = projects_list
            LastSeenUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        }
