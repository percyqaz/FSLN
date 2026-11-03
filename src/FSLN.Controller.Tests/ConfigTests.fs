namespace FSLN.Controller.Tests

open System.Runtime.CompilerServices
open FSLN.Controller.Config
open NUnit.Framework

[<TestFixture>]
type ConfigTests() =

    [<Extension>]
    static member AssertOk(result: Result<'T, string>) =
        match result with
        | Ok _ -> ()
        | Error reason -> Assert.Fail(reason)

    [<Extension>]
    static member AssertOk(result: Result<'T, string>, expected: 'T) =
        match result with
        | Ok actual -> Assert.That(actual, Is.EqualTo<'T>(expected))
        | Error reason -> Assert.Fail(reason)

    [<Test>]
    member _.ParseFullscreenViewWithWhitespace() =
        let config = Config()
        let parse_result = config.ParseConfigLine("  view main = {  fsln  }  ")
        let resolve_result = config.ResolveViewConfig(None, "main")

        parse_result.AssertOk()

        let expected =
            { Left = { Command = "fsln"; WaitForKeypress = false }, 1; Right = None }

        resolve_result.AssertOk(expected)

    [<Test>]
    member _.ParseWaitForKeypressPrefix() =
        let config = Config()
        let parse_result = config.ParseConfigLine("  view main = { . fsln  }  ")
        let resolve_result = config.ResolveViewConfig(None, "main")

        parse_result.AssertOk()

        let expected =
            { Left = { Command = "fsln"; WaitForKeypress = true }, 1; Right = None }

        resolve_result.AssertOk(expected)

    [<Test>]
    member _.ParseTwoSideBySideProcesses() =
        let config = Config()

        let parse_result =
            config.ParseConfigLine("view main = { fsln } | { . git diff --staged }")

        let resolve_result = config.ResolveViewConfig(None, "main")

        parse_result.AssertOk()

        let expected =
            {
                Left = { Command = "fsln"; WaitForKeypress = false }, 1
                Right = Some({ Command = "git diff --staged"; WaitForKeypress = true }, 1)
            }

        resolve_result.AssertOk(expected)

    [<Test>]
    member _.ParseTwoSideBySideProcesses_OnlyCloseIfLeftCloses() =
        let config = Config()

        let parse_result =
            config.ParseConfigLine("view main = { fsln } < { . git diff --staged }")

        let resolve_result = config.ResolveViewConfig(None, "main")

        parse_result.AssertOk()

        let expected =
            {
                Left = { Command = "fsln"; WaitForKeypress = false }, 1
                Right = Some({ Command = "git diff --staged"; WaitForKeypress = true }, 0)
            }

        resolve_result.AssertOk(expected)

    [<Test>]
    member _.ParseTwoSideBySideProcesses_OnlyCloseIfRightCloses() =
        let config = Config()

        let parse_result =
            config.ParseConfigLine("view main = { fsln } > { . git diff --staged }")

        let resolve_result = config.ResolveViewConfig(None, "main")

        parse_result.AssertOk()

        let expected =
            {
                Left = { Command = "fsln"; WaitForKeypress = false }, 0
                Right = Some({ Command = "git diff --staged"; WaitForKeypress = true }, 1)
            }

        resolve_result.AssertOk(expected)

    [<Test>]
    member _.PreservePipeInsideShellCommand() =
        let config = Config()

        let parse_result =
            config.ParseConfigLine("view main = { printf hello | sed s/hello/goodbye/ }")

        let resolve_result = config.ResolveViewConfig(None, "main")

        parse_result.AssertOk()

        let expected =
            { Left = { Command = "printf hello | sed s/hello/goodbye/"; WaitForKeypress = false }, 1; Right = None }

        resolve_result.AssertOk(expected)

    [<Test>]
    member _.PreserveInternalWhitespace() =
        let config = Config()
        let parse_result = config.ParseConfigLine("view main = { echo   hello }")
        let resolve_result = config.ResolveViewConfig(None, "main")

        parse_result.AssertOk()

        let expected =
            { Left = { Command = "echo   hello"; WaitForKeypress = false }, 1; Right = None }

        resolve_result.AssertOk(expected)

    [<Test>]
    member _.FollowsAliases() =
        let config = Config()

        let parse_result_1 =
            config.ParseConfigLine("proc GIT_DIFF = . git diff --staged . | ydiff")

        let parse_result_2 = config.ParseConfigLine("view main = { fsln } | GIT_DIFF")
        let resolve_result = config.ResolveViewConfig(None, "main")

        parse_result_1.AssertOk()
        parse_result_2.AssertOk()

        let expected =
            {
                Left = { Command = "fsln"; WaitForKeypress = false }, 1
                Right = Some({ Command = "git diff --staged . | ydiff"; WaitForKeypress = true }, 1)
            }

        resolve_result.AssertOk(expected)

    [<Test>]
    member _.DepthsStackCorrectly() =
        let config = Config()
        let parse_result = config.ParseConfigLine("view main = { fsln } | { bash }")

        let existing =
            { Left = { Command = "fsln"; WaitForKeypress = false }, 3; Right = None }

        let resolve_result = config.ResolveViewConfig(Some existing, "main")

        parse_result.AssertOk()

        let expected =
            {
                Left = { Command = "fsln"; WaitForKeypress = false }, 4
                Right = Some({ Command = "bash"; WaitForKeypress = false }, 1)
            }

        resolve_result.AssertOk(expected)

    [<Test(Description = "This test acts as a note of what happens when you do this")>]
    member _.DepthsStackEdgeCase() =
        let config = Config()
        let parse_result = config.ParseConfigLine("view main = { fsln } > { bash }")

        let existing =
            { Left = { Command = "fsln"; WaitForKeypress = false }, 3; Right = None }

        let resolve_result = config.ResolveViewConfig(Some existing, "main")

        parse_result.AssertOk()

        let expected =
            {
                Left = { Command = "fsln"; WaitForKeypress = false }, 0
                Right = Some({ Command = "bash"; WaitForKeypress = false }, 1)
            }

        resolve_result.AssertOk(expected)

    [<TestCase("view = { fsln }")>]
    [<TestCase("view main { fsln }")>]
    [<TestCase("view main =")>]
    [<TestCase("view main = { fsln")>]
    [<TestCase("view main = fsln }")>]
    [<TestCase("view main = { fsln } | { git diff } | { shell }")>]
    member _.InvalidConfigLines(line: string) =
        let config = Config()
        let parse_result = config.ParseConfigLine(line)

        match parse_result with
        | Error _ -> Assert.Pass()
        | Ok _ -> Assert.Fail(sprintf "Expected an invalid view declaration to be rejected: %s" line)
