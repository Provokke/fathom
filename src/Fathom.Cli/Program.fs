module Fathom.Cli.Program

open System
open System.IO
open Fathom

let private usage =
    "usage: fathom [--csv] <file.csv | -> \"select ... where ... order by ... limit ...\""

[<EntryPoint>]
let main argv =
    let asCsv = argv |> Array.contains "--csv"
    let args = argv |> Array.filter (fun arg -> arg <> "--csv")

    match args with
    | [| path; text |] ->
        try
            let source = if path = "-" then Console.In.ReadToEnd() else File.ReadAllText path
            let result = Engine.query (Csv.parse source) text
            printfn "%s" (if asCsv then Csv.write result else Engine.render result)
            0
        with
        | QueryError message ->
            eprintfn "fathom: %s" message
            1
        | :? IOException as error ->
            eprintfn "fathom: %s" error.Message
            1
    | _ ->
        eprintfn "%s" usage
        2

