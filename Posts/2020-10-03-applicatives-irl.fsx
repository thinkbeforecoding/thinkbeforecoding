(*** hide ***)
let basicApply f x = f x
module Options =
(** 
*This is the content of the presentation of the same name I did
at Open FSharp (San Francisco) and NewCrafts (Paris)*

Monads are all the rage in FP land... but Applicatives are also
very useful and quite easy to get. We'll go through 3 real use cases
to discover what they're up to.

Examples will be in F#. This is of course applicable in other languages
with [currying](https://en.wikipedia.org/wiki/Currying).

## Options

We start with a simple structure: `Option<'t>`. As a reminder,
its definition is
*)
 type Option<'t> =
     | Some of 't
     | None
(** It's a value that either contains a value of type `'t` (the `Some` case)
or doesn't contain a value (`None`)

### map
We can easily write a `map` function for this structure.

`map` takes a `'a -> 'b` function and applies it to the inner value of the 
option if any. If the option is `None`, it simply returns `None`.
*)
 let map (f: 'a -> 'b) (x: Option<'a>) : Option<'b> =
     match x with
     | Some value -> Some (f value)
     | None -> None
(** when looking at map as a function taking a single parameter its
signature is 

    ('a -> 'b) -> (Option<'a> -> Option<'b>)

`map` takes a `('a -> 'b)` function and change it in a `(Option<'a> -> Option<'b>)` function.

### map2

`map` takes a single argument function `f` and make it Option aware.
But what about a function taking two arguments ?

We can define a map2 function with the following signature:

    ('a -> 'b -> 'c) -> (Option<'a> -> Option<'b> -> Option<'c>)

It takes the function and calls it with the value of the two other
Option parameter if they both have a value. It returns `None` if any is `None`.
*)
 let map2 (f: 'a -> 'b -> 'c) (x: Option<'a>) (y: Option<'b>) : Option<'c> =
     match x,y with
     | Some xvalue, Some yvalue -> Some (f xvalue yvalue)
     | _ -> None
(**
### map3, map4...
For a 3 argument function, a 4 argument function we'd then have to write
extra `map` functions. That would be doable but tedious.
And where should we stop ?

Let's take a simple two argument function and se what happens when
we use `map` on it:
*)
 let add x y = x + y

 let mappedAdd = map add
(** Notice first that it compiles because `add` can be seen as
a one argument function returning another 1 argument function:

    let add x =
        fun y -> x + y

So its signature can be read as `(int -> (int -> int))`

Using map, it will become:

    Option<int> -> Option<int -> int>

If you call it with the value `Some 3`, the result will be `Some` function
that take an `int` and add 3 to it. Called with `None`, you get no function.

Lets take the most basic function application: 
*)
 let basicApply f x = f x
(** `basicApply` is a function that take a function f, a value v, and
pass the value v to the function f (this is the F# `<|` operator).

we can make it work with option using map2:
    
*)
 let apply f x = map2 basicApply f x
(** `basicApply` signature is

    ('a -> 'b) -> 'a -> 'b

and we can look at it as a 2 argument function:
    
    (('a -> 'b) -> 'a) -> 'b

And map2 will change it to:
    
    (Option<'a -> 'b> -> Option<'a>) -> Option<'b>

This is a function that take an optional function as first
argument and an optional value as a second argument. If both
have a value, the value is passed to the function. If any is `None`
the result is `None`.
*)
 let partialAdd = map add (Some 3) // Some(fun y -> 3 + Y)
 apply partialAdd (Some 5) // Some(3 + 5) => Some 8
(*** include-it ***)
(** The nice thing is that it works for function with more than 2 arguments
since any function can be considered as a function of 1 arguement.
*)
 type User =
     { FirstName: string
       LastName: string
       Age: int
       FavoriteLanguage: string }
 let user firstname lastname age favoriteLang =
     { FirstName = firstname
       LastName = lastname
       Age = age
       FavoriteLanguage = favoriteLang }

 let user' = map user (Some "John") // Option<string -> int -> string -> User>
 let user'' = apply user' (Some "Doe") // Option<int -> string -> User>
 let user''' = apply user'' (Some 42) // Option<string -> User>
 apply user''' (Some "F#") // Option<User>
(*** include-it ***)
(** Hopefully we can simplify the writing by defining two infix
operators for `map` and `apply`.
*)
 let (<!>) = map
 let (<*>) = apply
(** Now we can write *)
 user
 <!> Some "John"
 <*> Some "Doe"
 <*> Some 42
 <*> Some "F#"

(*** hide ***)
module Results =
(** this is very usefull with validation, and you can easily do the same thing for `Result<'a,string>`
type, a type that has either a value of type `'a`, or an error message. 
`map` is already defined and we have to write a map2: *)
 let map2 (f: 'a -> 'b -> 'c) 
          (xresult: Result<'a, string>)
          (yresult: Result<'b, string>) 
          : Result<'c, string> =
    match xresult, yresult with
    | Ok x, Ok y -> Ok (f x y)
    | Error fe, Ok _ -> Error fe
    | Ok _, Error xe -> Error xe
    | Error fe, Error xe -> Error (fe + "\n" + xe)

 let apply f x = map2 basicApply f x

 let (<!>) = Result.map
 let (<*>) = apply

 open System

 let notEmpty prop value =
     if String.IsNullOrEmpty value then
         Error (sprintf "%s should not be empty" prop)
     else
         Ok value
 
 let tryParse prop input =
    match Int32.TryParse(input: string) with
    | true, value -> Ok value
    | false, _ -> Error (sprintf "%s should be an number" prop)

  // define a user as previously
 type User =
     { FirstName: string
       LastName: string
       Age: int
       FavoriteLanguage: string }
 let user firstname lastname age favoriteLang =
     { FirstName = firstname
       LastName = lastname
       Age = age
       FavoriteLanguage = favoriteLang }


 user
 <!> notEmpty "firstname" "John"
 <*> notEmpty "lastname" "Doe"
 <*> tryParse "age" "42"
 <*> notEmpty "favorite language" "F#"

(*** hide ***)
module Series =
 open System
(** Now try to play with the input values and look at the 
  result. Instead of getting `Ok` with a typed user, you'll get
  an error with a message describing why the user could ne be constructed. Neat!

## Series

Applicatives can be defined on anything on which we can write a map2
functions. Any construct that is **zippable**.

A good example of zippable, non trivial stucture is time series.
Let's define one: 
*)
 type Series<'a> = Series of 'a * (DateTime * 'a) list
(**
It has a initial value of type `'a` and a list of changes
consisting of the date of change and the new value.

For instance we can easily build constant series like this:
*)
 let always x = Series(x, [])
(** it defines a `Series` with initial value x that never change.

We can also define a `map`:
*)
 let map f (Series(initial, changes)) =
    Series( f initial, List.map (fun (d,v) -> d, f v) changes)
(** It applies the function f to the initial value and every change values.

`map2` is a bit more convoluted. The two series start with
an initial value that can be fed to the given function.
After that, one of the series change, and we call f with
the initial value of the one that didn't change an the new value
of the other one.. each time a value change, we use this value and the last known
value of the other one. We use a recursive function for this.
*)
 let map2 f (Series(ix, cx)) (Series(iy, cy)) =
    let rec zip lastx lasty changesx changesy =
        [ match changesx, changesy with
          | [],[] -> () // we're done
          | [], _ ->
             // x won't change anymore
             for d,y in changesy do 
                d, f lastx y
          | _, [] -> 
             // y won't change anymore
             for d,x in changesx do 
                d, f x lasty
          | (dx,x) :: tailx , (dy,y) :: taily ->
            if dx < dy then
                // x changes before y
                dx, f x lasty
                yield! zip x lasty tailx changesy
            elif dy < dx then
                // y changes before x
                dy, f lastx y
                yield! zip lastx y changesx taily
            else
                // x and y change at the same time
                // dx is equal to dy
                dx, f x y
                yield! zip x y tailx taily
        ]
    Series(f ix iy, zip ix iy cx cy)

(** using `map2` we can add two series: *)
 let add x y = x + y
 map2 add
        (Series(0, [DateTime(2020,10,03), 1
                    DateTime(2020,10,05), 5 ]))
        (Series(2, [DateTime(2020,10,03), 2 
                    DateTime(2020,10,04), 3]))
(*** include-it ***)
(** Here is a graphical representation:

             Initial   2020-10-3    4   5
    x:          0              1    -   5
    y:          2              2    3   -
    result:     2              3    4   8

We define `apply` and the two operators as we did for option:
*)
 let apply f x = map2 basicApply f x
 let (<!>) = map
 let (<*>) = apply
(** And we can use it with a user *)
 type User =
     { FirstName: string
       LastName: string
       Age: int
       FavoriteLanguage: string }
 let user firstname lastname age favoriteLang =
     { FirstName = firstname
       LastName = lastname
       Age = age
       FavoriteLanguage = favoriteLang } 

 let birthDate = DateTime(1978,10,01) 
 user
    <!> always "John"
    <*> Series("Dow", [ DateTime(2010,08,17), "Snow"]) // married
    <*> Series(0, [for y in 1 .. 42 -> birthDate.AddYears(y), y ])
    <*> Series("C#", [DateTime(2005,05,01), "F#"])
(** The result is a `Series` of `User` with the new values on each
change.

We can now take any function that work on non-`Series` values, and apply
it to `Series` of values. This is especially useful whith hotels data.
Hotels define prices, availability, closures and all other properties for their
rooms for each night in a calendar. Each property can be modeled as a Series.

Let's compute the potential sells for a room:
*)
 let potentialSells availability price closed =
     if closed then
         0m
     else
         decimal availability * price
(** This function has no notion of time and Series.

Now we want to apply it for every nights: *)
 let availability = 
    Series(0, [ DateTime(2020,10,03), 5
                DateTime(2020,10,07), 3
                DateTime(2020,10,09), 0])
 let price =
    Series(100m, [ DateTime(2020,10,04), 110m
                   DateTime(2020,10,06), 100m ])
 let closed =
    Series(false, [DateTime(2020,10,5), true
                   DateTime(2020,10,6), false])
 potentialSells
 <!> availability
 <*> price
 <*> closed
(*** include-it ***)
(** Just imagine the headache if we did not have this applicative.. *)
(*** hide ***)
module Queries =
(**
## Queries

The first two examples were structures that "contain" data. For this
third case, we'll consider a different problem.

We have a document store - like elastic search - where we can query documents
and request specific properties for the returned document. For instance
in the query for a user we ask for the "firstname", "lastname" and "age" properties
and we get these properties in the result.

For the demo we'll use a simple function:
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
(** The true one would take a document id and actually call the document database.

Despite being very useful (we get only the properties we're interested in), this kind
of interface often leads to irritating bugs. When accessing the result, we must be sure that
all the properties we use have been correctly requested. And when we stop using a property,
we should not forget to remove it from the query.

That would be awsome if we could just use the properties and the query would
magically know which one to fetch.

Let's introduce the Query type:
*)
 type Query<'t> =
    { Properties: string Set
      Get: Map<string,string> -> 't }

(** This type is exactly here to do what we want. It contains a list
of properties to query and a function that use the result to extract a value
from it. We have to create simple ways to build it safely.

The first thing is to be able to query a single property
 *)
 let prop name : Query<string> =
      { Properties = set [ name ]
        Get = fun response -> response.[name] }
(** This function takes a property name and builds a Query that:

* has the expected property name in `Properties`
* can extract this property from the result

We can define stock properties like using the `prop` function:
*)
 let firstname = prop "firstname"
 let lastname = prop "lastname"
 let favoriteLanguage = prop "favoritelanguage"
(** We can fetch them with the following function: *)
 let callService (query: Query<'t>) : 't =
    let response = queryService query.Properties
    query.Get response
(** For `age`, we'd like to convert it to an int. This is easily done with
a `map` function that will change the result using a given function
*)
 let map (f: 'a -> 'b) (q: Query<'a>) : Query<'b> =
     { Properties = q.Properties
       Get = fun response ->
               let v = q.Get response
               f v }

 let (<!>) = map

 let age = int <!> prop "age"

(** Building a full name could be done like that 
 (it can be [more complicated](https://shinesolutions.com/2018/01/08/falsehoods-programmers-believe-about-names-with-examples/)): 
*)
 let fullname firstname lastname =
     firstname + " " + lastname

(** We need to retrieve both first name and last name and pass it to the function. 
But we can also make a `map2`:
*)
 let map2 (f: 'a -> 'b -> 'c) (x: Query<'a>) (y: Query<'b>) : Query<'c> =
    { Properties = x.Properties + y.Properties
      Get = fun response ->
               let xvalue = x.Get response
               let yvalue = y.Get response
               f xvalue yvalue 
    }
(** The result is a Query that:

* has the union of the properties of both queries
* get values from both and pass them to the given function

With a map2, we can define apply and the opertor:
*)
 let apply f x = map2 basicApply f x
 let (<*>) = apply
(** With this we can query a user: *)
 type User =
     { FirstName: string
       LastName: string
       Age: int
       FavoriteLanguage: string }
 let user firstname lastname age favoriteLang =
     { FirstName = firstname
       LastName = lastname
       Age = age
       FavoriteLanguage = favoriteLang } 
 
 let userQuery =
     user
     <!> firstname
     <*> lastname
     <*> age
     <*> favoriteLanguage

 callService userQuery
(** using userQuery, we just composed basic properties to form a
query for a larger structure, so we know that we cannot use a property without
requesting it in the query.

## Other applicatives

Applicatives can be found it a lot of places. 
To zip lists, execute async computations in parallel.

It can also be used to create formlets. A `Formlet<'t>` is a UX form to fill a `'t` structure.
The simples formlet are input fields. A text input is a `Formlet<string>`, a checkbox a `Formlet<bool>`.
A label function is a `(string -> Formlet<'a> -> Formlet<'a>)` function that adds a label to an existing formlet.
A `map` function can change `Formlet<string>` to `Formlet<Address>` given a `(string -> Addess)` function. And we use `map2` and `apply` to take several
formlets and compose them in a `Formlet<User>` for instance.

Once you start to see it, you'll spot them everywhere. 

Be careful to not abuse it. For a single use, it's often better to compute the result directly.

But when you have many places that are impacted, especially if the code change often, it can reduce
code complexity by a fair amount.

Don't hesitate to ping me [on twitter](https://twitter.com/thinkbeforecoding) if you find nice uses
of applicatives!

*As suggested by [Chet Husk](https://twitter.com/ChetHusk), I'll soon post about the new
Applicatives support in Computation Expressions.*
*)


























