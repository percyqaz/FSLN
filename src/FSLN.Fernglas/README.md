# Fernglas

Is a terminal-based file browser designed for my personal use:
- Can install it easily anywhere, and it will just work with the same experience on Linux/macOS/Windows
- Interlinks with my other terminal-based tools for a very smooth workflow

Features:
- View contents of a folder as a scrollable list
- Can ascend/descend folders
- Can open another folder in a split pane view
- All typical file operations (delete, copy, rename, add)
- Can move files and folders from one side pane to the other
- Git status support/highlighting which files are changed
- All actions driven by vim-like commands, with vim-like pluggable keybinds making it very flexible
- Execute shell commands in current directory `:!` prefix, like vim
- Use `$` in shell commands to represent current selected folder/file

## Installing

1. Clone the repo

2. Run `./update.sh` to install as a dotnet tool  
   Needs dotnet 10 installed and googling skills for when your dotnet tools are inevitably not found in your path

3. Run with `fernglas`
