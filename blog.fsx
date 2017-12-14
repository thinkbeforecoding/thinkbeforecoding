#r "packages/Fake/tools/FakeLib.dll"
#load "packages/FSharp.Formatting/FSharp.Formatting.fsx"
#load "html.fsx"
#load "posts.fsx"
#load "feed.fsx"
open System
open Fake
open Posts

open FSharp.Literate


let template templateStr replacements =
    let rx = Text.RegularExpressions.Regex("\{([^}]+)\}")
    rx.Replace(templateStr, fun (m: Text.RegularExpressions.Match) ->
        match Map.tryFind (m.Groups.[1].Value) replacements with
        | Some v -> v
        | None -> "")

type FormattedPost = {
  Link: Link
  Content: string
  Tooltips: string
  Date: DateTime
  FileName: string
  Next:  Link option
  Previous: Link option 
}
and Link = {
  Text: string
  Href: string
}

let processHtmlPost post  =
  let source = Path.posts </> post.Name + ".html"
  let dest = post.Name + ".html"
  let html = IO.File.ReadAllText source
  let document = html.Replace("http://www.thinkbeforecoding.com/","/")
  { Content = document
    Tooltips = ""
    Link = { Text = post.Title; Href = post.Url }
    Date = post.Date
    FileName = dest
    Next =  None
    Previous =None }


let processScriptPost post =
  let source = Path.posts </> post.Name + ".fsx"
  let dest = post.Name + ".html"
  let doc = Literate.ParseScriptFile(source)

  let doc' = FSharp.Literate.Literate.FormatLiterateNodes(doc, OutputKind.Html, "", true, true)
  let output = Formatting.format doc'.MarkdownDocument true OutputKind.Html
  { Content = output
    Tooltips = doc'.FormattedTips
    Link =  { Text = post.Title; Href = post.Url }
    Date = post.Date 
    FileName = dest
    Next = None
    Previous = None }

let processPost post =
  if fileExists (Path.posts </> post.Name + ".fsx") then
    processScriptPost post
  else
    processHtmlPost post

open Html
let copyright = div [cls "copyright"] ["&copy; 2017 Jérémie Chassaing / thinkbeforecoding"]

let save outputDir templateStr categories post =
  let link l = a ["href", "/post/" + l.Href] [ Html.text l.Text]
  let prev = post.Previous |> Option.map link
  let next = post.Next |> Option.map link
  let links =
      match prev, next with
      | None, None -> ""
      | Some p, None -> "&lt;&nbsp;" + p + "&nbsp;|"
      | None, Some n -> "|&nbsp;" + n + "&nbsp;&gt;"
      | Some p, Some n -> "&lt;&nbsp;" + p + "&nbsp;|&nbsp;" + n + "&nbsp;&gt;"

  let content =
    els [
      h1 [cls "title"] [ link post.Link ]
      div [cls "date"] 
        [ post.Date.ToString("yyyy-MM-ddTHH:mm-ss")
          " / jeremie chassaing"]
      post.Content
      " "
      post.Tooltips
    ]
  let footer =
    els [
      div [cls "links"] [links]
      copyright
    ]
  [ "document", content
    "page-title", post.Link.Text
    "categories", categories
    "footer", footer ]

  |> Map.ofList
  |> template templateStr
  |> fun r -> IO.File.WriteAllText(outputDir </> post.FileName, r)


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

  let categoriesHtml =
    div ["class", "categoris"] [
      spant [cls "k"] "type "
      spant [cls "t"] "Categories "
      spant [cls "o"] "="
      ul [] [
        for c in categories ->
          li [] [
            spant [cls "o"] "| "
            a ["href", "/category/" + name c ] [text (title c)]
          ] ] ]
 
  let processCategory outputDir templateStr cat =
    let dest = outputDir </> name cat + ".html"
    let catPosts =
      posts
      |> List.filter (fun p -> p.Category = cat)
      |> List.sortByDescending (fun p -> p.Date)
    let content =
      els [
        h1 [cls "title"] [text (title cat)]
        ul ["class", "category"]
          [ for p in catPosts ->
               li [] [ 
                    p.Date.ToString("yyyy-MM-ddTHH:mm:ss")
                    " |> "
                    a ["href", "/post/" + p.Url ] [text p.Title] ] ]
      ]
    let footer = copyright
    [ "document", content
      "page-title", title cat
      "categories", categoriesHtml
      "footer", footer ]
    |> Map.ofList
    |> template templateStr
   
    |> fun r -> IO.File.WriteAllText(dest, r)


let prevnext f l =
  let rec loop p result =
    function
    | [] -> result |> List.rev
    | [ c ] -> List.rev (f (Some p) c None :: result)
    | c :: n :: tail -> loop n (f (Some p) c (Some n) :: result) (n :: tail)
  match l with
  | [] -> []
  | [ c ] -> [f None c None]
  | c :: n :: tail -> loop n  [f None c (Some n)] (n :: tail) 

let templateStr = IO.File.ReadAllText(__SOURCE_DIRECTORY__ </> "template.html")

let formattedPosts =
  posts
  |> List.map processPost
  |> List.sortByDescending (fun p -> p.Date)
  |> prevnext (fun p c n -> { c with Next = n |> Option.map (fun n -> n.Link); Previous = p |> Option.map (fun p -> p.Link)})
  

if directoryExists Path.out then
  DeleteDir Path.out
CreateDir Path.outPosts
formattedPosts
|> List.iter (save Path.outPosts templateStr Categories.categoriesHtml)

CreateDir Path.feed
formattedPosts
|> List.map (fun p -> Feed.entry p.Link.Text p.Link.Href p.Date p.Content)
|> Feed.feed
|> string
|> fun f -> IO.File.WriteAllText(Path.atom, f)


if directoryExists Path.categories then
  DeleteDir Path.categories
CreateDir Path.categories
Categories.categories
|> List.iter (Categories.processCategory Path.categories templateStr)
CopyDir Path.outmedia Path.media allFiles
CopyDir Path.outcontent Path.content allFiles

formattedPosts
|> List.head
|> fun p -> Path.outPosts </> p.FileName
|> CopyFile (Path.out </> "index.html")

