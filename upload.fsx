#r "paket:
framework: netstandard2.0
source https://api.nuget.org/v3/index.json

nuget Fake.Core
nuget Fake.Core.Trace
nuget Fake.IO.FileSystem
nuget WindowsAzure.Storage
nuget TaskBuilder.fs // "
#if !FAKE
#r "netstandard"
#endif

#load ".fake/upload.fsx/intellisense.fsx"

#load "Categories.fsx"
#load "posts.fsx"

open System
open Fake.Core
open Fake.IO.FileSystemOperators
open Fake.IO
open Fake.IO.Globbing.Operators
open Posts
open Categories
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open FSharp.Control.Tasks.V2.ContextInsensitive


let account = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse "***"
let blobClient = account.CreateCloudBlobClient()
let blog = blobClient.GetContainerReference("gzipblog")

let toUri (u: string) = u.Replace(@".\","").Replace(@"\","/")
let md5 path = 
  use md5p = new System.Security.Cryptography.MD5CryptoServiceProvider()
  use stream = IO.File.OpenRead(path)
  md5p.ComputeHash(stream)
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

let uploadMedia (blog: CloudBlobContainer) path =
  task {
    let uri =
        path 
        |> Fake.IO.Path.toRelativeFrom Path.out
        |> toUri

    let opts = 
      Blob.BlobRequestOptions(
        StoreBlobContentMD5 = Nullable true, 
        UseTransactionalMD5 = Nullable true
        )
    let blob = blog.GetBlockBlobReference(uri)
    let! exists = blob.ExistsAsync() 
    if not exists then
        Trace.tracefn "[Media] %s is new" uri
        match contentType uri with
        | Flat contentType ->
          blob.Properties.ContentType <- contentType
          do! blob.UploadFromFileAsync(path, null, opts, null)
        | NoEncoding ->
          do! blob.UploadFromFileAsync(path, null, opts, null)
        | Gzip contentType ->
          blob.Properties.ContentType <- contentType
          blob.Properties.ContentEncoding <- "gzip"
          use file = IO.File.OpenRead(path)
          use! blobStream = blob.OpenWriteAsync(null, opts, null)
          use gzip = new IO.Compression.GZipStream(blobStream, IO.Compression.CompressionMode.Compress)
          do! file.CopyToAsync(gzip)
    else
        let fileMd5 = md5 path
        do! blob.FetchAttributesAsync()
        let blobMd5 =
          match contentType uri with
          | Flat _ | NoEncoding -> blob.Properties.ContentMD5  
          | Gzip _ -> 
              match blob.Metadata.TryGetValue "md5" with
              | true, v -> v
              | _ -> ""

        if blobMd5 <> fileMd5 then
            Trace.tracefn "[Media] %s has changed" uri
            match contentType uri with
            | Flat contentType ->
              blob.Properties.ContentType <- contentType
              do! blob.UploadFromFileAsync(path, null, opts, null)
            | NoEncoding ->
              do! blob.UploadFromFileAsync(path, null, opts, null)
            | Gzip contentType ->
              if blob.Metadata.ContainsKey "md5" then
                blob.Metadata.["md5"] <- fileMd5
              else
                blob.Metadata.Add("md5", fileMd5)

              use file = IO.File.OpenRead(path)
              use! blobStream = blob.OpenWriteAsync(null, opts, null)
              use gzip = new IO.Compression.GZipStream(blobStream, IO.Compression.CompressionMode.Compress)
              do! file.CopyToAsync(gzip)
              blob.Properties.ContentType <- contentType
              blob.Properties.ContentEncoding <- "gzip"
              do! blob.SetPropertiesAsync()
          else
            Trace.logfn "[Media] Skipping %s" uri
  }


let upload (blog: CloudBlobContainer) tag path blobPath name contentType =
  task {
  let blob = blog.GetBlockBlobReference(blobPath)

  let fileMd5 = md5 path
  let! upload = 
    task {
    let! exists = blob.ExistsAsync()
    if exists then
      do! blob.FetchAttributesAsync()
      let blobMd5 =
        match blob.Metadata.TryGetValue "md5" with
        | true, v -> v
        | _ -> ""

      if blobMd5 <> fileMd5 then
        Trace.tracefn "[%s] %s has changed" tag name 
        return true
      else
        Trace.logfn "[%s] Skipping %s" tag name
        return false
    else
      Trace.tracefn "[%s] %s is new" tag name
      return true }
  if upload then
    let opts = 
      Blob.BlobRequestOptions(
        StoreBlobContentMD5 = Nullable true, 
        UseTransactionalMD5 = Nullable true)
    let upload() =    
      task {
      use file = IO.File.OpenRead(path)
      use! blobStream = blob.OpenWriteAsync(null, opts, null)
      use gzip = new IO.Compression.GZipStream(blobStream, IO.Compression.CompressionMode.Compress)
      do! file.CopyToAsync (gzip) }
    if blog.Metadata.ContainsKey("md5") then
      blob.Metadata.["md5"] <- fileMd5
    else
      blob.Metadata.Add("md5", fileMd5)
    do! upload()
    blob.Properties.ContentType <- contentType
    blob.Properties.ContentEncoding <- "gzip"
    do! blob.SetPropertiesAsync()
  }

let uploadPost blog post =
  let path = Path.outPosts </> post.Name + ".html"
  let blob = "posts/" + post.Url
  upload blog "Post" path blob post.Url "text/html"

let uploadCategory blog category =
    let name = Categories.name category
    let path = Path.categories </> name + ".html"
    let blob = "category/" + name
    upload blog "Category" path blob name "text/html"

let uploadAll blog =
    let name = "all"
    let path = Path.categories </> name + ".html"
    let blob = "category/" + name
    upload blog "Category" path blob name "text/html"

let run =
  task {
    let! _ = blog.CreateIfNotExistsAsync()

      
    Trace.tracefn "Upload media"
    let media =
      !! (Path.outmedia </> "**/*.*")
      |> Seq.toList

    for m in media do
      do! uploadMedia blog m

    Trace.tracefn "Upload posts"
    for post in posts do
      do! uploadPost blog post

    do! upload blog "Post" (Path.out </> "index.html") "index.html" "index.html" "text/html"
     
    for c in categories do
      do! uploadCategory blog c
     
    do! uploadAll blog
    do! upload blog "Feed" Path.atom "feed/atom" "feed" "application/atom+xml"
  }

run.Wait()