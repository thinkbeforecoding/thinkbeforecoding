#r "packages/Fake/tools/FakeLib.dll"
#load "packages/FSharp.Azure.StorageTypeProvider/StorageTypeProvider.fsx"
#load "posts.fsx"
#load "Categories.fsx"

open System
open Fake
open FSharp.Azure.StorageTypeProvider
open Posts
open Categories
open Microsoft.WindowsAzure.Storage

type BlogStorage = AzureTypeProvider<"***">
let blog = BlogStorage.Containers.CloudBlobClient.GetContainerReference("gzipblog")

blog.CreateIfNotExists()

let toUri (u: string) = u.Replace(@".\","").Replace(@"\","/")
let md5 path = 
  use md5p = new System.Security.Cryptography.MD5CryptoServiceProvider()
  use stream = IO.File.OpenRead(path)
  md5p.ComputeHash(stream)
  |> Convert.ToBase64String

let md5Gzip path =
  use stream = IO.File.OpenRead(path)
  use memoryStream = new IO.MemoryStream()
  use gzip = new IO.Compression.GZipStream(memoryStream, IO.Compression.CompressionMode.Compress, true)
  stream.CopyTo(gzip)
  gzip.Flush()
  gzip.Close()
  memoryStream.Position <- 0L
  use md5p = new System.Security.Cryptography.MD5CryptoServiceProvider()
  md5p.ComputeHash(memoryStream)
  |> Convert.ToBase64String
   

type Encoding =
  | Gzip of string
  | Flat of string
  | NoEncoding 

let contentType uri = 
  match IO.Path.GetExtension uri with
  | ".css" -> Gzip "text/css"
  | ".js" -> Gzip "application/javascript"
  | ".jpeg" 
  | ".jpg" -> Flat "image/jpeg"
  | ".png" -> Flat "image/png"
  | ".gif" -> Flat "image/gif"
  | ".htm"
  | ".html" -> Gzip "text/html"
  | _ -> NoEncoding

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
        match contentType uri with
        | Flat contentType ->
          blob.Properties.ContentType <- contentType
          blob.UploadFromFile(path, options = opts)
        | NoEncoding ->
            blob.UploadFromFile(path, options = opts)
        | Gzip contentType ->
          blob.Properties.ContentType <- contentType
          blob.Properties.ContentEncoding <- "gzip"
          use file = IO.File.OpenRead(path)
          use blobStream = blob.OpenWrite(options = opts)
          use gzip = new IO.Compression.GZipStream(blobStream, IO.Compression.CompressionMode.Compress)
          file.CopyTo(gzip)
    else
        let fileMd5 =
          match contentType uri with
          | Flat _ | NoEncoding -> md5 path  
          | Gzip _ -> md5Gzip path
        blob.FetchAttributes()
        if blob.Properties.ContentMD5 <> fileMd5 then
            tracefn "[Media] %s has changed" uri
            match contentType uri with
            | Flat contentType ->
              blob.Properties.ContentType <- contentType
              blob.UploadFromFile(path, options = opts)
            | NoEncoding ->
                blob.UploadFromFile(path, options = opts)
            | Gzip contentType ->
              blob.Properties.ContentType <- contentType
              blob.Properties.ContentEncoding <- "gzip"
              use file = IO.File.OpenRead(path)
              use blobStream = blob.OpenWrite(options = opts)
              use gzip = new IO.Compression.GZipStream(blobStream, IO.Compression.CompressionMode.Compress)
              file.CopyTo(gzip)
          else
            logfn "[Media] Skipping %s" uri

tracef "Upload media"
!! (Path.media </> "**/*.*")
|> Seq.map (ProduceRelativePath Path.root  >> toUri)
|> Seq.toList
|> List.iter uploadMedia

!! (Path.outcontent </> "**/*.*")
|> Seq.map (ProduceRelativePath Path.out >> toUri)
|> Seq.toList
|> List.iter uploadMedia


let upload tag path blobPath name contentType =

  let blob = blog.GetBlockBlobReference(blobPath)

  let fileMd5 = md5Gzip path
  let upload = 
    if blob.Exists() then
      blob.FetchAttributes()
      if blob.Properties.ContentMD5 <> fileMd5 then
        tracefn "[%s] %s has changed" tag name
        true
      else
        logfn "[%s] Skipping %s" tag name
        false
    else
      tracefn "[%s] %s is new" tag name
      true
  if upload then
    let opts = 
      Blob.BlobRequestOptions(
        StoreBlobContentMD5 = Nullable true, 
        UseTransactionalMD5 = Nullable true)
    let upload() =    
      use file = IO.File.OpenRead(path)
      use blobStream = blob.OpenWrite(options = opts)
      use gzip = new IO.Compression.GZipStream(blobStream, IO.Compression.CompressionMode.Compress)
      file.CopyTo(gzip)
    upload()
    blob.Properties.ContentType <- contentType
    blob.Properties.ContentEncoding <- "gzip"
    blob.SetProperties()

let uploadPost post =
  let path = Path.outPosts </> post.Name + ".html"
  let blob = "posts/" + post.Url
  upload "Post" path blob post.Url "text/html"
    
tracef "Upload posts"
posts
|> List.iter uploadPost

md5 (Path.out </> "index.html")
upload "Post" (Path.out </> "index.html") "index.html" "index.html" "text/html"
 

let uploadCategory category =
    let name = Categories.name category
    let path = Path.categories </> name + ".html"
    let blob = "category/" + name
    upload "Category" path blob name "text/html"
      
Categories.categories
|> List.iter uploadCategory 
let uploadAll() =
    let name = "all"
    let path = Path.categories </> name + ".html"
    let blob = "category/" + name
    upload "Category" path blob name "text/html"
 
uploadAll()
upload "Feed" Path.atom "feed/atom" "feed" "application/atom+xml"

