#I ".paket/load/netcoreapp3.1/full"
#I "packages/full/fsharp.formatting/lib/netstandard2.0"
#load "FSharp.Compiler.Service.fsx"
#r "FSharp.Formatting.Common.dll"
#r "FSharp.Formatting.Markdown.dll"
#r "FSharp.Formatting.Literate.dll"
#r "FSharp.Formatting.CodeFormat.dll"
#r "FSharp.Formatting.ApiDocs.dll"
#load "Fable.React.fsx"
#load "FSharp.Text.RegexProvider.fsx"
#load "posts.fsx"
#load "feed.fsx"

let md5 = new System.Security.Cryptography.MD5CryptoServiceProvider()

// #nowarn "86"
open FSharp.Formatting.Literate
open FSharp.Formatting.Markdown

open Fable.React
open Fable.React.Props
open Posts
open Categories
// open Fake.IO
// open Fake.Core
open System

// HACK: force usage of Fsharp.Compiler.Services
// or the indirect reference from FSharp.Literate will fail to loadlet dummy (pos: FSharp.Compiler.Range.pos) =
let dummy (pos: FSharp.Compiler.Range.pos) =
    pos.Column
FSharp.Compiler.Range.mkPos 1 1 |> dummy

// HACK: Force usage of Fable.Core
// or the indirect reference from Fable.React will fail to load
typeof<Fable.Core.EraseAttribute>


let (</>) x y =IO.Path.Combine(x,y)

let (./) (x: string) (y: string) =
 match x.EndsWith("/"), y.StartsWith("/") with
 | false, false -> x + "/" + y
 | true, true -> x + y.Substring(1)
 | _ -> x + y

module File =
  let exists path = IO.File.Exists(path)

module Directory =
  let delete path = 
    if IO.Directory.Exists path then
      let dir = IO.DirectoryInfo(path)
      dir.Delete(true)

  let ensure path =
    if not(IO.Directory.Exists(path)) then
      IO.Directory.CreateDirectory(path) |> ignore

  let copy source dest =
    let srcDir = IO.DirectoryInfo(source)
    let dstDir = IO.Directory.CreateDirectory(dest)

    let rec loop (src: IO.DirectoryInfo) (dst: IO.DirectoryInfo) =
      for file in src.GetFiles() do
        let path = dst.FullName </> file.Name
        file.CopyTo(path, true) |> ignore

      for srcsubdir in src.GetDirectories() do
        let dstsubdir = dst.CreateSubdirectory(srcsubdir.Name)
        loop srcsubdir dstsubdir


    loop srcDir dstDir

    

let raw value = Fable.React.RawText value :> ReactElement
module Entities =
  let nbsp = raw "&nbsp;"
  let copy = raw "&copy;"


module Html =
  let script' src = script [ HTMLAttr.Type "text/javascript"; Src src ] []
  let stylesheet src = link [ HTMLAttr.Type "text/css"; Rel "stylesheet"; Href src ]

  let strf fmt = Printf.kprintf str fmt

  let save path html =
    let content =
      fragment [] [ 
        raw "<!doctype html>"
        raw "\n" 
        html ]
      |> Fable.ReactServer.renderToString 
    IO.File.WriteAllText(path, content)

open Html

let blogTitle = fragment [] [str "//"; Entities.nbsp; str"thinkbeforecoding"]

let scripts =
  fragment [] [
      // script' "//code.jquery.com/jquery-1.8.0.js"
      // script' "//netdna.bootstrapcdn.com/twitter-bootstrap/2.2.1/js/bootstrap.min.js"
      // script' "//cdnjs.cloudflare.com/ajax/libs/mathjax/2.7.1/MathJax.js?config=TeX-AMS-MML_HTMLorMML"
      script' "/content/tips.js"
  ]

type Title =
  | Home
  | Post of string

[<NoComparison>]
type Template = {
    title: Title
    content: ReactElement
    categories: ReactElement
    recentPosts: ReactElement
    footer: ReactElement
    canonicalUrl: Uri option } 

