(**

This is a little known pattern I learned a few years ago that is taking more value these days.

## Context

When attending [BuildStuff](http://buildstuff.lt/2013) second edition in 2013, I had to choose a workshop,
and [Michael Feathers](https://twitter.com/mfeathers)' one drew my attention. The title **Error Proofing Software**
was a pledge we would dig into the dreaded areas of software development, and this is what I was looking for.

I was amazed by the soundness of Michael's insights in these intricate matters. 
It changed my programming understanding deeply.

## Errors

I was already interested in errors through my DDD approach, like in this 
2009 post [Business Errors are Just Ordinary Events](/post/2009/12/10/Business-Errors-are-Just-Ordinary-Events).

But we went further in the classification.

### Business Errors/Events

**What happened:** The business process reached a corner case

**What should we do ?** Alert the business to take care of the customer

**Handling:** An automatic or manual business process will take care of it

### Technical Errors

**What happened :** The infrastructure had a problem, a resource cannot be reached...

**What should we do ?** Alert the Ops to fix the infrastructure.

**Handling:** Retry until it's fixed or degrade the service gently

### Bugs

**What happened :** Something that was totally unexpected happened and there is no code to handle it.

**What should we do ?** Alert the developers

**Handling:** Fix the bug and release to production

## Categorization

One of the challenge is then to categorize errors.

Most errors are triggered through exceptions, and exceptions are notoriously difficult to filter correctly.

Some languages like Java force exception declaration in methods signature, which is a good thing to document
what could go wrong, but also appears to be difficult to manage since any adding a new exception becomes
a breaking change.

Filtering exceptions based on classes and/or error message is very fragile, and frankly tedious.

## Hexagonal Architecture

One of the promises of Hexagonal Architecture is to have a clean domain, void of any infrastructural or
technical concern.

By providing interfaces to the domain, it can be abstracted to run on any infrastructure.

It makes it also easier to test with Fakes and Mocks.

One of the problem with Hexagonal Architecture, is that when the Domain code calls a method on an interface,
some technical concerns can leak under the form of exceptions or errors... What should the domain do ?

## Functional Core

In functional programming, functions are [Pure](https://en.wikipedia.org/wiki/Pure_function).

It means:

* The output is always the same for the same inputs. It cannot depend on hidden state, internal or external (IO). 
* It doesn't cause any observable side effects.

We can get rid of the Hexagonal Architecture problems by doing things like that:
*)
(*** hide ***)
type BusinessInput = | BusinessInput
type BusinessOutput = | BusinessOutput
let pureFunction (input: BusinessInput) : BusinessOutput  = failwith "not implemented"
let fetchData _ = failwith "not implemented"
let saveData _ = failwith "not implemented"
(*** show ***)
let business input =
    pureFunction input

let service parameters =
    let input = fetchData parameters  // this is not pure, gets data from IO
    let output = business input // this is a pure function
    saveData output // this is not pure, saves data to IO 


(**
In a pure functional language, the fetchData and saveData parts will use the IO monad for IO effects.
In F# or non functional languages, developers have to take care to write side effect free code in the
business function. In F# it's quite easy since types are immutable by default.

The structure of the service function is really interesting from a Error handling point of view :

* The **fetchData** function just fetch data, and can fail only for technical reasons.
* The **business** function computes values. It can return a result indicating a business corner case, or raise
 an exception in case of programming error, a bug.
* The **saveData** function just saves the data, send messages etc, and can fail also for technical reasons.

It is also really interesting from a **transactional** perspective, since no external side effects are produced before
the saveData call. It is easy to put it in a transaction and rollback in case of failure.

Most functional language have a way to return business corner cases gently from the business function.
We use the Either monad in Haskell or the Result type in F#:
*)
type Result<'a,'e> =
    | Ok of 'a
    | Error of 'e
(** 
You can read Scott Wlaschin's excellent post on the subject: [Railway oriented programming](https://fsharpforfunandprofit.com/posts/recipe-part2)

By implementing a functional core, we can easily differentiate errors, adapt error handling strategy in each part 
fetchData/business/saveData, have a clear transaction strategy.

The code is also easier to test since Pure function have very deterministic behaviors, can be run in parallel and 
easily fit with Property Based Testing. And getting rid of Fakes and Mocks is always a win.

## Up to the sky

This done, the business function can easily be run in different environments by placing it between different
versions of fetchData/saveData.

One of the best scenario for this is Azure Functions / AWS lambdas or any serverless environments.

This architectures already implement the data in/data out shell. Just add some code to feed function input 
to the business function, and transform output slightly before returning it.

You can watch my talk on the subject at NewCrafts Paris 2017: 
[Lambdas, the Architectural Way to Serverless](http://videos.ncrafts.io/video/223267154) 

## Gotcha

One usual question with this pattern is: 
    
> What if I need business rules to know what data to fetch ?

The answer is simple: 

> Do it in several steps.
*)
(*** hide ***)

(*** show ***)
let determineNeededResouces input =
    pureFunction input

let useData input =
    pureFunction input

let multiStepService parameters =
    let input = fetchData parameters
    let neededResources = determineNeededResouces input // pure
    let allNeededData = fetchData neededResources
    let ouput = business allNeededData // pure
    saveData ouput

(**
## Wrap up

The code with error handling/reporting should look like
*)
(*** hide ***)
let retryOrReportToOps ex = failwith "not implemented"
let alertDevThatThereIsABug ex = failwith "not implemented"
let business' input = failwith "Not implemented"
let alertBusinessOrStartCornerCaseProcess case = failwith "Not implemented"
(*** show ***)
let serviceWithReporting parameters =

    let input =
        try
            fetchData parameters
        with
        | ex -> retryOrReportToOps ex

    let output =
        try
            business' input
        with
        | ex -> alertDevThatThereIsABug ex

    try
        match output with
        | Ok result -> saveData result
        | Error case -> alertBusinessOrStartCornerCaseProcess case
    with
    | ex -> retryOrReportToOps ex

(**
More about the this pattern in [Part 2](/post/2018/02/01/functional-core-2)
*)