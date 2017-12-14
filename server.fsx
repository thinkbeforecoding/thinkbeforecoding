#r "packages/suave/lib/net40/suave.dll"
#load "posts.fsx"
open Suave
open Web
open Http
open Filters
open Files
open Suave.Operators

let service =
  GET >=> choose [
    path "/" >=> browseFileHome "index.html"
    pathStarts "/content/" >=> browseHome
    pathScan "/post/%s" (fun url -> 
      posts
      |> List.tryFind (fun p -> p.Url = url)
      |> Option.map (fun p -> browseFileHome ("posts/" + p.Name + ".html"))
      |> Option.defaultValue (RequestErrors.NOT_FOUND "Not found"))
    pathStarts "/public/" >=> browseHome
    pathScan "/category/%s" (fun c -> browseFileHome ("category/" + c + ".html"))
    path "/feed/atom" >=> browseFileHome ("/feed/atom.xml")
  ]

Async.CancelDefaultToken()
let _, s = startWebServerAsync { defaultConfig with homeFolder = Some Path.out }  service
s |> Async.Start
