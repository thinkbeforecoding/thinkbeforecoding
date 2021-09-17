// Learn more about F# at http://fsharp.org

open System
open Fake.Core
open Fake.IO.FileSystemOperators
open Fake.IO
open Fake.IO.Globbing.Operators
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open FSharp.Control.Tasks.V2.ContextInsensitive
open Blog


type Encoding =
  | Gzip of string
  | Flat of string
  | NoEncoding 

[<EntryPoint>]
let main argv =
    
    let container =
       match Environment.GetCommandLineArgs() with
       | [| _; container |] -> 
            tracefn "Uploading to container %s" container
            container

       | _ -> failwith "provide a container name"

    let account = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse "***"
    let blobClient = account.CreateCloudBlobClient()
    let blog = blobClient.GetContainerReference(container)

    let toUri (u: string) = u.Replace(@".\","").Replace(@"\","/")
    let md5 path = 
      use md5p = System.Security.Cryptography.MD5.Create()
      use stream = IO.File.OpenRead(path)
      md5p.ComputeHash(stream)
      |> Convert.ToBase64String


    let contentType uri = 
      match IO.Path.GetExtension(uri: string) with
      | ".css" -> Gzip "text/css"
      | ".js" -> Gzip "application/javascript"
      | ".jpeg" 
      | ".jpg" -> Flat "image/jpeg"
      | ".png" -> Flat "image/png"
      | ".gif" -> Flat "image/gif"
      | ".htm"
      | ".html" -> Gzip "text/html"
      | _ -> NoEncoding
    let cachecontrol = "public, max-age=31536000"
    let uploadMedia tag (blog: CloudBlobContainer) path =
      task {
        let outPath = Path.out
        let uri =
            path 
            |> Fake.IO.Path.toRelativeFrom (Path.out)
            |> toUri

        let opts = 
          Blob.BlobRequestOptions(
            StoreBlobContentMD5 = Nullable true, 
            UseTransactionalMD5 = Nullable true
            )
        let blob = blog.GetBlockBlobReference(uri)
        let! exists = blob.ExistsAsync() 
        if not exists then
            tracefn "[%s] %s is new" tag uri
            blob.Properties.CacheControl <- cachecontrol
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

            if blobMd5 <> fileMd5 || blob.Properties.CacheControl <> cachecontrol  then
                tracefn "[%s] %s has changed" tag uri
                blob.Properties.CacheControl <- cachecontrol
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
                logfn "[%s] Skipping %s" tag uri
      }

    let removeFirstSlash (url: string) =
      if url.StartsWith("/") then
        url.Substring(1)
      else
        url

    let upload (blog: CloudBlobContainer) tag path blobPath name contentType cache =
      task {
      let blob = blog.GetBlockBlobReference(removeFirstSlash blobPath)

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
            tracefn "[%s] %s has changed" tag name 
            return true
          else
            logfn "[%s] Skipping %s" tag name
            return false
        else
          tracefn "[%s] %s is new" tag name
          return true }
      let cachectl = if cache then cachecontrol else null
      if upload || blob.Properties.CacheControl <> cachectl  then
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
        if blob.Metadata.ContainsKey("md5") then
          blob.Metadata.["md5"] <- fileMd5
        else
          blob.Metadata.Add("md5", fileMd5)
        do! upload()
        blob.Properties.ContentType <- contentType
        blob.Properties.ContentEncoding <- "gzip"
        blob.Properties.CacheControl <- cachectl
        do! blob.SetPropertiesAsync()
      }

    let uploadPost blog (post: Post) =
      let path = Path.outPosts </> post.Filename + ".html"
      let blob = post.FullUrl
      upload blog "Post" path blob post.Url "text/html" false

    let uploadCategory blog category =
        let name = Category.name category
        let path = Path.categories </> name + ".html"
        let blob = "category/" + name
        upload blog "Category" path blob name "text/html" false

    let uploadAll blog =
        let name = "all"
        let path = Path.categories </> name + ".html"
        let blob = "category/" + name
        upload blog "Category" path blob name "text/html" false

    let run =
      task {
        let! _ = blog.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Blob, null, null)

          
        tracefn "Upload media"
        let media =
          !! (Path.outmedia </> "**/*.*")
          |> Seq.toList

        for m in media do
          do! uploadMedia "Media" blog m

        let content =
          !! (Path.outcontent </> "**/*.*")
          |> Seq.toList

        for c in content do
          do! uploadMedia "Content" blog c

        tracefn "Upload posts"
        for post in posts do
          do! uploadPost blog post

        do! upload blog "Post" (Path.out </> "index.html") "index.html" "index.html" "text/html" false
         
        for c in categories do
          do! uploadCategory blog c
         
        do! uploadAll blog
        do! upload blog "Feed" Path.atom "feed/atom" "feed" "application/atom+xml" true
      }

    run.Wait()
    0 // return an integer exit code
