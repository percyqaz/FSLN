namespace FSLN

open System.IO

type Workspace =
    {
        RootPath: string
        Ordering: OrderFile
    }

    static member private FromPath(workspace_root: string) : Workspace =
        { RootPath = workspace_root; Ordering = OrderFile(Path.Combine(workspace_root, ".fsln", ".fslnorder")) }

    static member Create(solution_folder: string) : Workspace =
        let workspace_root =
            match Path.find_fsln_workspace_root() with
            | None -> solution_folder
            | Some path -> path

        Directory.CreateDirectory(Path.Combine(workspace_root, ".fsln")) |> ignore
        Workspace.FromPath(workspace_root)