let template template =
  html [ Lang "en"]
    [ head [] 
        [ meta [CharSet "utf-8"]
          Fable.React.Standard.title [] 
             <| match template.title with
                | Post title -> [str ("// thinkbeforecoding -> " + title) ]
                | Home -> [str "// thinkbeforecoding"]
          meta [Name "viewport"; HTMLAttr.Content "width=device-width, initial-scale=1.0"]
          (match template.canonicalUrl with
           | Some url -> link [ Rel "canonical"; Href (string url)  ]
           | _ -> fragment [] [])
          scripts
          stylesheet "//netdna.bootstrapcdn.com/twitter-bootstrap/2.2.1/css/bootstrap-combined.min.css"
          stylesheet "/content/style-1.1.css"
          script [ Defer true
                   Src "https://use.fontawesome.com/releases/v5.5.0/js/all.js"
                   Integrity "sha384-GqVMZRt5Gn7tB9D9q7ONtcp4gtHIUEW/yG7h98J7IpE3kpi+srfFyyB/04OV6pG0"
                   CrossOrigin "anonymous"] []
          // script' "https://use.fontawesome.com/5477943014.js"


          link [ Rel "alternate"
                 HTMLAttr.Type "application/atom+xml"
                 Title "Atom 1.0"
                 Href "/feed/atom" ]
        ]
      body [] 
        [ div [Class "container"] 
            [ div [] [ h1 [ Class "header"] [ a [Href "/"] [ blogTitle ]] ]
              div [ Class "row" ] 
                [ div [Class "span1"] []
                  div [Class "span8"; Id "main"] [ template.content ]
                  div [Class "span3 categories"] 
                      [ template.categories
                        div [Class "social"] [
                          a [ Href "/feed/atom"] 
                            [ i [ Class "fa fa-rss-square"] []
                              str " atom feed"]
                          br []
                          a [ Href "https://twitter.com/thinkb4coding"] 
                            [ i [ Class "fab fa-twitter-square"] []
                              str " @thinkb4coding" ] ]

                        template.recentPosts
                      ]
                 ] 
            ]
         
          div [Class "footer"] [ template.footer ]
        ]
      ]

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


let processHtmlPost (post: Post)  =
  let source = Path.posts </> post.Filename + ".html"
  let dest = post.Filename + ".html"
  let html = IO.File.ReadAllText source
  let document = html.Replace("http://www.thinkbeforecoding.com/","/")
  { Content = document
    Tooltips = ""
    Link = { Text = post.Title; Href = post.FullUrl }
    Date = post.Date
    FileName = dest
    Next =  None
    Previous =None }

//FSharp.Formatting.Common.Log.SetupListener
//    Diagnostics.TraceOptions.None
//    Diagnostics.SourceLevels.All
//    (FSharp.Formatting.Common.Log.ConsoleListener())

open FSharp.Text.RegexProvider
type RemoveNamespace = Regex< @"Microsoft\.FSharp\.Core\.(?<name>\w+)">

