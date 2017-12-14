#r "packages/FSharp.Data/lib/net45/FSharp.Data.dll"
#r "System.Xml.Linq"

open System
open FSharp.Data
open System.Text.RegularExpressions
open System.Xml

module Option =
    let ofPair = function true,v -> Some v | _ -> None

type EditInfo = {
    Date: DateTime
    TimeZone: string
    Created: DateTime
    Updated: DateTime }


type Post = { 
    Id: int
    CategoryId: int option
    EditInfo: EditInfo
    Url: string
    Title: string
    Content: string
    Meta: string
    Published: bool
    Selected: bool
    NbComment: int
    NbTrackback: int }

let editInfo (l: string[]) = 
    {
        Date =  l.[4] |> DateTime.Parse
        TimeZone = l.[5]
        Created = l.[6] |> DateTime.Parse
        Updated = l.[7] |> DateTime.Parse
    }

let unescape (s: string) =
    s.Replace(@"\""","\"" ).Replace(@"\\",@"\").Replace(@"\n","\n").Replace(@"\r","\r").Replace(@"\t","\t")

let buildPost (l: string[]) =
    {
        Id = l.[0].Substring(1) |> int
        CategoryId =  l.[3] |> Int32.TryParse |> Option.ofPair
        EditInfo = editInfo l
        Url= l.[11]
        Title= l.[13]
        Content = unescape l.[16]
        Meta = l.[20]
        Published = l.[21] <> "0"
        Selected = l.[22] <> "0"
        NbComment = int l.[25]
        NbTrackback = int l.[26]
    }

// IO.File.ReadAllLines(__SOURCE_DIRECTORY__ + @"\Posts.csv")
// |> Array.take 1

let posts = 
    IO.File.ReadAllLines(__SOURCE_DIRECTORY__ + @"\Posts.csv")
    |> Array.skip 1
    |> Array.map(fun l -> Regex.Split(l, "\",\"")  |> buildPost)
    |> Array.filter(fun p -> p.Published )
    |> Array.toList

type Categories = CsvProvider<"Categories.csv">

let categories =
    Categories.GetSample().Rows
    |> Seq.map (fun c -> c.``Category cat_id``, c.Cat_title)
    |> Map.ofSeq

let cats =
    function
    | "" -> "NoCategory"
    | "Domain Driven Design" -> "DomainDrivenDesign"
    | ("Personal"   
        | "Design" 
        | "Linq"
        | "Duck"
        | "WP7"
        | "Agile"
        | "CQRS"
        | "NuRep" ) as c -> c
    | ".Net Framework" -> "NetFramework"
    | "Asp.net" -> "AspNet"
    | "F#" -> "FSharp"
    | "Event Sourcing" -> "EventSourcing"
    | s -> failwithf "Unknown category %s" s

let findCategory id =
    Map.find id categories |> cats



posts
|> Seq.iter (fun p -> 
    printfn """    { Name="%d"
      Title="%s"
      Category= %s
      Date=date "%s"
      Url="%s" }""" 
        p.Id 
        p.Title 
        (p.CategoryId  |> Option.map findCategory |> Option.defaultValue "NoCategory") 
        (XmlConvert.ToString(p.EditInfo.Created, XmlDateTimeSerializationMode.Utc))
        p.Url)

let (</>) x y = IO.Path.Combine(x,y)
let postsDir = __SOURCE_DIRECTORY__ </> "Posts"

posts
|> Seq.iter (fun p ->
    IO.File.WriteAllText( postsDir </> string p.Id + ".html", p.Content  ))


