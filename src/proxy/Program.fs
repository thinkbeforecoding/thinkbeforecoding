open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open System.Collections.Generic
open Microsoft.AspNetCore.Http.Extensions

[<CLIMutable>]
type Redirect =
    { Path: string
      Target: string}

[<CLIMutable>]
type HostRedirect =
    { Host: string
      Target: string}


[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    let redirects = 
        builder.Configuration.GetSection("Redirects").Get<Redirect[]>() 
        |> Option.ofObj
        |> Option.defaultValue [||]
    
    let hostRedirects = 
        let mappings = 
            builder.Configuration.GetSection("HostRedirects").Get<HostRedirect[]>() 
            |> Option.ofObj
            |> Option.defaultValue [||]

        let d = Dictionary(StringComparer.OrdinalIgnoreCase)
        for mapping in mappings do
            d.Add(mapping.Host, mapping.Target)
        d

        

    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        |> ignore


    let app = builder.Build()

    for redirect in redirects do
        app.MapGet(redirect.Path, (fun c -> 
            task { 
                c.Response.Headers.Add("Location", redirect.Target)
                c.Response.StatusCode <- 301 } :> Task) ) |> ignore

    

    app.MapReverseProxy() |> ignore

    if hostRedirects.Count > 0 then
        app.Use(fun (context: HttpContext) (next: RequestDelegate) ->
            task {
                match hostRedirects.TryGetValue(context.Request.Host.Host) with
                | false, _ -> do!  next.Invoke(context)
                | true, target -> 
                    let location = UriBuilder(context.Request.GetEncodedUrl(), Host = target).Uri
                    context.Response.Headers.Add("Location", string location )
                    context.Response.StatusCode <- 301
            } :> Task) |> ignore

    app.Run()

    0 // Exit code

open System

