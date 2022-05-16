(*** hide ***)
open System
#load @"..\.paket\load\netstandard2.0\full\FSharp.Data.fsx"
open FSharp.Data
type ZipCode = FSharp.Data.JsonProvider<"http://api.zippopotam.us/GB/EC1">
let dist (lata: decimal,longa: decimal) (latb: decimal, longb: decimal) =
    let x = float (longb - longa) * cos (double (latb + lata)  / 2. * Math.PI / 360.)
    let y = float (latb - lata)
    let z = sqrt (x*x + y*y)
    z * 1.852 * 60. |> decimal

/// Gets latitude/longitude for a returned zip info
let coord (zip: ZipCode.Root) =
    zip.Places.[0].Latitude, zip.Places.[0].Longitude

(** In [the last post](/post/2020/10/07/applicative-computation-expressions) we used
`BindReturn`and `MergeSources` to implement the applicative. The advantage of
implementing them is that it will work for any number of `and!`. 

When using `let!` with a single `and!`, it makes one call to `MergeSources` to pair
arguments and one call to `BindReturn` to pass values to the function.

When using `let!` with two `and!`, it makes two calls to `MergeSources` to tuple
arguments and one call to `BindReturn` to pass values to the function.

This can be expensive, so you can provide specific implementations for given numbers
of parameters to reduce the number of calls.

For two arguments, you can implement `Bind2Return`:
*)
type AsyncBuilder with
    member this.Bind2Return(x: 'a Async,y: 'b Async,f: 'a * 'b -> 'c) : 'c Async =
        this.Bind(Async.StartChild x, fun xa ->
            this.Bind(Async.StartChild y, fun ya ->
                this.Bind(xa, fun xv ->
                    this.Bind(ya, fun yv -> this.Return (f(xv, yv)))
                    )
                )
            
            )
(** its signature is:

    M<'a> * M<'b> * ('a * 'b -> 'c) -> M<'c>

Notice that it is similar to `map2` signature.

We can check that it's working as expected:
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

(** To handle a `let! and! and!` case you have to provide a `Bind3Return` member that 
takes 3 values and a function.
You can also provide a `Bind4Return`, `Bind5Return`... and from my tests there is no limit
in the number of arguments you can test. F# would happily call a `Bind265Return` if it had to.

Those additional functions are occasions to optimize the implementation by reducing the
number of intermediate allocations and function calls.

Obviously, it's advised to provide only a few special cases and fall back to `BindReturn`
and `MergeSources` for the rest.


Other options for implementation is to provide `MergeSources3`, `MergeSources4` to
reduce the number of intermediate tuples. The signature will be for `MergeSources3`:

    M<'a> * M<'b> * M<'c> -> M<'a * 'b * 'c>

It's also possible to define `Bind2`, `Bind3` with the following signatures:

    M<'a> * M<'b> * ('a * 'b -> M<'c>) -> M<'c>

This looks like the `Bind2Return` except that the function already returns a wrapped value.

You can find the complete documentation in [the RFC](https://github.com/fsharp/fslang-design/blob/master/preview/FS-1063-support-letbang-andbang-for-applicative-functors.md#detailed-design)
*)
