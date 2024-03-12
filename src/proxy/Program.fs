open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open System.Collections.Generic
open Microsoft.AspNetCore.Http.Extensions

[<CLIMutable;NoComparison;NoEquality>]
type Redirect =
    { Path: string
      Target: string
      Temporary: bool }

[<CLIMutable;NoComparison;NoEqualityAttribute>]
type HostRedirect =
    { Host: string
      Target: string }


[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    let redirects = 
        match builder.Configuration.GetSection("Redirects").Get<Redirect[]>() with
        | null -> Array.Empty()
        | section -> section
    
    let hostRedirects = 
        let mappings = 
            match builder.Configuration.GetSection("HostRedirects").Get<HostRedirect[]>() with
            | null -> Array.Empty()
            | section -> section

        let d = Dictionary(StringComparer.OrdinalIgnoreCase)
        for mapping in mappings do
            d.Add(mapping.Host, mapping.Target)
        d

    let robot = builder.Configuration.GetSection("Robot").Get<string>()
        

    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        |> ignore


    let app = builder.Build()

    for redirect in redirects do
        app.MapGet(redirect.Path, (fun c -> 
            c.Response.Redirect(redirect.Target, not redirect.Temporary)
            Task.CompletedTask
        )) |> ignore

    

    app.MapReverseProxy() |> ignore

    if hostRedirects.Count > 0 then
        app.Use(fun (context: HttpContext) (next: RequestDelegate) ->
                match hostRedirects.TryGetValue(context.Request.Host.Host) with
                | false, _ -> next.Invoke(context)
                | true, target -> 
                    let location = UriBuilder(context.Request.GetEncodedUrl(), Host = target).Uri
                    context.Response.Redirect( string location, true)
                    Task.CompletedTask
            ) |> ignore

    if not (isNull robot) then
           app.MapGet("/robot.txt", (fun c -> 
                c.Response.Headers.ContentType <- "text/plain"
                c.Response.WriteAsync(robot)
            ) ) |> ignore 

    app.Run()

    0 // Exit code