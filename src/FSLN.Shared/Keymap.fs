namespace Fernglas

open System.Collections
open System.Collections.Generic

type Keymap() =

    let mutable map: Map<string, string> = Map.empty

    static member val internal LT = "＜"
    static member val internal GT = "＞"

    static member SpecialKey(name: string) : string = Keymap.LT + name + Keymap.GT

    static member val ENTER = Keymap.SpecialKey("Enter")
    static member val ESC = Keymap.SpecialKey("Esc")

    member this.Alias(string: string, target: string) : unit =

        let inline replace_special (s: string) : string =
            s.Replace("<Space>", " ").Replace("<", Keymap.LT).Replace(">", Keymap.GT)

        map <- map.Add(replace_special string, replace_special target)

    member this.AliasCommand(string: string, command: string) : unit =
        this.Alias(string, ":" + command + Keymap.ENTER)

    interface IEnumerable<string * string> with
        override this.GetEnumerator() : IEnumerator<string * string> =
            (map |> Seq.map(fun kvp -> kvp.Key, kvp.Value)).GetEnumerator()

    interface IEnumerable with
        override this.GetEnumerator() : IEnumerator =
            (map |> Seq.map(fun kvp -> kvp.Key, kvp.Value)).GetEnumerator()
