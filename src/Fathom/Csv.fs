namespace Fathom

open System.Text

module Csv =
    let parseLine (line: string) =
        let fields = ResizeArray<string>()
        let current = StringBuilder()
        let mutable quoted = false
        let mutable i = 0

        while i < line.Length do
            let c = line.[i]

            if quoted then
                if c = '"' && i + 1 < line.Length && line.[i + 1] = '"' then
                    current.Append('"') |> ignore
                    i <- i + 1
                elif c = '"' then
                    quoted <- false
                else
                    current.Append(c) |> ignore
            elif c = '"' then
                quoted <- true
            elif c = ',' then
                fields.Add(current.ToString())
                current.Clear() |> ignore
            else
                current.Append(c) |> ignore

            i <- i + 1

        fields.Add(current.ToString())
        List.ofSeq fields

    let parse (text: string) =
        let lines =
            text.Split('\n')
            |> Array.map (fun line -> line.TrimEnd('\r'))
            |> Array.filter (fun line -> line.Length > 0)
            |> List.ofArray

        match lines with
        | [] -> { Headers = []; Rows = [] }
        | header :: rest ->
            { Headers = parseLine header |> List.map (fun h -> h.Trim())
              Rows = rest |> List.map parseLine }

    let private escape (field: string) =
        if field.IndexOfAny([| ','; '"'; '\n' |]) >= 0 then
            "\"" + field.Replace("\"", "\"\"") + "\""
        else
            field

    let write (table: Table) =
        table.Headers :: table.Rows
        |> List.map (fun row -> row |> List.map escape |> String.concat ",")
        |> String.concat "\n"