let processScriptPost (post: Post) =
  try
    let source = Path.posts </> post.Filename + ".fsx"
    let dest = post.Filename + ".html"


    // Trace.tracefn "Parsing script %s" source
    // let listener = 
    //   FSharp.Formatting.Common.Log.SetupListener 
    //                                 (Diagnostics.TraceOptions()) 
    //                                 (Diagnostics.SourceLevels.All)
    //                                 (FSharp.Formatting.Common.Log.ConsoleListener())
    // FSharp.Formatting.Common.Log.SetupSource [|listener|] (FSharp.Formatting.Common.Log.source)
    

    let doc = 
      let fsharpCoreDir = "-I:" + __SOURCE_DIRECTORY__ + @"\packages\full\FSharp.Core\lib\netstandard2.0\"
      let fcsDir = "-I:" + __SOURCE_DIRECTORY__ + @"\packages\full\FSharp.Compiler.Service\lib\netstandard2.0\"
      let fcs = "-r:" + __SOURCE_DIRECTORY__ + @"\packages\full\FSharp.Compiler.Service\lib\netstandard2.0\FSharp.Compiler.Service.dll"
      let e = Evaluation.FsiEvaluator([|fsharpCoreDir; fcsDir; fcs ;  "-d:BLOG" |])
      e.EvaluationFailed |> Event.add (fun e -> 
        eprintfn "%s" e.Text
        eprintfn "%O" e.Exception
        eprintfn "%s" e.StdErr)
      Literate.ParseAndCheckScriptFile(
                  source , 
                  fscoptions = String.concat " " [ fsharpCoreDir; fcsDir; fcs ],
                  fsiEvaluator = e)
    let doc' = Literate.FormatLiterateNodes(doc, OutputKind.Html, "", true, true)
    let output = Literate.ToHtml doc' 
    { Content = output
      Tooltips = RemoveNamespace().TypedReplace(doc'.FormattedTips, fun m -> m.name.Value)
      Link =  { Text = post.Title; Href = post.FullUrl }
      Date = post.Date 
      FileName = dest
      Next = None
      Previous = None }
    with
    | ex -> 
       eprintfn "%O" ex
       reraise()

let processMarkdownPost (post: Post) =
  let source = Path.posts </> post.Filename + ".md"
  let dest = post.Filename + ".html"

  let output = 
    IO.File.ReadAllText source
    |> Literate.ParseMarkdownString
    |> Literate.ToHtml
  { Content = output
    Tooltips = ""
    Link =  { Text = post.Title; Href = post.FullUrl }
    Date = post.Date 
    FileName = dest
    Next = None
    Previous = None }

let processPost post =
  printfn "[Post] %s" post.Title

  if File.exists (Path.posts </> post.Filename + ".fsx") then
    processScriptPost post
  elif File.exists (Path.posts </> post.Filename + ".md") then
    processMarkdownPost post
  else
    processHtmlPost post

open Entities


let copyright =
  let now = DateTime.UtcNow
  div [Class "copyright"] [Entities.copy ; strf " 2009-%d Jérémie Chassaing / thinkbeforecoding" now.Year]

let left = fragment [] [str "<"; nbsp]
let pipe = str "|"
let right = fragment [] [ nbsp; str ">"]
let fmtDate (d:DateTime) = str (d.ToString("yyyy-MM-ddTHH:mm:ss"))
let author = str " / jeremie chassaing"
let templatePost categories recentPosts titler post =
  let link l = a [Href l.Href] [ str l.Text]
  let prev = post.Previous |> Option.map link
  let next = post.Next |> Option.map link
  let links =
      match prev, next with
      | None, None -> [] 
      | Some p, None -> [left; p ; nbsp ; pipe ]
      | None, Some n -> [ pipe; nbsp; n; right ]
      | Some p, Some n -> [ left; p ; nbsp; pipe; nbsp; n; right ]
      |> fragment []

  let content =
    fragment [] [
      h1 [Class "title"] [ link post.Link ]
      div [Class "date"] 
        [ fmtDate post.Date
          author ]
      raw post.Content
      raw post.Tooltips
    ]
  let footer =
    fragment [] [
      div [Class "links"] [links]
      copyright
    ]
  { title = titler post.Link.Text
    content = content
    categories = categories
    recentPosts = recentPosts
    footer = footer
    canonicalUrl = Some (Uri ("https://thinkbeforecoding.com/" ./ post.Link.Href)) }
  |> template  

let savePost outputDir post html =
    html |> Html.save (outputDir </> post.FileName)


let recentPosts =
    div [ Class "recent-posts"] [
      span [Class "k"] [str "type "]
      span [Class "t"] [str "RecentPosts "]
      span [Class "o"] [str "="]
      ul [] [
        for p in posts 
                |> List.sortByDescending (fun p -> p.Date ) 
                |> List.truncate 10 do
          yield li [] [
            span [Class "o"] [str "| ``"]
            a [Href <| p.FullUrl] [str p.Title]
            span [Class "o"] [str "``"]]

        yield li [] [
          span [Class "o"] [str "| "]
          a [Href "/category/all" ] [str "_"]
          nbsp
          nbsp
          span [ Class "c"] [str "// "]
          a [Href "/category/all"] [span [Class "c"] [str "all posts so far"]]
        ]
      ]
    ]

let listPage outputDir categoriesHtml recentPosts name title (posts: Post list) = 
    let dest = outputDir </> name + ".html"
    let catPosts =
      posts
      |> List.sortByDescending (fun p -> p.Date)
    let content =
      fragment [] [
        h1 [Class "title"] [str title]
        ul [Class "category"]
          [ for p in catPosts ->
               li [] [ 
                    str (p.Date.ToString("yyyy-MM-ddTHH:mm:ss"))
                    str " |> "
                    a [Href ( p.FullUrl) ] [str p.Title] ] ]
      ]
    let footer = copyright
    { content = content
      title = Post title
      categories = categoriesHtml
      recentPosts = recentPosts
      footer = footer
      canonicalUrl = None
       }
    |> template
    |> Html.save dest

module Categories =
  let categoriesHtml =
    div [Class "categories"] [
      span [Class "k"] [str "type "]
      span [Class "t"] [str "Categories "]
      span [Class "o"] [str "="]
      ul [] [
        for c in categories ->
          li [] [
            span [Class "o"] [str "| "]
            a [Href <| "/category/" + Categories.name c ] [str (Categories.title c)]
          ] ] ]
 
  let processCategory outputDir recentPosts cat =
    posts
    |> List.filter (fun p -> p.Category = cat)
    |> listPage outputDir categoriesHtml recentPosts (Categories.name cat) (Categories.title cat) 


let prevnext f l =
  let rec loop p result =
    function
    | [] -> result |> List.rev
    | [ c ] -> List.rev (f (Some p) c None :: result)
    | c :: n :: tail -> loop c (f (Some p) c (Some n) :: result) (n :: tail)
  match l with
  | [] -> []
  | [ c ] -> [f None c None]
  | c :: n :: tail -> loop c  [f None c (Some n)] (n :: tail) 

let formattedPosts =
  posts
  |> List.sortByDescending (fun p -> p.Date) 
  |> List.map processPost
  |> prevnext (fun p c n -> { c with Next = n |> Option.map (fun n -> n.Link); Previous = p |> Option.map (fun p -> p.Link)})
  

try
  Directory.delete Path.out
with
| ex -> printfn "Could not delete out dir"
Directory.ensure Path.outPosts
formattedPosts
|> List.iter (fun p ->
  p
  |> templatePost Categories.categoriesHtml recentPosts Post
  |> savePost Path.outPosts p)


Directory.ensure Path.feed
formattedPosts
|> List.truncate 5
|> List.map (fun p -> Feed.entry p.Link.Text ("https://thinkbeforecoding.com/" ./ p.Link.Href) p.Date p.Content)
|> Feed.feed
|> string
|> fun f -> IO.File.WriteAllText(Path.atom, f)


Directory.delete Path.categories
Directory.ensure Path.categories
Categories.categories
|> List.iter (Categories.processCategory Path.categories recentPosts)

listPage Path.categories Categories.categoriesHtml recentPosts "all" "All posts so far" posts


printfn "[Media] copy dir"
Directory.copy Path.media Path.outmedia
printfn "[Content] copy dir"
Directory.ensure Path.outcontent
Directory.copy Path.content Path.outcontent

formattedPosts
|> List.tryHead
|> Option.iter (fun p -> 
      p
      |> templatePost Categories.categoriesHtml recentPosts (fun _ -> Home)
      |> Html.save (Path.out </> "index.html") )



