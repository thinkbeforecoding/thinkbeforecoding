#r "packages/Fake/tools/FakeLib.dll"
#load "packages/FSharp.Azure.StorageTypeProvider/StorageTypeProvider.fsx"
#load "posts.fsx"

open System
open Fake
open FSharp.Azure.StorageTypeProvider
open Posts
open Microsoft.WindowsAzure.Storage

type BlogStorage = AzureTypeProvider<"***">
let blog = BlogStorage.Containers.CloudBlobClient.GetContainerReference("blog")

blog.CreateIfNotExists()

let toUri (u: string) = u.Replace(@".\","").Replace(@"\","/")
let md5 path = 
  use md5p = new System.Security.Cryptography.MD5CryptoServiceProvider()
  use stream = IO.File.OpenRead(path)
  md5p.ComputeHash(stream)
  |> Convert.ToBase64String

let contentType uri = 
  match IO.Path.GetExtension uri with
  | ".css" -> "text/css"
  | ".js" -> "application/javascript"
  | ".jpeg" 
  | ".jpg" -> "image/jpeg"
  | ".png" -> "image/png"
  | ".gif" -> "image/gif"
  | ".htm"
  | ".html" -> "text/html"
  | _ -> null

let uploadMedia path =
    let uri =
        path 
        |> ProduceRelativePath Path.root
        |> toUri

    let opts = 
      Blob.BlobRequestOptions(
        StoreBlobContentMD5 = Nullable true, 
        UseTransactionalMD5 = Nullable true)
    let blob = blog.GetBlockBlobReference(uri)
    if not (blob.Exists()) then
        tracefn "[Media] %s is new" uri
        blob.Properties.ContentType <- contentType uri  
        blob.UploadFromFile(path, options = opts)
    else
        let fileMd5 = md5 path  
        blob.FetchAttributes()
        if blob.Properties.ContentMD5 <> fileMd5 then
            tracefn "[Media] %s has changed" uri
            blob.UploadFromFile(path, options = opts)
        else
            logfn "[Media] Skipping %s" uri
        let contenttype = contentType uri
        if blob.Properties.ContentType <> contenttype then
          tracefn "[Media] %s content type has changed" uri
          blob.Properties.ContentType <- contenttype
          blob.SetProperties()
        else
          logfn "[Media] Skipping %s content type" uri

tracef "Upload media"
!! (Path.media </> "**/*.*")
|> Seq.map (ProduceRelativePath Path.root  >> toUri)
|> Seq.toList
|> List.iter uploadMedia

!! (Path.outcontent </> "**/*.*")
|> Seq.map (ProduceRelativePath Path.out >> toUri)
|> Seq.toList
|> List.iter uploadMedia



let uploadPost post =
  let path = Path.outPosts </> post.Name + ".html"

  let blob = blog.GetBlockBlobReference("posts/" + post.Url)

  let fileMd5 = md5 path  
  let upload = 
    if blob.Exists() then
      blob.FetchAttributes()
      if blob.Properties.ContentMD5 <> fileMd5 then
        tracefn "[Post] %s has changed" post.Url
        true
      else
        logfn "[Post] Skipping %s" post.Url
        false
    else
      tracefn "[Post] %s is new" post.Url
      true
  if upload then
    let opts = 
      Blob.BlobRequestOptions(
        StoreBlobContentMD5 = Nullable true, 
        UseTransactionalMD5 = Nullable true)
    blob.UploadFromFile(path, options = opts)
 
tracef "Upload posts"
posts
|> List.iter uploadPost

posts
|> List.sortByDescending (fun p -> p.Date)
|> List.head
|> fun p -> { p with Url = "index.html"}
|> uploadPost
