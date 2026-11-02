# Terminal hosting evaluation

This deliberately small F# console project evaluates hosting interactive terminal programs with [Porta.Pty](https://github.com/tomlm/Porta.Pty) and [XTerm.NET](https://github.com/tomlm/XTerm.NET). It is separate from the FSLN application and does not implement the proposed controller or view stack.

## Run

From the FSLN repository root:

```powershell
dotnet run --project .\terminal-eval\TerminalEval.fsproj -- nvim
dotnet run --project .\terminal-eval\TerminalEval.fsproj -- fsln
```

The `nvim` target launches Neovim with `--clean`. If a `vim` executable is available, it can also be launched directly:

```powershell
dotnet run --project .\terminal-eval\TerminalEval.fsproj -- vim
```

The working directory is inherited from the directory where the harness is run. Use the hosted program's ordinary exit command to return to the harness.

## Current evaluation coverage

- Starts a child process in a real PTY through Porta.Pty.
- Feeds PTY output into an XTerm.NET terminal model.
- Renders that terminal model in a centered virtual terminal half the outer terminal's width and height, including text attributes and colors.
- Redraws the pane over its existing screen cells without clearing the outer screen between updates.
- Routes keyboard input to the child and forwards terminal-generated response data back to its PTY.
- Propagates outer terminal dimensions to the hosted terminal and PTY.
- Reports the child's exit code and restores the outer terminal screen.

This is a one-process host with a centered half-size virtual terminal. It checks the core library pairing and representative TUI compatibility; it does not yet evaluate split-pane composition, mouse input, clipboard behavior, image protocols, scrollback interaction, alternate persistent sessions, or cross-platform execution. The renderer is intentionally minimal and may not match every terminal's glyph-width and color behavior.

## Environment notes

The harness is targeted at .NET 10, matching the current FSLN project and the evaluated Porta.Pty/XTerm.NET packages. The machine used for the initial Windows check had Neovim 0.12.2 available but no `vim` executable, so Neovim is the available Vim-style test program there.
