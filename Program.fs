open System
open System.IO
open FSLN

let get_fsln_config () : string seq =
    let user_profile_settings =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fsln")

    let local_directory_settings = Path.walk_tree_specific_file(".fsln")

    seq {
        if File.Exists(user_profile_settings) then
            yield! File.ReadAllLines(user_profile_settings)

        match local_directory_settings with
        | Some file when file <> user_profile_settings && File.Exists(file) -> yield! File.ReadAllLines(file)
        | _ -> ()
    }
    |> Seq.filter(String.IsNullOrWhiteSpace >> not)

[<EntryPoint>]
let main (argv: string array) : int =
    ignore argv
    let sln = Path.walk_tree_specific_filetypes [| ".slnx"; ".sln" |]

    match sln with
    | None -> 1
    | Some solution_path ->
        let solution_folder = Path.GetDirectoryName(solution_path)
        let workspace = Workspace.Create(solution_path)
        Directory.SetCurrentDirectory(solution_folder)
        FSLN.loop(get_fsln_config(), workspace)
        0
