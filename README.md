<div align="center">

# fathom

Query CSV files with a small SQL-like language. Written in F#, with nothing beyond the .NET SDK.

![License](https://img.shields.io/badge/LICENSE-MIT-blue?style=for-the-badge)  ![Language](https://img.shields.io/badge/F%23-378bba?style=for-the-badge&logo=fsharp&logoColor=white)  ![Tests](https://img.shields.io/badge/TESTS-28-brightgreen?style=for-the-badge)

Refer to [LICENSE](LICENSE) for the legal bits.

</div>

## What's in it

- **CSV reader and writer** that handle quoted fields with embedded commas and doubled quotes. Quoted fields that span several lines aren't supported.
- **A hand-written tokeniser and recursive descent parser** for one statement shape: `select` a list of columns or `*`, then an optional `where`, `order by <column> [asc|desc]` and `limit <count>`.
- **Conditions** with `=`, `!=`, `<>`, `<`, `<=`, `>`, `>=`, `like` (`%` and `_` wildcards), `and`, `or`, `not` and brackets. `not` binds tighter than `and`, which binds tighter than `or`.
- **Typing by cell**: anything that parses as a number compares as a number, everything else compares as text ignoring case. Column names and keywords ignore case too.
- **Output** as an aligned table or as CSV, and a CLI that reads a file or standard input.

```fsharp
open Fathom

let table = Csv.parse (System.IO.File.ReadAllText "people.csv")
let result = Engine.query table "select name order by age limit 3"
printfn "%s" (Engine.render result)
```

## 🏗️ Architecture

```
 text ──> Csv.parse ──> Table { Headers; Rows }
                              │
 "select ..." ──> Parser.tokenise ──> Token list
                       │
                       v
                  Parser.parse ──> Query { Columns; Filter; Order; Limit }
                                         │
                                         v
                   Engine.run: filter ─> sort ─> limit ─> project
                                         │
                                         v
                      Engine.render (table)  |  Csv.write (CSV)

 src/Fathom/        Types.fs  Csv.fs  Parser.fs  Engine.fs
 src/Fathom.Cli/    Program.fs  (exe)
 tests/Fathom.Tests/  Tests.fs  (console program)
```

- `Types.fs` holds the data model: `Value`, `Expr`, `Query`, `Table` and the `QueryError` exception.
- `Parser.fs` tokenises into an immutable list and parses it by recursive descent. Anything malformed raises `QueryError` with a short message.
- `Engine.fs` evaluates the `where` tree per row, does a stable sort and truncates for `limit`. `like` is turned into an anchored, case-insensitive regex.
- The whole file is read into memory as lists of strings. That's comfortable into the tens of megabytes and not designed for more.
- There are no aggregates, joins or expressions in the select list.

## 🚀 Quick start

```
dotnet run --project tests/Fathom.Tests
dotnet run --project src/Fathom.Cli -- people.csv "select name, age where city = 'Perth' order by age desc"
```

Add `--csv` for CSV output, and pass `-` as the file name to read standard input. With a three-row `people.csv`, the query above prints:

```
name           age
-------------  ---
O'Brien, Kate  41
Mia            34
```

## 🧪 Testing

28 checks, all passing. The test program is a plain console app with a small `check` helper that prints `28 checks, 0 failed` and exits non-zero on any failure.

- CSV: headers, row count, a quoted comma, an escaped quote and a write/parse round trip.
- Queries: `select *`, projection, numeric and string filters, `and`, `or`, `not`, precedence, brackets, `<>`, `like` with `%` and `_`, ordering ascending, descending and by text, `limit` including zero, and case-insensitive keywords.
- Rejections: unknown column, missing `select`, trailing input, an unterminated string and a dangling operator.
- Rendering: the aligned table output.

## Licence

MIT.

