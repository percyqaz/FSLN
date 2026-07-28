namespace FSLN

open System.Collections.Generic
open System.IO

type OrderFile(path: string) =

    let load_file () =
        try
            ResizeArray(File.ReadAllLines(path))
        with :? FileNotFoundException ->
            ResizeArray()

    let entries = load_file()

    member this.Save() : unit = File.WriteAllLines(path, entries)

    member this.Contains(id: string) : bool = entries.Contains(id)

    member this.StorePreservingOrder(ids: string seq) : unit =
        for id in ids do
            entries.Remove(id) |> ignore

        entries.AddRange(ids)

    member this.PlaceBefore(ids: string seq, relative_to: string) : unit =
        let index = entries.IndexOf(relative_to)

        if index = -1 then
            failwith "Should have ensured .Contains(relative_to) was true before use"

        for id in ids do
            entries.Remove(id) |> ignore

        entries.InsertRange(index, ids)

    member this.PlaceAfter(ids: string seq, relative_to: string) : unit =
        let index = entries.IndexOf(relative_to)

        if index = -1 then
            failwith "Should have ensured .Contains(relative_to) was true before use"

        for id in ids do
            entries.Remove(id) |> ignore

        entries.InsertRange(index + 1, ids)

    member this.Sort(items: ResizeArray<'T>, by: 'T -> string) : unit =
        let mutable at_least_one_item_present = false
        let mutable not_all_items_present = false

        let inline sortkey_from_item (item: 'T) : int * string =
            let id = by item
            let index = entries.IndexOf(id)

            if index <> -1 then at_least_one_item_present <- true else not_all_items_present <- true

            index, id

        let inline create_sortkey_map () : Dictionary<'T, int * string> =
            items |> Seq.map(fun i -> KeyValuePair(i, sortkey_from_item(i))) |> Dictionary<'T, int * string>

        let sortkey_map = create_sortkey_map()

        let sorted = items |> Seq.sortBy(fun x -> sortkey_map.[x]) |> Array.ofSeq
        items.Clear()
        items.AddRange(sorted)

        if at_least_one_item_present && not_all_items_present then
            this.StorePreservingOrder(items |> Seq.map by)
