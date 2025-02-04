open FSharp.Formatting.Literate
open FSharp.Data
open Fable.React
open Fable.React.Props
open System
open Printf
open Blog
open System.Text.RegularExpressions


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


type Title =
  | Home
  | Post of string * string option

type Link = {
  Text: string
  Href: string }

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

    let dir = System.IO.Path.GetDirectoryName(path: string)
    Directory.ensure dir
    IO.File.WriteAllText(path, content)

type TwitterLink = 
    { Link: Link
      Via: string
      Hashtags: string list}
module Twitter =
    open System.Web
    let linkUrl link =
        sprintf "https://twitter.com/intent/tweet?text=%s&url=%s&via=%s&hashtags=%s"
            (HttpUtility.UrlEncode (link.Link.Text + "\n"))
            (HttpUtility.UrlEncode link.Link.Href)
            (HttpUtility.UrlEncode link.Via)
            (HttpUtility.UrlEncode (String.concat "," link.Hashtags))


            

open Html

let blogTitle = fragment [] [str "//"; Entities.nbsp; str"thinkbeforecoding"]


[<NoComparison>]
type Template = {
    title: Title
    content: ReactElement
    categories: ReactElement
    recentPosts: ReactElement
    footer: ReactElement
    canonicalUrl: Uri option
    Previous: Link option
    Next: Link option
    HotReload: bool
    } 

let metaf name fmt =
  kprintf (fun s -> meta [Name name; HTMLAttr.Content s]) fmt
let metafb name fmt =
  kprintf (fun s -> meta [Property name; HTMLAttr.Content s]) fmt


