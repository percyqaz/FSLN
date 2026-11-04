# Fernglas

Is a terminal-based file browser designed for my personal use:
- Can install it easily anywhere, and it will just work with the same experience on Linux/macOS/Windows
- Interlinks with my other terminal-based tools for a very smooth workflow

Written over the course of about 36 hours without using AI (since I am currently on a wave of improving my baseline efficiency)

This tool now acts as my main "entry point" when I am in my terminal  
I can navigate anywhere and then open [FSLN](https://github.com/percyqaz/FSLN) as my solution/code explorer  
I can open [Tedium](https://github.com/percyqaz/Tedium) as my todo list tracker as I work (and also when inside FSLN)  
Incidentally I am also learning German which I use [Loana](https://github.com/percyqaz/Loana) for, also embedded in a terminal

Features:
- View contents of a folder as a scrollable list
- Can ascend/descend folders
- Can open another folder in a split pane view
- Main pane's directory is written to a file  
  On exit my bash profile is set to `cd` to that directory for using it as a graphical navigation tool
- All typical file operations (delete, copy, rename, add)
- Can move files and folders from one side pane to the other
- Per-pane git status and file highlighting
- All actions driven by vim-like commands, with vim-like pluggable keybinds making it very flexible
- Execute shell commands in current directory `:!` prefix, like vim
- Use `$` in shell commands to represent current selected folder/file

## Installing

1. Clone the repo

2. Run `./update.sh` to install as a dotnet tool  
   Needs dotnet 10 installed and googling skills for when your dotnet tools are inevitably not found in your path

3. Run with `fernglas`
