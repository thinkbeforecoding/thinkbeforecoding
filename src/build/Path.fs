[<AutoOpen>]
module FileSystem
open System

let (</>) x y =IO.Path.Combine(x,y)
module Path =
  let root = IO.Path.GetFullPath (__SOURCE_DIRECTORY__ </> ".." </> "..")
  let out = root </> "output"

  let outPosts = out </> "post"
  let posts = root </> "Posts"
  let media = root </> "public"
  let content = root </> "content"
  let categories = out </> "category"
  let outmedia = out </> "public"
  let outcontent = out </> "content"
  let feed = out </> "feed"
  let atom = feed </> "atom.xml"
  let tmp = root </> "tmp"
  let fonts = content </> "fonts"


  let clean (str: string) = str.Replace(":","")