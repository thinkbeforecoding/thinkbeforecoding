[<AutoOpen>]

module Categories
type Category =
    | NoCategory
    | Personal 
    | Design
    | DomainDrivenDesign
    | NetFramework
    | AspNet
    | Linq
    | Duck
    | WP7
    | Agile
    | NuRep
    | CQRS
    | FSharp
    | EventSourcing

module Categories =
  let title = function
  | DomainDrivenDesign -> "Domain Driven Desing"
  | NetFramework -> ".Net Framework"
  | AspNet -> "Asp.net"
  | FSharp -> "F#"
  | EventSourcing -> "Event Sourcing"
  | c -> string c

  let name = function
  | NetFramework -> "Net-Framework"
  | AspNet -> "Aspnet" 
  | FSharp -> "FSharp"
  | cat -> (title cat).Replace(" ","-") 

  let adapt = function
  | "F" -> "FSharp"
  | cat -> cat
  
  let categories =
    Reflection.FSharpType.GetUnionCases(typeof<Category>)
    |> Array.map (fun c ->Reflection.FSharpValue.MakeUnion(c,[||]) |> unbox)
    |> Array.filter ((<>) NoCategory)
    |> Array.toList
