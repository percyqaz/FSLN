open System.IO
open Fernglas
open FSLN.Shared

let config = ConfigFile.FromUserFile(".fernglas")
Fernglas.loop(Directory.GetCurrentDirectory(), config.Commands)
