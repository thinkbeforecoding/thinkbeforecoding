#r "packages/Fake/tools/FakeLib.dll"
#load "packages/FSharp.Formatting/FSharp.Formatting.fsx"
#load "html.fsx"
#load "posts.fsx"
#load "feed.fsx"
open System
open Fake
open Html
open Attributes
open Posts
open Categories

open FSharp.Literate


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

type Template = {
    title: Title
    content: Html
    categories: Html
    recentPosts: Html
    footer: Html
  } 
let template template =
  html ["lang" := "en"]
    [ head [] 
        [ meta ["charset" := "utf-8"]
          Html.title <| match template.title with
                        | Post title -> "// thinkbeforecoding -> " + title
                        | Home -> "// thinkbeforecoding"
          meta ["name" := "viewport"; "content" := "width=device-width, initial-scale=1.0"]
          scripts
          stylesheet "//netdna.bootstrapcdn.com/twitter-bootstrap/2.2.1/css/bootstrap-combined.min.css"
          stylesheet "/content/style.css"
          // stylesheet "https://use.fontawesome.com/c3e81a7a2a.css"
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

let processMarkdownPost post =
  let source = Path.posts </> post.Name + ".md"
  let dest = post.Name + ".html"

  let output = 
    IO.File.ReadAllText source
    |> FSharp.Markdown.Markdown.TransformHtml
  { Content = output
    Tooltips = ""
    Link =  { Text = post.Title; Href = post.Url }
    Date = post.Date 
    FileName = dest
    Next = None
    Previous = None }

let processPost post =
  tracefn "[Post] %s" post.Title
  if fileExists (Path.posts </> post.Name + ".fsx") then
    processScriptPost post
  elif fileExists (Path.posts </> post.Name + ".md") then
    processMarkdownPost post
  else
    processHtmlPost post

open Html
open Attributes
open Entities


let copyright =
  let currentYear = DateTime.UtcNow.Year
  div [cls "copyright"] [Entities.copy ; textf " 2009-%d Jérémie Chassaing / thinkbeforecoding" currentYear]

let left = els [text "<"; nbsp]
let pipe = text "|"
let right = els [ nbsp; text ">"]
let fmtDate (d:DateTime) = text (d.ToString("yyyy-MM-ddTHH:mm-ss"))
let author = text " / jeremie chassaing"
let templatePost categories recentPosts titler post =
  let link l = a [href ("/post/" + l.Href)] [ text l.Text]
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
    footer = footer }
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
            a [href <| "/post/" + p.Url] [text p.Title]
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
                    a [href ("/post/" + p.Url) ] [text p.Title] ] ]
      ]
    let footer = copyright
    { content = content
      title = Post title
      categories = categoriesHtml
      recentPosts = recentPosts
      footer = footer }
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
  

if directoryExists Path.out then
  DeleteDir Path.out
CreateDir Path.outPosts
formattedPosts
|> List.iter (fun p ->
  p
  |> templatePost Categories.categoriesHtml recentPosts Post
  |> savePost Path.outPosts p)

CreateDir Path.feed
formattedPosts
|> List.map (fun p -> Feed.entry p.Link.Text ("https://thinkbeforecoding.com/post/" + p.Link.Href) p.Date p.Content)
|> Feed.feed
|> string
|> fun f -> IO.File.WriteAllText(Path.atom, f)


if directoryExists Path.categories then
  DeleteDir Path.categories
CreateDir Path.categories
Categories.categories
|> List.iter (Categories.processCategory Path.categories recentPosts)

listPage Path.categories Categories.categoriesHtml recentPosts "all" "All posts so far" posts

CopyDir Path.outmedia Path.media allFiles
CopyDir Path.outcontent Path.content allFiles

formattedPosts
|> List.tryHead
|> Option.iter (fun p -> 
      p
      |> templatePost Categories.categoriesHtml recentPosts (fun _ -> Home)
      |> Html.save (Path.out </> "index.html") )

