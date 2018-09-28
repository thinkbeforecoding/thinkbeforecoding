#r "paket:
framework: netstandard2.0
source https://api.nuget.org/v3/index.json
source c:/Development/FSharp.Formatting/bin/nuget
source /mnt/c/Development/FSharp.Formatting/bin/nuget

nuget Fake.Core.Target 
nuget Fake.Core.Trace
nuget FSharp.Data prerelease
nuget FSharp.Literate //" 

#load "./.fake/blog.fsx/intellisense.fsx" 
#load "html.fsx"
#load "posts.fsx"
#load "feed.fsx"
#if !FAKE
#r "netstandard"
#endif

#nowarn "86"

open Html
open Attributes
open Posts
open Categories
open Fake.IO.FileSystemOperators
open Fake.IO
open Fake.Core
open System

open FSharp.Literate
open FSharp.Markdown
let (./) (x: string) (y: string) =
 match x.EndsWith("/"), y.StartsWith("/") with
 | false, false -> x + "/" + y
 | true, true -> x + y.Substring(1)
 | _ -> x + y

let blogTitle = els [text "//";Entities.nbsp;text"thinkbeforecoding"] |> Html.flatten
let scripts =
  els [
      script "//code.jquery.com/jquery-1.8.0.js"
      script "//netdna.bootstrapcdn.com/twitter-bootstrap/2.2.1/js/bootstrap.min.js"
      script "//cdnjs.cloudflare.com/ajax/libs/mathjax/2.7.1/MathJax.js?config=TeX-AMS-MML_HTMLorMML"
      script "/content/tips.js"
  ]

type Title =
  | Home
  | Post of string

[<NoComparison>]
type Template = {
    title: Title
    content: Html
    categories: Html
    recentPosts: Html
    footer: Html
    canonicalUrl: Uri option } 

