namespace Fathom

open System
open System.Globalization
open System.Text.RegularExpressions

module Engine =
    let private toValue (cell: string) =
        match Double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture) with
        | true, number -> Number number
        | _ -> Text cell

    let private columnIndex (table: Table) (name: string) =
        let found =
            table.Headers
            |> List.tryFindIndex (fun header -> String.Equals(header, name, StringComparison.OrdinalIgnoreCase))

        match found with
        | Some index -> index
        | None -> raise (QueryError(sprintf "unknown column '%s'" name))

    let private cell (row: string list) index =
        match List.tryItem index row with
        | Some value -> value
        | None -> ""

    let private compareValues left right =
        match left, right with
        | Number a, Number b -> compare a b
        | Text a, Text b -> String.Compare(a, b, StringComparison.OrdinalIgnoreCase)
        | Number _, Text _ -> -1
        | Text _, Number _ -> 1

    let private asText value =
        match value with
        | Text text -> text
        | Number number -> number.ToString(CultureInfo.InvariantCulture)

    let private likeRegex (pattern: string) =
        let body = Regex.Escape(pattern).Replace("%", ".*").Replace("_", ".")
        Regex("^" + body + "$", RegexOptions.IgnoreCase ||| RegexOptions.Singleline)

    let private operand table row expr =
        match expr with
        | Column name -> toValue (cell row (columnIndex table name))
        | Literal value -> value
        | _ -> raise (QueryError "expected a column or a literal")

    let rec private eval table row expr =
        match expr with
        | Compare(op, left, right) ->
            let order = compareValues (operand table row left) (operand table row right)

            (match op with
             | "=" -> order = 0
             | "!=" -> order <> 0
             | "<" -> order < 0
             | "<=" -> order <= 0
             | ">" -> order > 0
             | ">=" -> order >= 0
             | other -> raise (QueryError(sprintf "unknown operator '%s'" other)))
        | Like(target, pattern) -> (likeRegex pattern).IsMatch(asText (operand table row target))
        | And(left, right) -> eval table row left && eval table row right
        | Or(left, right) -> eval table row left || eval table row right
        | Not inner -> not (eval table row inner)
        | Column _
        | Literal _ -> raise (QueryError "expected a condition")

    let run (table: Table) (query: Query) : Table =
        let filtered =
            match query.Filter with
            | Some expr -> table.Rows |> List.filter (fun row -> eval table row expr)
            | None -> table.Rows

        let ordered =
            match query.Order with
            | Some(name, direction) ->
                let index = columnIndex table name
                let sign = if direction = Descending then -1 else 1

                filtered
                |> List.sortWith (fun a b -> sign * compareValues (toValue (cell a index)) (toValue (cell b index)))
            | None -> filtered

        let limited =
            match query.Limit with
            | Some count -> List.truncate count ordered
            | None -> ordered

        match query.Columns with
        | [] -> { table with Rows = limited }
        | names ->
            let indexes = names |> List.map (columnIndex table)

            { Headers = indexes |> List.map (fun index -> List.item index table.Headers)
              Rows = limited |> List.map (fun row -> indexes |> List.map (cell row)) }

    let query (table: Table) (text: string) = run table (Parser.parse text)

    let render (table: Table) =
        let widths =
            table.Headers
            |> List.mapi (fun index header ->
                table.Rows
                |> List.fold (fun width row -> max width (cell row index).Length) header.Length)

        let line (row: string list) =
            widths
            |> List.mapi (fun index width -> (cell row index).PadRight(width))
            |> String.concat "  "
            |> fun text -> text.TrimEnd()

        let rule = widths |> List.map (fun width -> String('-', width)) |> String.concat "  "
        line table.Headers :: rule :: List.map line table.Rows |> String.concat "\n"
