namespace FSLN

open System.IO

type Workspace =
    {
        SolutionFile: string
        RootPath: string
        Ordering: OrderFile
    }

    member this.ProjectFiles() : string seq =
        Directory.EnumerateFiles(Path.Combine(this.RootPath, ".fsln"), "*.fslnproj")

    static member Create(solution_file: string) : Workspace =
        let workspace_root =
            match Path.find_fsln_workspace_root() with
            | None -> Path.GetDirectoryName(solution_file)
            | Some path -> path

        Directory.CreateDirectory(Path.Combine(workspace_root, ".fsln")) |> ignore

        {
            SolutionFile = solution_file
            RootPath = workspace_root
            Ordering = OrderFile(Path.Combine(workspace_root, ".fsln", ".fslnorder"))
        }
