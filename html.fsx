module Html
open System
open System.Web

/// A Html fragment tree
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

// converts a Html tree to a single node
let private flatten (html: Html) =
    string html |> Html

/// creates an html encoded text fragment
let inline text txt = 
  txt 
  |> HttpUtility.HtmlEncode
  |> Html

/// creates an html encoded text fragment with a formatting string
let textf fmt = Printf.kprintf text fmt

// Often used fragments
let private space = Html " "
let private ``"`` = Html "\""
let private ``="`` = Html "=\""
let attr name = 
    let ``name="`` = Html (" " + name + "=\"")
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

let (<?) l tail =
    if List.isEmpty l then
        tail
    else
        Lst l :: tail


let element tag  =
    let ``<tag`` = els [ ``<``; Html tag ] |> flatten
    let ``</tag>`` = [ els [ ``</``; Html tag; ``>`` ] |> flatten ]

    fun attributes content ->
        Lst (``<tag`` :: attributes <? ``>`` :: content <? ``</tag>`` )

let empty tag =
    let ``<tag`` = els [``<``; Html tag] |> flatten
    let ``/>`` = [``/>``]
    fun  attributes ->
        Lst (``<tag`` ::   attributes <? ``/>``)


module Attributes =
    let cls = attr "class"
    let href  = attr "href"
    let id = attr "id"
    let ``type`` = attr "type"
    let src = attr "src"
    let rel = attr "rel"
    let action = attr "action"
    let name = attr "name"
open Attributes

let ul = element "ul"
let li = element "li"
let h1 = element "h1"
let h2 = element "h2"
let h3 = element "h3"
let h4 = element "h4"

let a = element "a"
let div = element "div"
let span = element "span"
let spant attr txt = span attr [text txt]
let spantf attr fmt = span attr [textf fmt]

let html attr content =
    els [ Html "<!DOCTYPE html>"
          element "html" attr content ]
let head = element "head"
let body = element "body"

let meta = empty "meta"
let title txt = element "title" [] [ text txt ]
let script url = element "script" [ ``type`` "text/javascript" ; src url] []

let link = empty "link"

let stylesheet src = link [ ``type`` "text/css"; rel "stylesheet"; href src ]

let i = element "i"
let br = empty "br" [] |> flatten

let form = element "form" 

module Input =
    let text  = 
        let t = ``type`` "text" |> flatten
        fun attr content -> 
            element "input" (t :: attr) content

    let label =
        let f = attr "for" 
        fun fr attr content ->
            element "label" (f fr :: attr) content 
    
    let submit =
        let t = ``type`` "submit" |> flatten
        fun attr content ->
            element "button" (t :: attr) content

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
        use file = IO.File.Create(path)
        use writer = new IO.StreamWriter(file)
        writeTo writer html