let template template =
  html [ Lang "en"]
    [ head [] 
        [ meta [CharSet "utf-8"]
          Fable.React.Standard.title [] 
             <| match template.title with
                | Post (title , _)-> [str ("// thinkbeforecoding -> " + title) ]
                | Home -> [str "// thinkbeforecoding"]

          metaf "viewport" "width=device-width, initial-scale=1.0"
          metaf "author" "Jérémie Chassaing"
          metaf "keywords" "programming, F#, Event Sourcing, DDD"
          metafb "description" "think - code - repeat"
          metafb "og:title" "// thinkbeforecoding"
          metafb "og:type" "website"
          metafb "og:url" "https://thinkbeforecoding.com"
          metafb "og:image" "https://thinkbeforecoding.com/content/thinkbeforecoding-social.jpg"
          metafb "og:description" "think - code - repeate - Jérémie Chassaing"
          metafb "og:locale" "en_US"
          metaf "twitter:card" "summary_large_image"

          match template.title with
          | Home ->
                metaf "twitter:description" "think - code - repeat - Jérémie Chassaing"
                metaf "twitter:image" "https://thinkbeforecoding.com/content/thinkbeforecoding-twitter.jpg"
                metaf "twitter:title" "// thinkbeforecoding"
          | Post(title, Some url) -> 
                metaf "twitter:description" $"{title} - Jérémie Chassaing"
                metaf "twitter:image" $"https://thinkbeforecoding.com/public/{Path.clean url}/thinkbeforecoding-twitter.jpg"
                metaf "twitter:title" $"// {title} - thinkbeforecoding"
          | Post(title, None)->
              metaf "twitter:description" $"{title} - Jérémie Chassaing"
              metaf "twitter:image" "https://thinkbeforecoding.com/content/thinkbeforecoding-twitter.jpg"
              metaf "twitter:title" $"// {title} - thinkbeforecoding"

          metaf "twitter:site" "@thinkbeforecoding" 
          metaf "twitter:creator" "@thinkbeforecoding"

          link [Rel "author"; Href "https://mastodon.social/@thinkb4coding" ]
          link [Rel "icon"; Href "/content/favicon.ico" ]
          link [Rel "shortcut icon"; Href "/content/favicon.ico" ]
          link [Rel "me"; Href "https://mastodon.social/@thinkb4coding"]
          (match template.title,template.canonicalUrl with
           | Home, _ -> link [ Rel "canonical"; Href "https://thinkbeforecoding.com"  ]
           | _,Some url -> link [ Rel "canonical"; Href (string url)  ]
           | _ -> null)
          stylesheet "/content/bootstrap/5.0.2/bootstrap.min.css"
          stylesheet "/content/style-1.8.css"
          stylesheet "/content/fontawesome/6.2.0/css/all.min.css"

          if template.HotReload then
            script [ ] [
              RawText """
  
function reload() {
    var xmlhttp = new XMLHttpRequest();
    xmlhttp.onreadystatechange = function() {
        if (xmlhttp.readyState == XMLHttpRequest.DONE) {   // XMLHttpRequest.DONE == 4
           if (xmlhttp.status == 200) {
         j=JSON.parse(xmlhttp.responseText);
         console.log(j);
         //if file changed RESTULT is TRUE we issue a 

         if (j.hasChanged)
           { 
           console.log("Changed!");
             window.location.reload(false); //reload this page
            }  
         else
        console.log("No Change:");
 
         
           }
           else if (xmlhttp.status == 400) {
              console.log('error 400');
           }
           else {
               console.log('Error '+xmlhttp.status );
           }
          setTimeout(function() { reload(); }, 250);
        }

    };
    xmlhttp.open("GET", "/watch", true);
    xmlhttp.send();

}
setTimeout( function() { reload(); }, 250 );
              """
            ]


          link [ Rel "alternate"
                 HTMLAttr.Type "application/atom+xml"
                 Title "Atom 1.0"
                 Href "https://feeds.thinkbeforecoding.com" ]

          match template.Next with
          | Some next -> link [ Rel "next"; Href next.Href ]
          | None -> null
          match template.Previous with
          | Some prev -> link [ Rel "prev"; Href prev.Href ]
          | None -> null

        ]
      body [] 
        [ div [Class "container"] 
            [ div [] [ h1 [ Class "header"] [ a [Href "/"] [ blogTitle ]] ]
              div [ Class "row" ] 
                [ //div [Class "col-1"] []
                  div [Class "col-12 col-md-8"; Id "main"] [ template.content ]
                  div [Class "col-12 col-md-4 categories"] 
                      [ template.categories
                        div [Class "social"] [
                          a [ Href "/feed/atom"] 
                            [ i [ Class "fa fa-rss-square"] []
                              str " atom feed"]
                          br []
                          a [ Href "https://mastodon.social/@thinkb4coding"]
                            [ i [ Class "fa-brands fa-mastodon"] []
                              str " @thinkb4coding@mastodon.social"]
                          ]

                        template.recentPosts
                      ]
                 ] 
            ]
         
          div [Class "footer"] [ template.footer ]
        ]
      ]

type FormattedPost = {
  MD5: string
  TitleUrl: string
  Link: Link
  Content: string
  Tooltips: string
  Date: DateTimeOffset
  FileName: string
  Next:  Link option
  Previous: Link option 
  Hashtags: string list
}

open FSharp.Text.RegexProvider
type RemoveNamespace = Regex< @"Microsoft\.FSharp\.Core\.(?<name>\w+)">
#if INTERACTIVE
#r "nuget: FSharp.Data"
#r "nuget: SixLabors.ImageSharp"
#r "nuget: SixLabors.ImageSharp.Drawing, Version=*-*"
#load "Path.fs"
#load "Blog.fs"
open Blog
open System.IO
open System
#endif
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open SixLabors.ImageSharp.Drawing.Processing
open SixLabors.Fonts


let prepareCard (post: Post) =
   let img = Image.Load(Path.content </> "thinkbeforecoding-twitter.jpg")
   let fonts = FontCollection()
   fonts.Add(Path.fonts </> "OpenSans-Regular.ttf") |> ignore
   let opensans = fonts.Get("Open Sans")
   let font  = opensans.CreateFont(24.f)
   let color = Color.FromRgb(0x33uy,0x33uy,0x33uy)

   //let options = DrawingOptions()
   let rtOpts = RichTextOptions(font)
   rtOpts.TextAlignment <- TextAlignment.Center
  
   //options.TextOptions.HorizontalAlignment <- HorizontalAlignment.Center
   let mid = single img.Width / 2.f
   img.Mutate(fun x -> 
    x.DrawText(rtOpts,post.Title, color) |> ignore)
   let path = Path.tmp </> "public" </> Path.clean post.Url
   Directory.ensure path
   img.SaveAsJpeg(path </> "thinkbeforecoding-twitter.jpg" )

