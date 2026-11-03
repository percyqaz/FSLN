open FSLN.Controller

[<EntryPoint>]
let main (_args: string array) : int =
    match State.TryCreate() with
    | Error error ->
        printfn "%s" error
        1
    | Ok state ->

    try
        state.Run().GetAwaiter().GetResult()
        0
    with error ->
        printfn "%s" error.Message
        1
