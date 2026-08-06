namespace FSLN

open System.IO

type WorkspaceSolution =
    | Dotnet of path: string
    | Virtual

type Workspace =
    {
        Solution: WorkspaceSolution
        RootPath: string
        Ordering: OrderFile
    }

    member this.ProjectFiles() : string seq =
        Directory.EnumerateFiles(Path.Combine(this.RootPath, ".fsln"), "*.fslnproj")

    static member CreateBasedOnSolution(solution_file: string) : Workspace =
        let workspace_root =
            match Path.find_fsln_workspace_root() with
            | None -> Path.GetDirectoryName(solution_file)
            | Some path -> path

        Directory.CreateDirectory(Path.Combine(workspace_root, ".fsln")) |> ignore

        {
            Solution = Dotnet(solution_file)
            RootPath = workspace_root
            Ordering = OrderFile(Path.Combine(workspace_root, ".fsln", ".fslnorder"))
        }

    static member CreateBasedOnFslnFolder(workspace_root: string) : Workspace =
        {
            Solution = Virtual
            RootPath = workspace_root
            Ordering = OrderFile(Path.Combine(workspace_root, ".fsln", ".fslnorder"))
        }

    static member TryDetect() : Workspace option =
        match Path.walk_tree_specific_filetypes [| ".slnx"; ".sln" |] with
        | Some solution_path ->
            let workspace = Workspace.CreateBasedOnSolution(solution_path)
            Some(workspace)
        | None ->

        match Path.find_fsln_workspace_root() with
        | Some path when File.Exists(Path.Combine(path, ".fsln", ".fsln")) ->
            let workspace = Workspace.CreateBasedOnFslnFolder(path)
            Some(workspace)
        | _ -> None

    static member Init() : unit =
        Directory.CreateDirectory(".fsln") |> ignore
        let containing_folder = Path.GetFileName(Directory.GetCurrentDirectory())
        File.WriteAllText(Path.Combine(".fsln", containing_folder + ".fslnproj"), "")
        File.WriteAllText(Path.Combine(".fsln", ".fsln"), "")
