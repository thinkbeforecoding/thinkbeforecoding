module Challenge

open System.Net
open System.Net.Http
open System.IO
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open System

[<FunctionName("acmechallenge")>]
let Run([<HttpTrigger(AuthorizationLevel.Anonymous, [| "get" |],Route="acme-challenge/{code}")>]req: HttpRequestMessage, code: string, log: ILogger) =
    task {
        try
            log.LogInformation (sprintf "chalenge for: %s" code)
            let content = File.ReadAllText(@"D:\home\site\wwwroot\.well-known\acme-challenge\"+code);
            log.LogInformation("Challenge found")
            return new HttpResponseMessage(
                    HttpStatusCode.OK,
                    Content = new StringContent(content, Text.Encoding.UTF8, "text/plain") )
        with
        | ex ->
            log.LogError(ex.Message)
            return raise (AggregateException ex)
    } 
