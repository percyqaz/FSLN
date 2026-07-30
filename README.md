# FSLN

Is a terminal-based tool general purpose solution explorer, primarily aimed to help with organising F# projects  
It can also handle my C# projects and random folders of scripts just fine

- Hardly any IDEs have good built-in support for reordering F# files or folders up or down  
- No IDE I've ever used has built-in support for reordering of C# projects, files, folders

File order matters in F# projects so I wanted a fast tool for reordering them (regular annoyance: manually editing .fsproj files)  
File order doesn't matter to C# programmers or IDEs but I like organising topologically as if I'm writing F#, to navigate the structure better

Features:
- Run in a folder, it detects a .sln or .slnx and opens all projects
- With configuration you can open any directory as a 'project' or 'solution' even if not .NET
- You can navigate, search, rename, reorder all files in a tree
  - Reordering data is stored in a .fsln directory for projects and for non-F# files
  - Reordering is applied directly in .fsproj for F# projects
- Searching by filename, git integration
- Vim-like bindings and keys that let me hot-wire it to do anything
  - Every action on state is a command e.g. `:edit`, `:move_up`, `:expand`
  - All hotkeys are macros for internal commands, or shell commands that use a `!` prefix
  - What happens when you run `:edit` on a file is pluggable by file type, set this to your editor of choice
  - These bindings end up very composable and reusable, I have a small .fslnrc full of binds like I would a .vimrc  
    Example: `:!git add $GITPATH` runs `git add ` in shell + the path of your selection relative to the detected git repo  
    Example: typing `:bind v = :!vim $` makes `v` open the selected file (or folder) in vim and returns you on exit
  
It it made specifically for me, your mileage may vary

I daily drive this, current workflow for F# projects is:
- Open project in FSLN, it has my todo list, formatting on commit, other doodads
- Hit enter on any file to open JetBrains Rider where I do my editing as normal
- `WIN+BACKTICK` brings up my terminal at any time for reordering/moving folders/etc, I write my commits in here, update todo list, re-enter Rider

## Installing

1. Clone the repo

2. Run `./update.sh` to install as a dotnet tool  
   Needs dotnet 10 installed and googling skills for when your dotnet tools are inevitably not found in your path

3. Run with `fsln`
