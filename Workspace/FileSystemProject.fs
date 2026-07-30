namespace FSLN

open System.IO

type FileSystemProject =
    {
        ProjectFilePath: string
        BaseDirectory: string
        Ordering: OrderFile
    }

    static member CreateFromFslnproj(workspace: Workspace, file: string) : FileSystemProject =
        let relative_path = File.ReadAllText(file).Trim()
        let project_path = Path.normalise(Path.Combine(workspace.RootPath, relative_path))

        { ProjectFilePath = Path.normalise(file); BaseDirectory = project_path; Ordering = workspace.Ordering }

    static member CreateFromCsproj(workspace: Workspace, file: string) : FileSystemProject =
        let project_containing_folder = Path.get_directory_name(file)

        {
            ProjectFilePath = Path.normalise(file)
            BaseDirectory = project_containing_folder
            Ordering = workspace.Ordering
        }
