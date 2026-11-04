namespace FSLN.Controller

open System
open System.Collections.Generic

type ProcessConfig = { Command: string; WaitForKeypress: bool }

type ViewConfig = { Left: ProcessConfig * int; Right: (ProcessConfig * int) option }

[<RequireQualifiedAccess>]
type ProcessExpr =
    | Config of ProcessConfig
    | Ident of string
    | Previous

[<RequireQualifiedAccess>]
type ViewExpr =
    | One of ProcessExpr
    | Two of (ProcessExpr * bool) * (ProcessExpr * bool)

type Config() =

    let views = Dictionary<string, ViewExpr>()
    let processes = Dictionary<string, ProcessConfig>()

    let is_upper_ident (s: string) : bool =
        s <> "" && String.forall (fun c -> c = '_' || Char.IsAsciiLetterUpper(c)) s

    let is_lower_ident (s: string) : bool =
        s <> "" && String.forall (fun c -> c = '_' || Char.IsAsciiLetterLower(c)) s

    member private this.ParseProcDefinition(expr: string) : ProcessConfig =
        let mutable command = expr.Trim()
        let mutable wait_for_keypress = false

        if command.StartsWith('.') then
            command <- command.Substring(1).Trim()
            wait_for_keypress <- true

        { Command = command; WaitForKeypress = wait_for_keypress }

    member private this.ParseViewExpr(expr: string) : Result<ViewExpr, string> =
        let mutable expr = expr.Trim()

        let parse_process_expr () : ProcessExpr =
            if expr.StartsWith('{') then
                let split = expr.Substring(1).Split('}', 2)

                if split.Length < 2 then
                    failwith "Expected a }"

                expr <- split.[1].Trim()
                ProcessExpr.Config(this.ParseProcDefinition(split.[0]))
            elif expr.Length > 1 && Char.IsAsciiLetterUpper(expr.[0]) then
                let split = expr.Split(' ', 2)

                if not(is_upper_ident(split.[0])) then
                    failwith "Proc Ident must be SHOUTING_SNAKE_CASE"

                expr <- if split.Length > 1 then split.[1].Trim() else ""
                ProcessExpr.Ident(split.[0])
            elif expr.StartsWith('.') then
                expr <- expr.Substring(1).Trim()
                ProcessExpr.Previous
            else
                failwithf "Unrecognised expression for process '%s'" expr

        try
            let left = parse_process_expr()

            if expr <> "" then
                let join = expr.[0]

                if join <> '|' && join <> '<' && join <> '>' then
                    failwithf "Invalid splitter symbol '%c'" join

                expr <- expr.Substring(1).Trim()
                let right = parse_process_expr()

                if expr <> "" then
                    failwithf "Unexpected stuff at end of expression: %s" expr

                ViewExpr.Two((left, join <> '>'), (right, join <> '<'))
            else
                ViewExpr.One(left)
            |> Ok
        with err ->
            Error err.Message

    member this.ParseConfigLine(line: string) : Result<unit, string> =

        let parse_view (ident, expr) =
            if not(is_lower_ident ident) then
                failwithf "View Ident must be snake_case"

            match this.ParseViewExpr(expr) with
            | Ok parsed_expr -> views.[ident] <- parsed_expr
            | Error reason -> failwithf "%s" reason

        let parse_proc (ident, expr) =
            if not(is_upper_ident ident) then
                failwithf "Proc Ident must be SHOUTING_SNAKE_CASE"

            processes.[ident] <- this.ParseProcDefinition(expr)

        let line = line.Trim()

        if line = "" then
            Ok()
        else

        try
            let split = line.Split(' ', 2)

            if split.Length < 2 then
                failwithf "Invalid config line: %s" line

            let esplit = split.[1].Split('=', 2)

            match split.[0].Trim() with
            | "view" -> parse_view(esplit.[0].Trim(), esplit.[1].Trim())
            | "proc" -> parse_proc(esplit.[0].Trim(), esplit.[1].Trim())
            | other -> failwithf "Unrecognised word '%s'" other
            |> Ok
        with err ->
            Error err.Message

    member this.ResolveViewConfig(top_of_stack: ViewConfig option, expr_or_ident: string) : Result<ViewConfig, string> =

        let resolve (tos: (ProcessConfig * int) option, (ex, close_on_exit): ProcessExpr * bool) : ProcessConfig * int =

            let resolve_inner (config: ProcessConfig) =
                if not close_on_exit then
                    config, 0
                else
                    match tos with
                    | None -> config, 1
                    | Some(under_config, depth) -> if under_config = config then config, depth + 1 else config, 1

            match ex with
            | ProcessExpr.Config config -> resolve_inner(config)
            | ProcessExpr.Ident ident ->
                if processes.ContainsKey(ident) then
                    resolve_inner(processes.[ident])
                else
                    failwithf "No such process alias '%s'" ident
            | ProcessExpr.Previous ->
                match tos with
                | None -> failwith "'.' used as initial view, nothing to repeat!"
                | Some(under_config, depth) -> under_config, depth + 1

        try
            let parsed_expr =
                if is_lower_ident(expr_or_ident) then
                    if views.ContainsKey(expr_or_ident) then
                        views.[expr_or_ident]
                    else
                        failwithf "No such view alias '%s'" expr_or_ident
                else
                    match this.ParseViewExpr(expr_or_ident) with
                    | Error reason -> failwithf "Parse error in view expression: %s" reason
                    | Ok expr -> expr

            match parsed_expr with
            | ViewExpr.One(ex) -> Ok { Left = resolve(top_of_stack |> Option.map _.Left, (ex, true)); Right = None }
            | ViewExpr.Two(left, right) ->
                Ok
                    {
                        Left = resolve(top_of_stack |> Option.map _.Left, left)
                        Right = Some(resolve(top_of_stack |> Option.bind _.Right, right))
                    }
        with err ->
            Error err.Message