let processHtmlPost (post: Post) source md5 =
  prepareCard post
  let dest = post.OutPath 
  let html = IO.File.ReadAllText source
  let document = html.Replace("http://www.thinkbeforecoding.com/","/")
  { MD5 = md5
    TitleUrl = post.Url
    Content = document
    Tooltips = ""
    Link = { Text = post.Title; Href = post.FullUrl }
    Date = post.Date
    FileName = dest
    Next = None
    Previous = None
    Hashtags = post.Hashtags }


open System.Xml.Linq
let svgns = XNamespace.Get "http://www.w3.org/2000/svg"
let patchSvg (svg: string) =
   let doc = XDocument.Parse svg 
   let style = doc.Root.Element(svgns + "style")
   let styleTxt = style.Value
   style.Value <- styleTxt.Replace("black", "#d1d1d1").Replace("white","#212d30")
   string doc


let prepareDiagrams (post: Post) source =
    let lines = IO.File.ReadAllLines(source) |> Array.toList

    let rec findDiag (lines: string list) result diags =
        match lines with
        | [] -> List.rev result, List.rev diags
        | line :: rest when line.Contains("[lang=diag]") ->
            takeDiag rest ( $"![Diagram](/public/{Path.clean post.Url}/diagram-{List.length diags}.svg)" :: result) diags []
        | line :: rest -> 
            findDiag rest (line :: result) diags
    and takeDiag lines result diags diag =
        match lines with
        | [] -> List.rev result, List.rev ((List.rev diag) :: diags)
        | line :: _ when line.Length = 0 || line[0] <> ' ' ->
            findDiag lines result ((List.rev diag) :: diags)
        | line :: rest -> 
            takeDiag rest result diags (line :: diag)

    let scriptLines, diags = findDiag lines [] []

    let diagDir = Path.tmp </> "public" </> Path.clean post.Url

    for i,diag in Seq.indexed diags do
        Directory.ensure diagDir
        tracefn "[DIAG]%d" i
        let diag = 
            if diag |> List.forall (fun l -> l.StartsWith "    ") then
                diag |> List.map (fun l -> l.Substring 4)
            else
                diag
                
        let diagTxt = String.concat "\n" diag
        
        let result = 
            Fake.Core.CreateProcess.fromRawCommandLine "docker" $"""run --rm ghcr.io/gtramontina/svgbob:0.5.3 -s "{diagTxt}" """
            |> Fake.Core.CreateProcess.redirectOutput
            |> Fake.Core.Proc.run
        if result.ExitCode <> 0 then
            failwithf $"Error while converting diagram. Make sure docker is running: {result.Result.Error}" 
        let svg = patchSvg result.Result.Output
        let path = diagDir </> $"diagram-{i}.svg"
        IO.File.WriteAllText(path, svg)


    scriptLines |> String.concat "\n"

        


let processScriptPost (post: Post) source md5 =
  prepareCard post
  try
    let dest = post.OutPath

    let script = prepareDiagrams post source

    let doc = 
      let fsharpCoreDir = "-I:" + __SOURCE_DIRECTORY__ + @"/../../packages/full/FSharp.Core/lib/netstandard2.0/"
      let fcsDir = "-I:" + __SOURCE_DIRECTORY__ + @"/../../packages/full/FSharp.Compiler.Service/lib/netstandard2.0/"
      let fcs = "-r:" + __SOURCE_DIRECTORY__ + @"/../../packages/full/FSharp.Compiler.Service/lib/netstandard2.0/FSharp.Compiler.Service.dll"
      let lang = "--preferreduilang:en-US"
      let e = Evaluation.FsiEvaluator([|fsharpCoreDir; fcsDir; fcs ;  "-d:BLOG"; "--langversion:preview"; lang |])
      e.EvaluationFailed |> Event.add (fun e -> 
        eprintfn "%s" e.Text
        eprintfn "%O" e.Exception
        eprintfn "%s" e.StdErr)
      Literate.ParseScriptString(
                  script,
                  source, 
                  fscOptions = String.concat " " [ fsharpCoreDir; fcsDir; fcs; lang ],
                  fsiEvaluator = e)
    let output = Literate.ToHtml(doc, "", false, true)
    
    { MD5 = md5 
      TitleUrl = post.Url
      Content = output
      Tooltips = RemoveNamespace().TypedReplace(doc.FormattedTips, fun m -> m.name.Value)
      Link =  { Text = post.Title; Href = post.FullUrl }
      Date = post.Date 
      FileName = dest
      Next = None
      Previous = None
      Hashtags = post.Hashtags }
    with
    | ex -> 
       eprintfn "%O" ex
       reraise()


    

