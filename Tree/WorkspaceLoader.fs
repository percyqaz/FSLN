namespace FSLN

open System
open System.IO
open System.Runtime.CompilerServices
open Microsoft.Build.Evaluation
open Microsoft.Build.Construction
open FSLN

type WorkspaceLoader =

    static member LoadFileSystemProject(workspace: Workspace, name: string, project_guts: FileSystemProject) : Project =

        let project =
            {
                Name = name
                Guts = FileSystem(project_guts)
                Children = ResizeArray<FileTreeEntry>()
                LastSeenUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }

        let rec recurse_folder (parent: Parent, path: string) =

            for subfolder in Directory.EnumerateDirectories(path) do
                let folder: FileTreeFolder =
                    {
                        Name = Path.GetFileName(subfolder)
                        FullPath = subfolder.Replace('\\', '/')
                        Children = ResizeArray()
                        Parent = parent
                    }

                // todo: filter by ignore file, for now:
                if folder.Name <> "bin" && folder.Name <> "obj" && folder.Name <> ".git" && folder.Name <> ".fsln" then
                    recurse_folder(Parent.Folder(folder), subfolder)

                    if folder.Children.Count > 0 then
                        workspace.Ordering.Sort(folder.Children, _.FullPath)
                        parent.Children.Add(Folder folder)

            for file_path in Directory.EnumerateFiles(path) do
                let file: FileTreeFile =
                    {
                        Name = Path.GetFileName(file_path)
                        FullPath = file_path.Replace('\\', '/')
                        ProjectItemElement = None
                        Parent = parent
                    }

                // todo: filter by ignore file
                if file.FullPath <> project.FullPath then
                    parent.Children.Add(File file)

        recurse_folder(Parent.Project(project), project_guts.BaseDirectory)
        workspace.Ordering.Sort(project.Children, _.FullPath)

        project

    static member LoadFSharpProject(name: string, project_path: string) : Project =

        let project_guts = FSharpProject.Create(project_path)

        let project =
            {
                Name = name
                Guts = FSharp project_guts
                Children = ResizeArray<FileTreeEntry>()
                LastSeenUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }

        let inline create_folder (target: Parent, folder_name: string) =
            let parent_path =
                match target with
                | Parent.Project _ -> project_guts.BaseDirectory
                | Parent.Folder folder -> folder.FullPath

            let new_folder_path = Path.Combine(parent_path, folder_name).Replace('\\', '/')

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
        let projects_list = ResizeArray()

        let inline load_dotnet_projects (solution_path: string) : unit =
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects()
            let solution_file = SolutionFile.Parse(solution_path)

            for project in solution_file.ProjectsInOrder do
                if File.Exists(project.AbsolutePath) then
                    let ext = Path.GetExtension(project.AbsolutePath).ToLower()

                    match ext with
                    | ".fsproj" ->
                        projects_list.Add(WorkspaceLoader.LoadFSharpProject(project.ProjectName, project.AbsolutePath))
                    | ".csproj" ->
                        let file_system_project =
                            FileSystemProject.CreateFromCsproj(workspace, project.AbsolutePath)

                        projects_list.Add(
                            WorkspaceLoader.LoadFileSystemProject(workspace, project.ProjectName, file_system_project)
                        )
                    | _ -> printfn "'%s' is unrecognised project type!" project.AbsolutePath
                else
                    printfn "'%s' could not be found!" project.AbsolutePath

        let inline load_virtual_projects () =
            for project in workspace.ProjectFiles() do
                let file_system_project = FileSystemProject.CreateFromFslnproj(workspace, project)

                projects_list.Add(
                    WorkspaceLoader.LoadFileSystemProject(
                        workspace,
                        Path.GetFileNameWithoutExtension(project),
                        file_system_project
                    )
                )

        match workspace.Solution with
        | Dotnet sln -> load_dotnet_projects(sln)
        | Virtual -> ()

        load_virtual_projects()
        workspace.Ordering.Sort(projects_list, _.FullPath)

        let name, fullpath =
            match workspace.Solution with
            | Dotnet sln -> Path.GetFileNameWithoutExtension(sln), sln.Replace('\\', '/')
            | Virtual ->
                Path.GetFileName(workspace.RootPath), Path.normalise(Path.Combine(workspace.RootPath, ".fsln", ".fsln"))

        {
            Name = name
            FullPath = fullpath
            Ordering = workspace.Ordering
            Projects = projects_list
            LastSeenUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        }
