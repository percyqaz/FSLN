namespace FSLN

[<RequireQualifiedAccess>]
type Mode =
    | Normal of NormalMode
    | Search of SearchMode
    | Git of GitMode

    member this.Solution: Solution =
        match this with
        | Normal nm -> nm.Solution
        | Search sm -> sm.Solution.Original
        | Git gm -> gm.Solution.Original

    member this.Selection: Selection =
        match this with
        | Normal nm -> nm.Selected
        | Search sm -> sm.Selected.ToSelection()
        | Git gm -> gm.Selected.ToSelection()

    member this.ToggleGitMode(git_status: GitStatus option) : Mode =
        match git_status with
        | Some git_status ->
            match this with
            | Normal nm -> Git(GitMode.Create(nm, "", git_status))
            | Search sm -> Git(GitMode.Create(sm.ToNormalMode(), sm.Query, git_status))
            | Git gm ->
                if gm.Query <> "" then
                    Search(SearchMode.Create(gm.ToNormalMode(), gm.Query))
                else
                    Normal(gm.ToNormalMode())
        | None -> this

    member this.Update(query: string, git_status: GitStatus option) : Mode =
        match this with
        | Normal nm -> if query <> "" then Search(SearchMode.Create(nm, query)) else Normal(nm)
        | Search sm -> if query <> "" then Search(sm.Update(query)) else Normal(sm.ToNormalMode())
        | Git gm ->
            match git_status with
            | Some git_status -> Git(gm.Update(query, git_status))
            | None -> Normal(gm.ToNormalMode())

    member this.Reload(workspace: Workspace) : Mode =
        match this with
        | Mode.Normal nm -> Mode.Normal(nm.Reload(workspace))
        | Mode.Search sm -> Mode.Search(sm.Reload(workspace))
        | Mode.Git gm -> Mode.Git(gm.Reload(workspace))

    member this.AutoReload(workspace: Workspace) : Mode =
        match this with
        | Mode.Normal nm -> Mode.Normal(nm.AutoReload(workspace))
        | Mode.Search sm -> Mode.Search(sm.AutoReload(workspace))
        | Mode.Git gm -> Mode.Git(gm.AutoReload(workspace))
