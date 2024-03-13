open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open System.Collections.Generic
open Microsoft.AspNetCore.Http.Extensions

        

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    let robot = builder.Configuration["Robot"]
        

    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        |> ignore


    let app = builder.Build()

    match builder.Configuration.GetSection("Redirects") with
    | null -> ()
    | section ->
        for redirect in section.GetChildren()  do
            let target = redirect["Target"]
            let path = redirect["Path"]
            let permanent = match redirect["Temporary"] with | null -> false | b -> not (Boolean.Parse b)
            app.MapGet(path, (fun c -> 
                c.Response.Redirect(target, permanent)
                Task.CompletedTask
            )) |> ignore


    app.MapReverseProxy() |> ignore

    let hostRedirects = Dictionary(StringComparer.OrdinalIgnoreCase)
    match builder.Configuration.GetSection("HostRedirects") with
    | null -> ()
    | section ->
            for redirect in section.GetChildren() do
                hostRedirects.Add(redirect["Host"], redirect["Target"])
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