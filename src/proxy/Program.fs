open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open System.Threading.Tasks

[<CLIMutable>]
type Redirect =
    { Path: string
      Target: string}

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    let redirects = 
        builder.Configuration.GetSection("Redirects").Get<Redirect[]>() 
        |> Option.ofObj
        |> Option.defaultValue [||]

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

    app.Run()

    0 // Exit code

