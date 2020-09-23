module Feed
open System
// #load "./.fake/blog.fsx/intellisense.fsx" 
// #if !FAKE
//   #r "netstandard"
// #endif

//#r "packages/FSharp.Data/lib/netstandard2.0/FSharp.Data.dll"
#load ".paket/load/netcoreapp3.1/full/FSharp.Data.fsx"
#r "System.Xml.Linq"
open System
open FSharp.Data
open System.Security.Cryptography

[<Literal>]
let feedXml = """<?xml version="1.0" encoding="utf-8"?>
<feed xmlns="http://www.w3.org/2005/Atom"
  xmlns:dc="http://purl.org/dc/elements/1.1/"
  xmlns:wfw="http://wellformedweb.org/CommentAPI/"
  xml:lang="en">
  
  <title type="html">Think Before Coding</title>
  <link href="http://thinkbeforecoding.com:82/feed/atom" rel="self" type="application/atom+xml"/>
  <link href="http://thinkbeforecoding.com/" rel="alternate" type="text/html"
  title=""/>
  <updated>2017-12-09T01:20:21+01:00</updated>
  <author>
    <name>Jérémie Chassaing</name>
  </author>
  <id>urn:md5:18477</id>
  <generator uri="http://www.dotclear.net/">Dotclear</generator>
  <entry>
    <title>fck: Fake Construction Kit</title>
    <link href="http://thinkbeforecoding.com/post/2016/12/04/fck%3A-Fake-Construction-Kit" rel="alternate" type="text/html"
    title="fck: Fake Construction Kit" />
    <id>urn:md5:d78962772329a428a89ca9d77ae1a56b</id>
    <updated>2016-12-04T10:34:00+01:00</updated>
    <author><name>Jérémie Chassaing</name></author>
        <dc:subject>f</dc:subject><dc:subject>FsAdvent</dc:subject>    
    <content type="html">    &lt;p&gt;Yeah it's christmas time again, and santa's elves are quite busy.&lt;/p&gt;
ll name: Microsoft.FSharp.Core.Operators.not&lt;/div&gt;</content>
      </entry>
  <entry>
    <title>Ukulele Fun for XMas !</title>
    <link href="http://thinkbeforecoding.com/post/2015/12/17/Ukulele-Fun-for-XMas-%21" rel="alternate" type="text/html"
    title="Ukulele Fun for XMas !" />
    <id>urn:md5:5919e73c387df2af043bd531ea6edf47</id>
    <updated>2015-12-17T10:44:00+01:00</updated>
    <author><name>Jérémie Chassaing</name></author>
        <dc:subject>F#</dc:subject>
    <content type="html">    &lt;div style=&quot;margin-top:30px&quot; class=&quot;container row&quot;&gt;
lt;/div&gt;</content>
      </entry>
</feed>"""

type Rss = XmlProvider<feedXml>
let links: Rss.Link[] = [|
    Rss.Link("https://thinkbeforecoding.com/feed/atom","self", "application/atom+xml", null)
    Rss.Link("https://thinkbeforecoding.com/","alternate", "text/html", "thinkbeforecoding")
    |]

let entry title link date content = 
    let md5Csp = MD5CryptoServiceProvider.Create()
    let md5 =
        md5Csp.ComputeHash(Text.Encoding.UTF8.GetBytes(content: string))
        |> Array.map (sprintf "%2x")
        |> String.concat ""
        |> (+) "urn:md5:"

    Rss.Entry(
        title,
        Rss.Link2(link, "alternate", "text/html", title),
        md5,
        DateTimeOffset.op_Implicit date,
        Rss.Author2("Jérémie Chassaing"),
        [||],
        Rss.Content("html", content)
        )
let feed entries =
    Rss.Feed("en", 
        Rss.Title("html","thinkbeforecoding"), 
        links,DateTimeOffset.UtcNow, 
        Rss.Author("Jérémie Chassaing"),
        "urn:md5:18477",
        Rss.Generator("https://fsharp.org","F# script"),
        List.toArray entries
         )