let processMarkdownPost (post: Post) source md5 =
  prepareCard post
  let dest = post.OutPath

  let output = 
    IO.File.ReadAllText source
    |> Literate.ParseMarkdownString
    |> Literate.ToHtml
  { MD5 = md5
    TitleUrl = post.Url
    Content = output
    Tooltips = ""
    Link =  { Text = post.Title; Href = post.FullUrl }
    Date = post.Date 
    FileName = dest
    Next = None
    Previous = None
    Hashtags = post.Hashtags
     }

type PostType =
    | Script
    | MD 
    | Html 

let findPostType (post: Post) =
    let fsxPath = Path.posts </> post.Filename + ".fsx"
    let mdPath = Path.posts </> post.Filename + ".md"
    if File.exists fsxPath then
      fsxPath, Script
    elif File.exists mdPath then
      mdPath, MD
    else
      Path.posts </> post.Filename + ".html", Html

let tryLoadFormattedPost filename =
    let fullPath = Path.tmp </> filename + ".post"
    if File.exists fullPath then
        IO.File.ReadAllText fullPath
        |> Newtonsoft.Json.JsonConvert.DeserializeObject<FormattedPost>
        |> Some
    else
        None

let saveFormattedPost filename (post: FormattedPost) =
    let fullPath = Path.tmp </> filename + ".post"
    post
    |> Newtonsoft.Json.JsonConvert.SerializeObject
    |> fun s -> IO.File.WriteAllText(fullPath, s)
    post



let processPost post =
    let source, postType =  findPostType post
    let md5 = md5 source
    let existingPost = tryLoadFormattedPost post.Filename
    match existingPost with
    | Some p when p.MD5 = md5 -> 
        logfn "[POST] %s" post.Title
        p
    | _ ->
        tracefn "[POST] %s" post.Title
        match postType with
        | Script -> processScriptPost post source md5
        | MD -> processMarkdownPost post source md5
        | Html -> processHtmlPost post source md5
        |> saveFormattedPost post.Filename

open Entities


let copyright =
  let now = DateTime.UtcNow
  div [Class "copyright"] [Entities.copy ; strf " 2009-%d Jérémie Chassaing / thinkbeforecoding" now.Year]

let left = fragment [] [str "<"; nbsp]
let pipe = str "|"
let right = fragment [] [ nbsp; str ">"]
let fmtDate (d:DateTimeOffset) = str (d.ToString("yyyy-MM-ddTHH:mm:ss"))
let author = str " / jeremie chassaing"
let templatePost hotReload categories recentPosts titler post =
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

  let toAbsolute (link: Link) =
    { link with Href = "https://thinkbeforecoding.com" + link.Href}
  let content =
    fragment [] [
      h1 [Class "title"] [ link post.Link ]
      div [Class "date"] 
        [ fmtDate post.Date
          author ]
      raw post.Content
      //if not (String.IsNullOrEmpty post.Tooltips) then
      script' "/content/tips.js"

      raw post.Tooltips
    ]
  let footer =
    fragment [] [
      div [Class "links"] [links]
      copyright
    ]
  { title = titler (post.Link.Text, (Some post.TitleUrl))
    content = content
    categories = categories
    recentPosts = recentPosts
    footer = footer
    canonicalUrl = Some (Uri ("https://thinkbeforecoding.com/" ./ post.Link.Href)) 
    Next = post.Previous
    Previous = post.Next
    HotReload = hotReload }
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

