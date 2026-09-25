open System
open System.IO
open Fernglas

let get_fernglas_config () : string seq =

    let user_profile_settings =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fernglas")

    seq {
        if File.Exists(user_profile_settings) then
            yield! File.ReadAllLines(user_profile_settings)
    }
    |> Seq.filter(String.IsNullOrWhiteSpace >> not)

Fernglas.loop(Directory.GetCurrentDirectory(), get_fernglas_config())
