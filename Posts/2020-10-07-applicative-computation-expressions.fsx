(*** hide ***)
open System
#load @"..\.paket\load\netstandard2.0\full\FSharp.Data.fsx"
(** 
In [the last post](/post/2020/10/03/applicatives-irl) we saw how to implement
applicatives using `map`, `map2` and `apply` to define `<!>`and `<*>`operators.

This time, we will use [Computation Expressions](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions)
to achieve the same result. *This is not yet part of F# 5.0, you need the --langversion:preview flag to compile the following code*.

Let's start again with our `Query<'t>`type. 

As a reminder, we created it to access a service that is called with a list of properties
to return for a given document.

This is a mock version of such service. In real world, you'll call Elastic Search
indicating the document id and the properties you need:
*)


let queryService (properties: string Set) : Map<string,string> =
    Map.ofList [
        if Set.contains "firstname" properties then
            "firstname", "John"
        if Set.contains "lastname" properties then
            "lastname", "Doe"
        if Set.contains "age" properties then
            "age", "42"
        if Set.contains "favoritelanguage" properties then
            "favoritelanguage", "F#"
    ]

(**
The problem with this kind of service is that there is usually
no way to be sure that all properties used in the result have been correctly requested.
This types contains both a list of properties to query from an external service
as well a the code using fetched properties to build the result.
*)

type Query<'t> = 
    { Properties: string Set
      Get: Map<string,string> -> 't}


(** It can be used to call service:  *)
let callService (query: Query<'t>) : 't =
    queryService query.Properties
    |> query.Get

(** From here we defined a function to create a query from a single column: *)
module Query =
    let prop name =
        { Properties = Set.singleton name 
          Get = fun m ->
            m.[name] }


(** And the `map` function that applies a given function to the result.  *)

    let map f q =
        { Properties = q.Properties
          Get = fun m -> 
            let value = q.Get m
            f value }

(** We also defined a `map2` to combine two queries as a single. This query
will request the unions of the argument queries properties, and call the 
first query to get its result, the second to get the other result, and pass
both to the given function to combine them:
*)
    let map2 f x y =
        { Properties = x.Properties + y.Properties
          Get = fun m ->
          let vx = x.Get m
          let vy = y.Get m
          f vx vy }

(** With `map2` we can define a `zip` function that takes two `Query` arguments and 
 will combine their results as a pair. We will use this function in our builder.
*)
    let zip x y =
        map2 (fun vx vy -> vx,vy) x y


(** Computation Expressions are created using types that implement specific
members corresponding to the different operations. For applicatives, we
new to implement `BindReturn` with the following signature:

    M<'a> * ('a -> 'b) -> M<'b>

Where `M` in our case is `Query`. You should spot that it's the same signature
as `map` (with the function as the second argument).

The second one is `MergeSources` and is used to zip parameter together:

    M<'a> * M<'b> -> M<'a * 'b>

Here we will use the `zip` function we defined before.

Here is the builder definition:
*)
type QueryBuilder() =
    
    member _.BindReturn(x : Query<'a>,f: 'a -> 'b) : Query<'b> = 
        Query.map f x
    
    member _.MergeSources(x : Query<'a>,y: Query<'b>) : Query<'a * 'b> = 
        Query.zip x y

let query = QueryBuilder()

(** For our sample, use define a User and the basic properties
defined by the service:
*)
type User =
    { FullName: string 
      Age: int
      FavoriteLanguage: string}

module Props =
    let firstname = Query.prop "firstname"
    let lastname = Query.prop "lastname"
    let age = Query.prop "age" |> Query.map int
    let favoriteLanguage = Query.prop "favoritelanguage" 


(** Is is now possible to use the query computation expression to compute
new derived properties. Here we define fullname that queries firstname
and last name and append them together. When using this derived property,
it will request both firstname and last properties from the service.
*)
module DerivedProps =
    let fullname = 
        query {
            let! firstname = Props.firstname
            and! lastname = Props.lastname
            return firstname + " " + lastname
        }
(** you can notice that we use `let!` and `and!` here.

The meaning of `let!` is: Give this name (here firstname) to the value **inside** the structure on the right (the query).
Since we have a `Query<string>` on the right, firstname will be a `string`.

The `and!` means: and at the same time, give this name to this value **inside** this other structure on the right.

This is *at the same time* extracting both values with zip. The actuall code looks like this:
*)
query.BindReturn(
    query.MergeSources(Props.firstname, Props.lastname), 
    fun (firstname, lastname) -> firstname + " " + lastname)

(** We can the compose queries further by reusing derived properties inside new queries: *)
let user =
    query {
        let! fullname = DerivedProps.fullname
        and! age = Props.age
        and! favoriteLanguage = Props.favoriteLanguage
        return 
            { FullName = fullname
              Age = age 
              FavoriteLanguage = favoriteLanguage }
    }

callService user

(** Let's use it for async.

We define a `BindReturn` and a `MergeSources` member.
Using a type extension, it is not advised to use `async {}`
blocks in the implementation because it can go recursive...

I still put the equivalent construct as a comment:
*)

type AsyncBuilder with
    member _.BindReturn(x: 'a Async,f: 'a -> 'b) : 'b Async = 
        // this is the same as:
        // async { return f v }
        async.Bind(x, fun v -> async.Return (f v))

    member _.MergeSources(x: 'a Async, y: 'b Async) : ('a * 'b) Async =
        // this is the same as:
        // async {
        //    let! xa = Async.StartChild x
        //    let! ya = Async.StartChild y
        //    let! xv = xa  // wait x value
        //    let! yv = ya  // wait y value
        //    return xv, yv // pair values
        // }
        async.Bind(Async.StartChild x,
            fun xa ->
                async.Bind(Async.StartChild y,
                    fun ya ->
                        async.Bind(xa, fun xv ->
                            async.Bind(ya, fun yv ->
                                async.Return (xv,yv)
                            
                            )
                        )
                )
            )

(** The zippopotam.us service returns informations about zip codes.
We will use the JsonProvider to load the data asynchronously and parse the result.

*)
open FSharp.Data
type ZipCode = FSharp.Data.JsonProvider<"http://api.zippopotam.us/GB/EC1">

/// Gets latitude/logitude for a returned zip info
let coord (zip: ZipCode.Root) =
    zip.Places.[0].Latitude, zip.Places.[0].Longitude

(** We use [The pythagorean theorem](https://stackoverflow.com/questions/1664799/calculating-distance-between-two-points-using-pythagorean-theorem)
to compute the distance given latitude and longitude of two points:
*)
let dist (lata: decimal,longa: decimal) (latb: decimal, longb: decimal) =
    let x = float (longb - longa) * cos (double (latb + lata)  / 2. * Math.PI / 360.)
    let y = float (latb - lata)
    let z = sqrt (x*x + y*y)
    z * 1.852 * 60. |> decimal


(** Now using `let!` `and!` we fetch and compute the coodinates of paris and london
in parallel and the use both results to get the distance:
*)
async {
    let! parisCoords = 
        async {
            let! paris = ZipCode.AsyncLoad "http://api.zippopotam.us/fr/75020"
            return coord paris }
    and! londonCoords = 
        async { 
            let! london = ZipCode.AsyncLoad "http://api.zippopotam.us/GB/EC1"
            return coord london}
    
    return dist parisCoords londonCoords
}
|> Async.RunSynchronously

(** It's obviously possible to use both Computation Expressions and the approach with
operators from the last post for more fun !
*)





