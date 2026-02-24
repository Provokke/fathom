namespace Fathom

type Value =
    | Number of float
    | Text of string

type Expr =
    | Column of string
    | Literal of Value
    | Compare of string * Expr * Expr
    | Like of Expr * string
    | And of Expr * Expr
    | Or of Expr * Expr
    | Not of Expr

type Direction =
    | Ascending
    | Descending

type Query =
    { Columns: string list
      Filter: Expr option
      Order: (string * Direction) option
      Limit: int option }

type Table =
    { Headers: string list
      Rows: string list list }

exception QueryError of string