let template template =
  html ["lang" := "en"]
    [ head [] 
        [ meta ["charset" := "utf-8"]
          Html.title <| match template.title with
                        | Post title -> "// thinkbeforecoding -> " + title
                        | Home -> "// thinkbeforecoding"
          meta ["name" := "viewport"; "content" := "width=device-width, initial-scale=1.0"]
          (match template.canonicalUrl with
           | Some url -> link [ rel "canonical"; href url  ]
           | _ -> none)
          scripts
          stylesheet "//netdna.bootstrapcdn.com/twitter-bootstrap/2.2.1/css/bootstrap-combined.min.css"
          stylesheet "/content/style.css"
          script "https://use.fontawesome.com/5477943014.js"


          link [ "rel" := "alternate"
                 "type" := "application/atom+xml"
                 "title" := "Atom 1.0"
                 "href" := "/feed/atom" ]
        ]
      body [] 
        [ div [cls "container"] 
            [ div [] [ h1 [ cls "header"] [ a [href "/"] [ blogTitle ]] ]
              div [ cls "row" ] 
                [ div [cls "span1"] []
                  div [cls "span8"; id "main"] [ template.content ]
                  div [cls "span3 categories"] 
                      [ template.categories
                        div [cls "social"] [
                          a [ href "/feed/atom"] 
                            [ i [ cls "fa fa-rss-square"] []
                              text " atom feed"]
                          br
                          a [ href "https://twitter.com/thinkb4coding"] 
                            [ i [ cls "fa fa-twitter-square"] []
                              text " @thinkb4coding" ] ]

                        template.recentPosts
                      ]
                 ] 
            ]
         
          div [cls "footer"] [ template.footer ]
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
  let source = Path.posts </> post.Name + ".html"
  let dest = post.Name + ".html"
  let html = IO.File.ReadAllText source
  let document = html.Replace("http://www.thinkbeforecoding.com/","/")
  { Content = document
    Tooltips = ""
    Link = { Text = post.Title; Href = "/post" ./ post.Url }
    Date = post.Date
    FileName = dest
    Next =  None
    Previous =None }

FSharp.Formatting.Common.Log.SetupListener
    Diagnostics.TraceOptions.None
    Diagnostics.SourceLevels.All
    (FSharp.Formatting.Common.Log.ConsoleListener())
    
let processScriptPost (post: Post) =
  try
    let source = Path.posts </> post.Name + ".fsx"
    let dest = post.Name + ".html"
    Trace.tracefn "Parsing script %s" source
    let doc = Literate.ParseScriptFile(source)
    Trace.tracefn "Format"
    let doc' = FSharp.Literate.Literate.FormatLiterateNodes(doc, OutputKind.Html, "", true, true)
    Trace.tracefn "Format Html"
    let output = Formatting.format doc'.MarkdownDocument true OutputKind.Html
    { Content = output
      Tooltips = doc'.FormattedTips
      Link =  { Text = post.Title; Href =  "/post" ./ post.Url }
      Date = post.Date 
      FileName = dest
      Next = None
      Previous = None }
    with
    | ex -> 
       Trace.traceError <| string ex
       reraise()

let processMarkdownPost (post: Post) =
  let source = Path.posts </> post.Name + ".md"
  let dest = post.Name + ".html"

  let output = 
    IO.File.ReadAllText source
    |> FSharp.Markdown.Markdown.TransformHtml
  { Content = output
    Tooltips = ""
    Link =  { Text = post.Title; Href = "/post" ./ post.Url }
    Date = post.Date 
    FileName = dest
    Next = None
    Previous = None }

let processPost post =
  Trace.tracefn "[Post] %s" post.Title
  if File.exists (Path.posts </> post.Name + ".fsx") then
    processScriptPost post
  elif File.exists (Path.posts </> post.Name + ".md") then
    processMarkdownPost post
  else
    processHtmlPost post

open Entities


let copyright =
  let currentYear = DateTime.UtcNow.Year
  div [cls "copyright"] [Entities.copy ; textf " 2009-%d Jérémie Chassaing / thinkbeforecoding" currentYear]

let left = els [text "<"; nbsp]
let pipe = text "|"
let right = els [ nbsp; text ">"]
let fmtDate (d:DateTime) = text (d.ToString("yyyy-MM-ddTHH:mm:ss"))
let author = text " / jeremie chassaing"
let templatePost categories recentPosts titler post =
  let link l = a [href l.Href] [ text l.Text]
  let prev = post.Previous |> Option.map link
  let next = post.Next |> Option.map link
  let links =
      match prev, next with
      | None, None -> [] 
      | Some p, None -> [left; p ; nbsp ; pipe ]
      | None, Some n -> [ pipe; nbsp; n; right ]
      | Some p, Some n -> [ left; p ; nbsp; pipe; nbsp; n; right ]
      |> els

  let content =
    els [
      h1 [cls "title"] [ link post.Link ]
      div [cls "date"] 
        [ fmtDate post.Date
          author ]
      Html post.Content
      Html post.Tooltips
    ]
  let footer =
    els [
      div [cls "links"] [links]
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
    div [ cls "recent-posts"] [
      spant [cls "k"] "type "
      spant [cls "t"] "RecentPosts "
      spant [cls "o"] "="
      ul [] [
        for p in posts 
                |> List.sortByDescending (fun p -> p.Date ) 
                |> List.truncate 10 do
          yield li [] [
            spant [cls "o"] "| ``"
            a [href <| "/post/" ./ p.Url] [text p.Title]
            spant [cls "o"] "``"]

        yield li [] [
          spant [cls "o"] "| "
          a [href "/category/all" ] [text "_"]
          nbsp
          nbsp
          spant [ cls "c"] "// "
          a [href "/category/all"] [spant [cls "c"] "all posts so far"]
        ]
      ]
    ]
    |> Html.flatten

let listPage outputDir categoriesHtml recentPosts name title (posts: Post list) = 
    let dest = outputDir </> name + ".html"
    let catPosts =
      posts
      |> List.sortByDescending (fun p -> p.Date)
    let content =
      els [
        h1 [cls "title"] [text title]
        ul [cls "category"]
          [ for p in catPosts ->
               li [] [ 
                    text (p.Date.ToString("yyyy-MM-ddTHH:mm:ss"))
                    text " |> "
                    a [href ("/post/" ./ p.Url) ] [text p.Title] ] ]
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
    div [cls "categories"] [
      spant [cls "k"] "type "
      spant [cls "t"] "Categories "
      spant [cls "o"] "="
      ul [] [
        for c in categories ->
          li [] [
            spant [cls "o"] "| "
            a [href <| "/category/" + Categories.name c ] [text (Categories.title c)]
          ] ] ]
    |> Html.flatten
 
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
  

Directory.delete Path.out
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

Shell.copyRecursive Path.media Path.outmedia true
Shell.copyRecursive Path.content Path.outcontent

formattedPosts
|> List.tryHead
|> Option.iter (fun p -> 
      p
      |> templatePost Categories.categoriesHtml recentPosts (fun _ -> Home)
      |> Html.save (Path.out </> "index.html") )
