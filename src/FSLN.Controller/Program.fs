open FSLN.Controller

[<EntryPoint>]
let main (_args: string array) : int =
    match State.TryCreate() with
    | Error error ->
        printfn "%s" error
        1
    | Ok state ->

    let server = PipeCommandServer()

    let main_async =
        task {
            do! server.StartAsync()
            do! state.Run()
            do! server.StopAsync()
        }

    try
        main_async.GetAwaiter().GetResult()
        0
    with error ->
        printfn "%s" error.Message
        1
