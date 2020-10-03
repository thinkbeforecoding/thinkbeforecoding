// Learn more about F# at http://fsharp.org

open System
open Blog
open System.Text.RegularExpressions
open FSharp.Data

let dashRegex = Regex("-+")
let cleanRegex = Regex(@"[\s.,!?]")
let fixTitle (title: string) =
    dashRegex.Replace(
         cleanRegex.Replace(title.ToLower(), "-"),
         "-").Trim('-')


[<EntryPoint>]
let main argv =
    if argv.Length < 2 then
        printfn """Usage: 
    ./new fsx <title>
    ./new md <title>"""
        exit(0)
    let ext = 
        match argv.[0] with
        | "md" -> ".md"
        | "fsx" -> ".fsx"
        | ext ->
            printfn "Unknow file type %s" ext
            exit(0)
    
    let title = argv.[1]
    let url = fixTitle title

    let date = DateTimeOffset.UtcNow

    let newJson =
        Blog.PostJson.Root(
            schema = Blog.json.Schema,
            categoryNames = Blog.json.CategoryNames,
            categoryTitles = Blog.json.CategoryTitles,
            posts =
                [| yield! Blog.json.Posts
                   yield PostJson.Post(title, None, date, url ) |]
        )
        
    use file = IO.File.CreateText(Path.root </> "posts.json")
    newJson.JsonValue.WriteTo(file, JsonSaveOptions.None)

    let filePath = Path.posts </> date.ToLocalTime().ToString("yyyy-MM-dd-") + url + ext
    let content = 
        if ext = ".md" then
            "Post content"
        else
            """(*** hide ***)
open System
(** Post content *)
"""
    if not (IO.File.Exists(filePath)) then
        IO.File.WriteAllText(filePath, content)

    0 // return an integer exit code
