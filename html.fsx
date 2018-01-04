module Html
open System
open System.Web

type Html =
  | Html of string
  | Lst of Html list
  with
    override this.ToString() =
        let rec write (sb : Text.StringBuilder) =
            function
            | Html str -> sb.Append(str)
            | Lst lst -> lst |> List.fold write sb
        
        this
        |> write (Text.StringBuilder())
        |> string



let inline text txt = 
  txt 
  |> HttpUtility.HtmlEncode
  |> Html

let textf fmt = Printf.kprintf text fmt

let private space = Html " "
let private ``"`` = Html "\""
let private ``="`` = Html "=\""
let attr name = 
    let ``name="`` = Lst [ Html (" " + name + "=\"") ]
    fun value ->
    Lst [ ``name="``
          Html (HttpUtility.HtmlAttributeEncode value) 
          ``"`` ]
let (:=) name value = 
    Lst [ space; Html name; ``="``
          Html (HttpUtility.HtmlAttributeEncode value) 
          ``"`` ]
let inline els x = Lst x

let private ``<`` = Html "<"
let private ``</`` = Html "</"
let private ``>`` = Html ">"
let private ``/>`` = Html "/>"

let element tag  =
    let ``<tag`` = els [ ``<``; Html tag ]
    let ``</tag>`` = els [ ``</``; Html tag; ``>`` ]

    fun attributes content ->
        Lst [ yield ``<tag``
              if not (List.isEmpty attributes) then  
                  yield Lst attributes
              yield ``>``
              if not (List.isEmpty content) then
                  yield Lst content
              yield ``</tag>``
            ]
let empty tag =
    let ``<tag`` = els [``<``; Html tag]
    fun  attributes ->
    Lst [ ``<tag``
          Lst attributes 
          ``/>``
        ]
     
let ul = element "ul"
let li = element "li"
let h1 = element "h1"

let a = element "a"
let div = element "div"
let span = element "span"
let spant attr txt = span attr [text txt]

let html attr content =
    els [
            Html "<!DOCTYPE html>"
            element "html" attr content ]
let head = element "head"
let body = element "body"

let meta = empty "meta"
let title txt = element "title" [] [ text txt ]
let script src = element "script" ["type" := "text/javascript" ; "src" := src] []

let link = empty "link"

let stylesheet src = link [ "type" := "text/css"; "rel" := "stylesheet"; "href" := src ]

let i = element "i"
let br = empty "br" [] 

module Attributes =
    let cls = attr "class"

    let href  = attr "href"

    let id = attr "id"

module Entities =
    let copy = Html "&copy;"
    let nbsp = Html "&nbsp;"

module Html =
    let rec writeTo (writer: IO.TextWriter) =
        function
        | Html str -> writer.Write(str)
        | Lst lst -> lst |> List.iter (writeTo writer)

    let flatten (html: Html) =
        string html |> Html

    let rec save path html =
        use file = IO.File.OpenWrite(path)
        use writer = new IO.StreamWriter(file)
        writeTo writer html