let listPage hotReload outputDir categoriesHtml recentPosts name title (posts: Post list) = 
    let dest = outputDir </> name 
    let catPosts =
      posts
      |> List.sortByDescending (fun p -> p.Date)
    let content =
      fragment [] [
        h1 [Class "title"] [str title]
        ul [Class "category"]
          [ for p in catPosts ->
               li [] [ 
                    str (p.Date.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"))
                    str " |> "
                    a [Href ( p.FullUrl) ] [str p.Title] ] ]
      ]
    let footer = copyright
    { content = content
      title = Post (title, None)
      categories = categoriesHtml
      recentPosts = recentPosts
      footer = footer
      canonicalUrl = None
      Previous = None
      Next = None
      HotReload = hotReload
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
            a [Href <| "/category/" + Category.name c ] [str (Category.title c)]
          ] ] ]
 
  let processCategory hotReload outputDir recentPosts cat =
    posts
    |> List.filter (fun p -> p.Category = Some cat)
    |> listPage hotReload outputDir categoriesHtml recentPosts (Category.name cat) (Category.title cat) 


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





    
let run hotReload = 
    Directory.ensure Path.tmp
    let formattedPosts =
      posts
      |> List.sortByDescending (fun p -> p.Date) 
      |> List.map processPost
      |> prevnext (fun p c n -> { c with Next = n |> Option.map (fun n -> n.Link); Previous = p |> Option.map (fun p -> p.Link)})
      

    try
      Directory.delete Path.out
    with
    | ex -> printfn "Could not delete out dir"

    printfn "[Media] copy dir"
    Directory.copy Path.media Path.outmedia

    Directory.ensure Path.outPosts
    formattedPosts
    |> List.iter (fun p ->
      p
      |> templatePost hotReload Categories.categoriesHtml recentPosts Post
      |> savePost Path.outPosts p)

    posts
    |> List.iter (fun p ->
        let srcPath = Path.tmp </> "public" </> p.Url
        let dstPath = Path.outmedia </> p.Url
        if IO.Directory.Exists srcPath then
            Directory.copy srcPath dstPath
    )


    Directory.ensure Path.feed
    formattedPosts
    |> List.truncate 5
    |> List.map (fun p -> Feed.entry p.Link.Text ("https://thinkbeforecoding.com/" ./ p.Link.Href) p.Date p.Content)
    |> Feed.feed
    |> string
    |> fun f -> IO.File.WriteAllText(Path.atom, f)


    Directory.delete Path.categories
    Directory.ensure Path.categories

    categories
    |> List.iter (Categories.processCategory hotReload Path.categories recentPosts)

    listPage hotReload Path.categories Categories.categoriesHtml recentPosts "all" "All posts so far" posts


    printfn "[Content] copy dir"
    Directory.ensure Path.outcontent
    Directory.copy Path.content Path.outcontent

    formattedPosts
    |> List.tryHead
    |> Option.iter (fun p -> 
          p
          |> templatePost hotReload Categories.categoriesHtml recentPosts (fun _ -> Home)
          |> Html.save (Path.out </> "index.html") )

let watch() =
  printfn "Start watch mode"
  try
    run true
    Http.Request("http://localhost:5000/watch", httpMethod = "POST") |> ignore
  with
  | ex -> eprintf "%O" ex
  use watcher = new IO.FileSystemWatcher(Path.posts)
  let quit = new Threading.ManualResetEvent(false)
  watcher.Changed |> Event.add (fun e ->
    Threading.Thread.Sleep(250)
    try
      run true
      Http.Request("http://localhost:5000/watch", httpMethod = "POST") |> ignore
    with
    | ex -> eprintfn "%O" ex
  )
  Console.CancelKeyPress   |> Event.add(fun _ -> quit.Set() |> ignore)
  watcher.EnableRaisingEvents <- true

  quit.WaitOne() |> ignore

  

[<EntryPoint>]
let main argv =
    match argv with
    | [|"-w"|] -> watch()
    | _ -> run false
    0 // return an integer exit code