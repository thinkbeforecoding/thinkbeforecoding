#load "Posts.fsx"
open System.IO
let root = __SOURCE_DIRECTORY__
let (</>) x y = Path.Combine(x,y)

let post = posts.[0]

let newPosts = 
    root </> "newposts"

if not (Directory.Exists newPosts) then
    Directory.CreateDirectory newPosts |> ignore

let move post =
    try
        let file = 
            Directory.GetFiles(root </> "posts",  post.Name + ".*" )
            |> Seq.head

        let ext= Path.GetExtension(file)
        let newFilename =
            post.Url.Replace('/','-').Replace(":","") + ext

        File.Copy(file, newPosts </> newFilename, true)
    with
    | ex ->
        printfn "Error with file : %s" post.Url
        reraise()


posts
|> List.iter move



