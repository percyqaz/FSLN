open System.IO
open Fernglas

let config = ConfigFile.FromUserFile(".fernglas")
Fernglas.loop(Directory.GetCurrentDirectory(), config.Commands)
