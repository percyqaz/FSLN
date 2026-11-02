# FSLN Controller and View System Specification

## Status

This document records the product and architectural requirements discussed for a future FSLN controller and pane system. It is a design specification, not an implementation plan. Implementation choices that have not yet been agreed are listed under [Open design questions](#open-design-questions).

## 1. Purpose

FSLN currently provides a terminal-based view of a .NET solution tree and can dispatch user-defined commands and shell macros. External interactive programs such as Vim and Git run directly in the terminal; FSLN waits for them to finish and then restores its own view.

The proposed system extends this into a coordinated terminal application. It must support multiple interactive programs in panes, change which programs are visible as workflows change, preserve selected programs across view changes, route input to the focused pane, and coordinate program lifetimes and communication.

The system should support Windows, Linux, and macOS desktop environments with the same user-facing pane experience and, preferably, the same pane backend on every operating system.

## 2. Goals

- Make the FSLN controller the entry point users launch.
- Run the existing solution explorer as one program managed by the controller.
- Display multiple interactive terminal programs in resizable panes in one terminal UI.
- Coordinate processes and views centrally, including lifecycle transitions and inter-process communication.
- Keep long-lived programs and their state across view changes.
- Scope other programs to a view and stop them when that view is left.
- Provide the same pane experience on Windows, Linux, and macOS.
- Keep operating-system-specific terminal details behind a shared backend abstraction where possible.

## 3. Non-goals (current scope)

- The system is not intended to be a general desktop window manager.
- A process is never displayed in two visible panels at once.
- Applications are not expected to communicate directly with one another; communication goes through the controller.
- Mouse input, clipboard integration, and other advanced terminal features are desirable but are not required for the first version unless validated by the terminal evaluation.

## 4. Terminology

### Controller

The top-level FSLN program launched by the user. It owns the session, process supervision, terminal hosting, view stack, pane layout, focus, input routing, and mediated communication.

### Managed program / process instance

An interactive program launched or supervised by the controller. Examples include the solution explorer, a shell, `git commit`, and a diff viewer. A process instance has an identity, a lifetime policy, a terminal session, and lifecycle state.

### Pane

A visible rectangular region assigned to one managed process instance in the active view. A pane has a size and may receive keyboard input when focused.

### View

A presentation and workflow state describing which process instances are displayed, their pane arrangement, and the initial focus. A view may reference existing process instances and/or define new ones.

### View stack

The navigation history of active views. Entering a new workflow pushes a view. Returning from it pops the top view and restores the preceding view.

### Long-lived process

A process intended to survive view changes. It remains supervised for the controller session and is stopped when the controller exits. If it exits unexpectedly, it is restarted when it is needed again. A restart may be clean and is not required to restore in-memory process state.

### View-scoped process

A process whose lifetime is owned by a particular view. It is stopped when that view is exited, including when the view is popped from the stack.

### Preview view

The view that is active immediately before a new view is pushed. Its focused location can be used to choose a corresponding initial focus when the new view does not specify one.

## 5. User-facing model

Users launch the controller, not an independent pane manager. The controller starts and supervises the solution explorer and other configured or requested programs. The explorer is an application that can run in a terminal environment; it should not need to own pane rendering or directly control other programs.

The controller presents a view composed of panels. For example, the initial `main` view may contain:

- The FSLN solution explorer on the left.
- A persistent terminal emulator/shell on the right.

The term “terminal emulator” here describes an interactive terminal program hosted in a pane. The controller is responsible for rendering the child program's terminal output and forwarding input through a pseudo-terminal or equivalent terminal interface.

## 6. Views and panes

### 6.1 View contents

Each view specifies:

- The process instances to show in its panes.
- A one-pane fullscreen or two-pane side-by-side layout.
- The leftmost pane's process, which is initially focused.
- Any view-scoped processes that it creates and owns.
- The leftmost process, which is the primary process and whose exit triggers the view's return transition.

A view can include a process instance created by another view. The reference points to the same process instance; it does not launch a duplicate. A process instance may be referenced by multiple views, but no active view may show that process in more than one pane at the same time.

### 6.2 View configuration syntax

Views are declared as configuration lines, one view per line:

```text
view <view name> = { <process> }
view <view name> = { <left process> } | { <right process> }
```

The first form displays one fullscreen pane. The second displays two equal-width side-by-side panes, with any odd leftover column assigned to the right pane. The divider does not consume a column. The initial syntax supports at most two panes, with no nested splits or tabs. There is no minimum pane size; the controller uses the available dimensions even if a process renders poorly at that size. The left process receives initial focus and controls when the view returns to the previous view. The right process is a companion process; its exit alone does not pop the view.

The process text is a command string. A process may have one optional prefix:

| Prefix | Meaning |
| --- | --- |
| `!` | Persistent process. Reusing the same persistent process specification in another view refers to the same process instance. |
| `.` | After the process exits, retain its final screen and wait for at least one keypress before closing its pane. If it is the leftmost process, the keypress also allows the view to return. |
| *(none)* | View-scoped process with no extra wait after a successful exit. |

The `!` and `.` prefixes are mutually exclusive. A nonzero exit still displays the process's final screen and waits for a keypress, as specified in the lifecycle rules; a `.` process waits after any exit code. A single keypress satisfies both conditions when both apply.

Process command strings are executed through a shell, following FSLN's existing shell-dispatch convention: `cmd.exe /c` on Windows and `/bin/sh -c` on Linux and macOS. Commands originating in existing FSLN macros retain the current FSLN command-substitution behavior.

Persistent process identity uses the `!` prefix and the command text. Whitespace surrounding the command text is trimmed for identity matching; internal command text, including its whitespace, remains significant. Only the outer view parser interprets the `|` between braced process specifications as a pane separator. Pipe characters inside a process command are part of that command. The exact `} | {` separator sequence is reserved by the view syntax; no escape syntax is provided initially. This can be revisited if it creates a practical issue.

### 6.3 Pane sizing and process terminal size

The controller lays out panes and communicates each visible pane's dimensions to its process terminal. When a reused process appears in a differently sized pane after a view transition, its terminal dimensions are updated. Processes should receive the normal terminal resize notification supported by the platform backend.

Hidden processes do not receive keyboard input. Their terminal output and screen state are retained by the controller while they run. Each process's scrollback is capped at 200 lines; when the cap is reached, the oldest lines are discarded. No guarantee is made that hidden processes can continue useful interactive work without input.

### 6.4 Focus and input routing

The controller tracks one focused pane in the active view and routes user keystrokes to that process's terminal session. The leftmost process is focused when a view is pushed.

The controller exposes commands to focus the left pane and the right pane. FSLN-owned programs may request these commands through the controller protocol. The default keybindings are capital `L` for the right pane and capital `H` for the left pane. These keys are reserved globally by the controller and are not forwarded to the focused process. There are no alternate bindings for now. A command targeting a pane that is absent from the active view has no target.

## 7. View stack and transitions

### 7.1 Push

Entering a new workflow pushes a view onto the stack. The previously active view becomes the preview view and remains beneath it. Its long-lived processes continue running, and their state is retained. View-scoped processes belonging to a covered view are governed by that view's lifetime rules; the current requirements do not request that they be stopped merely because the view is covered. They are stopped when their owning view is exited.

### 7.2 Pop / return

When a view's primary process exits, for any exit code, the controller exits that view and returns to the previous view on the stack. Before the view is removed, the controller requests graceful shutdown of all remaining view-scoped processes simultaneously. After a shared 500 ms deadline, it force-kills any that have not exited.

The process that caused the return has already exited. If it exits with a non-zero status, the controller displays its final terminal screen and exit status and waits for a keypress before completing the return. The error acknowledgement occurs on the exited process's final screen, not on the restored view.

After the acknowledgement, the controller completes cleanup and restores the previous view, including its prior process instances, terminal state, layout, and relevant focus state. If there is no previous view, the controller shuts down fully, closing any remaining processes.

### 7.3 Leaving a view by another action

When the user or controller exits a view without its primary process having completed, the controller requests graceful shutdown of all view-scoped processes simultaneously, waits up to the shared 500 ms deadline, then force-kills any remaining processes. It then pops the view and restores the previous view.

### 7.4 Long-lived process exit

Long-lived processes are expected to remain alive for the controller session. If a long-lived process exits while visible, its pane retains the final screen, displays the exit code, and waits for a keypress. If the process exits while hidden, the controller restarts it cleanly when returning to a view that needs it. A clean restart may lose in-memory state.

If a persistent process exits while visible, the controller retains its final screen, displays the exit code, and waits for a keypress. The view then follows its normal exit behavior. When a persistent process that exited while hidden is needed again, the controller attempts a clean restart. If that restart fails, the pane displays a process-start error and waits for a keypress before retrying. Each retry requires a separate keypress; the controller never retries automatically.

### 7.5 Starting a configured view

FSLN macros and other controller actions can request a view push by name using the controller command `:view <view name>`. The controller looks up the configured view and pushes it onto the view stack. The view's leftmost process controls its automatic return transition as described above.

## 8. Process lifetime and supervision

### 8.1 Long-lived processes

- Survive all view pushes and pops.
- Keep the same process identity when reused by another view.
- Are stopped when the controller exits.
- Are restarted cleanly when found exited while hidden and needed again.
- Do not have to preserve state across a restart.

The solution explorer and the main terminal shell are examples of long-lived processes. The shell is intended to remain alive across every view transition until the controller exits.

### 8.2 View-scoped processes

- Are created for and owned by a view.
- May be interactive and may be shown in separate panes of that view.
- Are stopped when their owning view exits.
- Receive a graceful shutdown request first; if they do not exit promptly, the controller force-kills them.

The `git commit` program and a staged-diff viewer in a commit workflow are examples. When the commit program exits, the diff viewer is shut down as part of leaving the commit view.

### 8.3 Exit reason

The controller must distinguish a program's own exit from an exit resulting from controller-requested shutdown. A sibling process stopped as part of normal view cleanup must not be misreported as an unexpected crash or independently trigger an exit prompt.

## 9. Terminal hosting and portability

The controller must render multiple child terminal sessions in panes and route input to them. It must preserve each process's terminal output state while that process is hidden and restore that state when visible again.

The target experience is consistent across Windows, Linux, and macOS. The selected libraries for the initial implementation are Porta.Pty for pseudo-terminal process hosting and XTerm.NET for terminal emulation. These libraries provide a shared .NET API while handling platform-specific terminal mechanisms internally. Higher-level view, process, and pane logic should remain shared across operating systems.

The initial design does not rely on tmux or another external terminal multiplexer as the required pane backend. This is because Windows must receive the same side-by-side capability and the user prefers one backend everywhere.

## 10. Communication between programs

Only FSLN-owned programs use the structured controller protocol. Managed programs communicate with one another only through the controller. Direct process-to-process communication is out of scope for the initial design. Ordinary external programs such as Vim, shells, Git, and diff viewers use their terminal sessions and do not need to implement this protocol.

Each participating process has a separate duplex named pipe to the controller. The protocol is intentionally minimal for the initial implementation:

- Every message has a length prefix followed by a UTF-8 JSON body.
- The length prefix is a four-byte unsigned big-endian integer counting the UTF-8 bytes in the JSON body.
- The JSON body has an `event` field containing a string event type and a `payload` field containing a string payload.
- Controller commands requested by a managed program use `"event": "command"`; the `payload` contains the controller command text, such as `focus-left`.
- Programs may request any controller command exposed to macros and keybindings. The controller validates and executes the command.
- Command messages are fire-and-forget; the initial protocol does not send acknowledgements or results.
- Additional event names and payload conventions may be added later.

If a requested controller command is not recognized, the controller reports that in its status line. The status line is displayed below the panels; the controller does not require a keypress for this error.

The controller mediates messages, routes them to the intended FSLN-owned program, and coordinates them with process and view lifecycle. Hidden processes may continue to send and receive controller messages while running.

The program-to-controller communication channel is distinct in concept from terminal input/output: terminal I/O carries interactive screen content and user keystrokes; controller messages carry structured requests and events.

## 11. Example workflows

### 11.1 Main view

The controller launches the main view containing:

- A long-lived FSLN solution explorer in the left pane.
- A long-lived shell in the right pane.

Both processes remain alive across other views. The shell keeps its working session while hidden, although it receives no keyboard input while not focused/visible.

### 11.2 Commit view

From the main view, a workflow pushes a commit view containing:

- A view-scoped `git commit` process in the left pane.
- A view-scoped interactive `git diff --staged` process in the right pane.

The user can interact with either pane by focusing it. When `git commit` exits, whether successfully, unsuccessfully, or by cancellation, the controller begins leaving the commit view. It shuts down the diff process gracefully, force-killing it if needed. For a non-zero commit exit, the final commit screen and exit status are shown until a keypress. The controller then pops the commit view and restores the same explorer and shell processes in the main view.

### 11.3 Fullscreen command

A macro can request a named configured view with `:view <view name>`. A one-process view occupies the full terminal presentation. The process lifetime and return behavior follow its view configuration.

## 12. Failure and edge behavior

- A non-zero exit of a view's primary process causes the same return transition as a zero exit, after showing the final screen and status and waiting for a keypress.
- A view-scoped sibling process stopped during cleanup does not create a second user-facing failure prompt.
- A long-lived process found exited while hidden is restarted when next needed; state restoration is not guaranteed.
- A long-lived process that exits while visible retains its last screen, displays the exit status, and waits for a keypress before the view follows its normal exit behavior.
- If a process cannot be started, its panel displays an error message and waits for a keystroke before exiting. For a failed primary process, that acknowledgement then allows the normal view return transition. A failed restart of a hidden persistent process instead waits for a keypress before another restart attempt.
- Controller shutdown requests graceful shutdown of all remaining processes simultaneously, then force-kills any that have not exited after the shared 500 ms deadline.
- Mouse input, clipboard integration, and other terminal features beyond those verified in the evaluation are optional enhancements for the first version.
- A process must not be assigned to multiple visible panes simultaneously.

## 13. Architectural implications

The system naturally separates into these responsibilities:

1. **Controller/session manager:** owns the view stack, lifecycle policies, process registry, transitions, and message mediation.
2. **Terminal host/pane renderer:** creates and manages child terminal sessions, renders their terminal state into panes, forwards input, and handles resize.
3. **Managed applications:** the solution explorer, shells, and workflow programs, each focused on its own job and communicating with other managed programs through the controller.

The terminal host should have a cross-platform boundary. The view and lifecycle model should not depend on Windows-specific or Unix-specific terminal APIs.

## 14. Open design questions

No open design questions remain from the discussions to date. Implementation details may be refined as the system is built and evaluated.

