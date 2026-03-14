module Fathom.Tests.Program

open Fathom

let private source =
    String.concat
        "\n"
        [ "name,city,age,role"
          "Mia,Perth,34,engineer"
          "Jack,Hobart,28,designer"
          "\"O'Brien, Kate\",Perth,41,engineer"
          "Noah,Darwin,19,apprentice"
          "Ruby,Hobart,52,\"lead \"\"acting\"\"\"" ]

let private table = Csv.parse source

let mutable private failures = 0
let mutable private checks = 0

let private check (name: string) (expected: 'a) (actual: 'a) =
    checks <- checks + 1

    if expected <> actual then
        failures <- failures + 1
        eprintfn "FAIL %s\n  expected %A\n  actual   %A" name expected actual

let private names text =
    (Engine.query table text).Rows |> List.map List.head

let private rejects name text =
    let rejected =
        try
            Engine.query table text |> ignore
            false
        with QueryError _ ->
            true

    check name true rejected

[<EntryPoint>]
let main _ =
    check "headers" [ "name"; "city"; "age"; "role" ] table.Headers
    check "row count" 5 table.Rows.Length
    check "quoted comma" "O'Brien, Kate" (table.Rows.[2].[0])
    check "escaped quote" "lead \"acting\"" (table.Rows.[4].[3])

    check "select star" 5 (Engine.query table "select *").Rows.Length
    check "projection" [ "city"; "name" ] (Engine.query table "select city, name").Headers
    check "numeric filter" [ "Mia"; "O'Brien, Kate"; "Ruby" ] (names "select name where age > 30")
    check "string filter" [ "Jack"; "Ruby" ] (names "select name where city = 'hobart'")
    check "and" [ "Mia" ] (names "select name where city = \"Perth\" and age < 40")
    check "or" [ "Jack"; "Noah"; "Ruby" ] (names "select name where city = 'Hobart' or age < 20")
    check "not" [ "Jack"; "Noah"; "Ruby" ] (names "select name where not city = 'Perth'")
    check "precedence" [ "Mia"; "O'Brien, Kate"; "Noah" ] (names "select name where age < 20 or city = 'Perth' and age > 30")
    check "grouping" [ "O'Brien, Kate" ] (names "select name where (age < 20 or city = 'Perth') and age > 40")
    check "not equal" 3 (names "select name where city <> 'Perth'").Length
    check "like" 2 (Engine.query table "select role where role like 'eng%'").Rows.Length
    check "like underscore" [ "Mia" ] (names "select name where name like 'M_a'")
    check "order asc" [ "Noah"; "Jack"; "Mia"; "O'Brien, Kate"; "Ruby" ] (names "select name order by age")
    check "order desc limit" [ "Ruby"; "O'Brien, Kate" ] (names "select name order by age desc limit 2")
    check "order by text" [ "Darwin"; "Hobart"; "Hobart"; "Perth"; "Perth" ] (names "select city order by city asc")
    check "limit zero" [] (names "select name limit 0")
    check "case insensitive keywords" [ "Noah" ] (names "SELECT name WHERE age <= 19")

    rejects "unknown column" "select nope"
    rejects "missing select" "name where age > 3"
    rejects "trailing input" "select name limit 2 extra"
    rejects "unterminated string" "select name where city = 'Perth"
    rejects "dangling operator" "select name where age >"

    let rendered = Engine.render (Engine.query table "select name, age where age < 30 order by age")
    check "render" "name  age\n----  ---\nNoah  19\nJack  28" rendered

    let roundTrip = Csv.parse (Csv.write table)
    check "csv round trip" table roundTrip

    printfn "%d checks, %d failed" checks failures
    if failures = 0 then 0 else 1

