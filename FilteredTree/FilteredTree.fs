namespace FSLN

type FilteredTreeFile = { Original: FileTreeFile }

and FilteredTreeFolder = { Original: FileTreeFolder; Children: FilteredTreeEntry array }

and FilteredTreeEntry =
    | FFile of FilteredTreeFile
    | FFolder of FilteredTreeFolder

and FilteredProject = { Original: Project; Children: FilteredTreeEntry array }

type FilteredSolution = { Original: Solution; Projects: FilteredProject array }
