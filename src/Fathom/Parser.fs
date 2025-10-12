namespace Fathom

open System
open System.Globalization

type Token =
    | Ident of string
    | Str of string
    | Num of float
    | Op of string
    | Comma
    | Star
    | LParen
    | RParen

module Parser =
    let private keywords =
        set [ "select"; "where"; "and"; "or"; "not"; "like"; "order"; "by"; "asc"; "desc"; "limit" ]

    let private fail message = raise (QueryError message)

    let tokenise (input: string) =
        let scan (start: int) (keep: char -> bool) =
            let mutable j = start

            while j < input.Length && keep input.[j] do
                j <- j + 1

            j

        let rec go i acc =
            if i >= input.Length then
                List.rev acc
            else
                let c = input.[i]

                if Char.IsWhiteSpace c then
                    go (i + 1) acc
                elif c = ',' then
                    go (i + 1) (Comma :: acc)
                elif c = '*' then
                    go (i + 1) (Star :: acc)
                elif c = '(' then
                    go (i + 1) (LParen :: acc)
                elif c = ')' then
                    go (i + 1) (RParen :: acc)
                elif c = '"' || c = '\'' then
                    let close = input.IndexOf(c, i + 1)

                    if close < 0 then
                        fail "unterminated string"

                    go (close + 1) (Str(input.Substring(i + 1, close - i - 1)) :: acc)
                elif Char.IsDigit c || (c = '-' && i + 1 < input.Length && Char.IsDigit input.[i + 1]) then
                    let j = scan (i + 1) (fun ch -> Char.IsDigit ch || ch = '.')
                    let number = Double.Parse(input.Substring(i, j - i), CultureInfo.InvariantCulture)
                    go j (Num number :: acc)
                elif Char.IsLetter c || c = '_' then
                    let j = scan (i + 1) (fun ch -> Char.IsLetterOrDigit ch || ch = '_')
                    go j (Ident(input.Substring(i, j - i)) :: acc)
                elif c = '<' || c = '>' || c = '=' || c = '!' then
                    let two = if i + 1 < input.Length then input.Substring(i, 2) else ""

                    match two with
                    | "<=" | ">=" | "!=" -> go (i + 2) (Op two :: acc)
                    | "<>" -> go (i + 2) (Op "!=" :: acc)
                    | _ when c = '!' -> fail "expected '=' after '!'"
                    | _ -> go (i + 1) (Op(string c) :: acc)
                else
                    fail (sprintf "unexpected character '%c'" c)

        go 0 []

    let private isKeyword (word: string) token =
        match token with
        | Ident name -> String.Equals(name, word, StringComparison.OrdinalIgnoreCase)
        | _ -> false

    let rec private parseOr tokens =
        let left, rest = parseAnd tokens

        match rest with
        | t :: tail when isKeyword "or" t ->
            let right, rest = parseOr tail
            Or(left, right), rest
        | _ -> left, rest

    and private parseAnd tokens =
        let left, rest = parseNot tokens

        match rest with
        | t :: tail when isKeyword "and" t ->
            let right, rest = parseAnd tail
            And(left, right), rest
        | _ -> left, rest

    and private parseNot tokens =
        match tokens with
        | t :: tail when isKeyword "not" t ->
            let inner, rest = parseNot tail
            Not inner, rest
        | LParen :: tail -> parseGroup tail
        | _ -> parseComparison tokens

    and private parseGroup tokens =
        let inner, rest = parseOr tokens

        match rest with
        | RParen :: rest -> inner, rest
        | _ -> fail "expected ')'"

    and private parseComparison tokens =
        let left, rest = parseOperand tokens

        match rest with
        | Op op :: tail ->
            let right, rest = parseOperand tail
            Compare(op, left, right), rest
        | t :: Str pattern :: tail when isKeyword "like" t -> Like(left, pattern), tail
        | _ -> fail "expected a comparison"

    and private parseOperand tokens =
        match tokens with
        | Ident name :: rest when not (keywords.Contains(name.ToLowerInvariant())) -> Column name, rest
        | Str text :: rest -> Literal(Text text), rest
        | Num number :: rest -> Literal(Number number), rest
        | _ -> fail "expected a column or a literal"

    let private parseColumns tokens =
        let rec loop tokens acc =
            match tokens with
            | Ident name :: Comma :: rest -> loop rest (name :: acc)
            | Ident name :: rest -> List.rev (name :: acc), rest
            | _ -> fail "expected a column name"

        match tokens with
        | Star :: rest -> [], rest
        | _ -> loop tokens []

    let private parseOrder tokens =
        match tokens with
        | o :: b :: Ident name :: tail when isKeyword "order" o && isKeyword "by" b ->
            (match tail with
             | d :: rest when isKeyword "desc" d -> Some(name, Descending), rest
             | a :: rest when isKeyword "asc" a -> Some(name, Ascending), rest
             | _ -> Some(name, Ascending), tail)
        | o :: _ when isKeyword "order" o -> fail "expected 'order by <column>'"
        | _ -> None, tokens

    let parse (input: string) : Query =
        let afterSelect =
            match tokenise input with
            | t :: rest when isKeyword "select" t -> rest
            | _ -> fail "a query starts with 'select'"

        let columns, rest = parseColumns afterSelect

        let filter, rest =
            match rest with
            | t :: tail when isKeyword "where" t ->
                let expr, rest = parseOr tail
                Some expr, rest
            | _ -> None, rest

        let order, rest = parseOrder rest

        let limit, rest =
            match rest with
            | l :: Num n :: tail when isKeyword "limit" l && n >= 0.0 -> Some(int n), tail
            | l :: _ when isKeyword "limit" l -> fail "expected 'limit <count>'"
            | _ -> None, rest

        if not (List.isEmpty rest) then
            fail "unexpected input after the end of the query"

        { Columns = columns
          Filter = filter
          Order = order
          Limit = limit }
