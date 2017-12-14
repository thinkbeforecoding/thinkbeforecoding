module Html
open System
open System.Web
open System.Web

let attr (name, value) = " " + name + "=\"" + HttpUtility.HtmlAttributeEncode value + "\""  
let els x = String.Concat (x: string list)
let element tag attributes content =
    [ yield "<"
      yield tag
      yield! attributes |> List.map attr
      yield ">"
      yield! (content : _ list)
      yield "</"
      yield tag
      yield ">"
    ] |> String.Concat
    
let ul = element "ul"
let li = element "li"
let h1 = element "h1"

let a = element "a"
let div = element "div"
let span = element "span"
let text = HttpUtility.HtmlEncode
let spant attr txt = span attr [ text txt]

let cls n = "class", n