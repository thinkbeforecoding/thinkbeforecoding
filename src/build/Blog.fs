module Blog
open System
open FSharp.Data

type Post = {
    Title: string
    Category: string option
    Date: DateTimeOffset
    Url: string
    Hashtags: string list
    }
    with
    member this.Filename =
      this.Date.ToLocalTime().ToString("yyyy-MM-dd-") + this.Url.Replace(":","")
    member this.FullUrl =
      "/post/" + this.Date.ToLocalTime().ToString("yyyy/MM/dd/") + Net.WebUtility.UrlEncode this.Url



type PostJson = JsonProvider<const(__SOURCE_DIRECTORY__ + "/posts.json")>
let json = PostJson.Load(Path.root </> "posts.json")
let posts =
    json.Posts
    |> Seq.map (fun p -> {
        Title = p.Title
        Category = p.Category
        Date = p.Date
        Url = p.Url
        Hashtags = p.Hashtags |> Array.toList
        })
    |> Seq.toList

let categories =
    posts
    |> Seq.choose (fun p -> p.Category)
    |> Seq.distinct
    |> Seq.toList
    
module Category =
    let names = 
        match json.CategoryNames.JsonValue with
        | JsonValue.Record props ->
            props
            |> Array.choose (function (k,JsonValue.String v) -> Some (k,v) | _ -> None )
            |> Map.ofArray
        | _ -> Map.empty
    let titles = 
        match json.CategoryTitles.JsonValue with
        | JsonValue.Record props ->
            props
            |> Array.choose (function (k,JsonValue.String v) -> Some (k,v) | _ -> None )
            |> Map.ofArray
        | _ -> Map.empty

    let name cat  =
        Map.tryFind cat names
        |> Option.defaultValue cat 
    let title cat =
        Map.tryFind cat titles
        |> Option.defaultValue cat




let md5 path = 
  use md5p = System.Security.Cryptography.MD5.Create()
  use stream = IO.File.OpenRead(path)
  md5p.ComputeHash(stream)
  |> Convert.ToBase64String
