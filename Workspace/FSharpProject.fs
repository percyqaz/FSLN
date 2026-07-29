namespace FSLN

open Microsoft.Build.Construction

type FSharpProject =
    {
        BaseDirectory: string
        RootElement: ProjectRootElement
    }

    static member Create(path_to_project: string) : FSharpProject =
        let project_containing_folder = Path.get_directory_name(path_to_project)
        let project_file = ProjectRootElement.Open(path_to_project)

        { BaseDirectory = project_containing_folder; RootElement = project_file }
