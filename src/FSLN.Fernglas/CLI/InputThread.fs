namespace Fernglas

open System
open System.Threading

type InputThread() =
    let sw = SpinWait()

    member this.TryReadKey(timeout_millis: int, key: outref<ConsoleKeyInfo>) : bool =
        let now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

        let timeout_up () =
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - now >= timeout_millis

        while not(Console.KeyAvailable) && not(timeout_up()) do
            sw.SpinOnce()

        if Console.KeyAvailable then
            key <- Console.ReadKey(true)
            true
        else
            false
