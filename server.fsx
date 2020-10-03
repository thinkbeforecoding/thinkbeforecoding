#r "paket:
framework: netstandard2.0
source https://api.nuget.org/v3/index.json

nuget Giraffe
nuget Microsoft.AspNetCore.App
nuget Microsoft.AspNetCore.Server.Kestrel
nuget Microsoft.AspNetCore 
nuget Microsoft.AspNetCore.StaticFiles // "
#if !FAKE
#r "netstandard"
#endif
#load "./.fake/server.fsx/intellisense.fsx" 

// #load "posts.fsx"

open Giraffe
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.StaticFiles
open Microsoft.Extensions.FileProviders
open FSharp.Control.Tasks.V2.ContextInsensitive

let (</>) x y = System.IO.Path.Combine(x,y)

let root = __SOURCE_DIRECTORY__
let posts = root </> "output" </> "post"

let setContentType ctype: HttpHandler =
  fun nxt ctx ->
   task { 
      ctx.SetContentType "ctype"
      return Some ctx }


let service =
  GET >=> choose [
    route "/" >=> htmlFile "index.html"
    routef "/post/%s" (fun url -> 
      let file = posts </> url.Replace("/","-").Replace(":","") + ".html"
      htmlFile file)
      // |> List.tryFind (fun p -> p.Date.ToString("yyyy/MM/dd/") + p.Url = url)
      // |> Option.map (fun p -> htmlFile ("posts/" + p.Date.ToString("yyyy-MM-dd-") + p.Url.Replace(":","") + ".html"))
      // |> Option.defaultValue (RequestErrors.NOT_FOUND "Not found"))
    routef "/category/%s" (fun c -> htmlFile ("category/" + c + ".html"))
    route "/feed/atom" >=> htmlFile ("/feed/atom.xml") >=> setContentType "application/atom+xml"
  ]

let configureApp (app : IApplicationBuilder) =
    // Add Giraffe to the ASP.NET Core pipeline
    app.UseGiraffe service

    let staticFiles dir reqPath =
      app.UseStaticFiles( 
               StaticFileOptions(
                   FileProvider = 
                       new PhysicalFileProvider(dir),
                   RequestPath = Http.PathString(reqPath))) 
      |> ignore
    
    staticFiles (root </> "content/") "/content"
    staticFiles (root </> "output/public") "/public"
    staticFiles (root </> "output/post") "/post"
    
       
        

let configureServices (services : IServiceCollection) =
    // Add Giraffe dependencies
    services.AddGiraffe() |> ignore

WebHost.CreateDefaultBuilder()
        .UseKestrel()
        .Configure(System.Action<_> configureApp)
        .ConfigureServices(configureServices)
        .UseSetting("contentRoot", __SOURCE_DIRECTORY__ + "/output" )
        .Build()
        .Run()
