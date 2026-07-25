namespace FSLN

type FilteredTreeFile = { Original: FileTreeFile }

and FilteredTreeFolder = { Original: FileTreeFolder; Children: FilteredTreeEntry list }

and FilteredTreeEntry =
    | FFile of FilteredTreeFile
    | FFolder of FilteredTreeFolder

and FilteredProject = { Original: Project; Children: FilteredTreeEntry list }

type FilteredSolution = { Original: Solution; Projects: FilteredProject list }
