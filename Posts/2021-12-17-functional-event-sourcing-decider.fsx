(*** hide ***)
open System
(** 
*this post is part of the [F# Advent Calendar 2021](https://sergeytihon.com/2021/10/18/f-advent-calendar-2021/)*

After years of talks and workshops about Functional Event Sourcing, I noticed that I published
only 4 posts on the topic.

The first of them is a [rant in 2013](/post/2013/07/28/Event-Sourcing-vs-Command-Sourcing) about [Martin Fowler's article from 2005](https://www.martinfowler.com/eaaDev/EventSourcing.html)
describing something that could be called Command Sourcing and is misleading people into thinking the are doing Event Sourcing.

The second is a [simple drawing](/post/2014/01/04/Event-Sourcing.-Draw-it) describing the flow of Functional Event Soucing. Really usefull, many people told
me somthing clicked once they saw this picture. But still a bit short. I made a few wording change since then.

I then made two posts about [Monoidal](/post/2014/04/11/Monoidal-Event-Sourcing) [Event Sourcing](/post/2014/04/27/Monoidal-Event-Sourcing-Examples)
trying theoretical ideas to change this [external monoid](http://thinkbeforecoding.github.io/FsUno.Prod/Dynamical%20Systems.html) to a plain monoid.

It was accompanied by many samples on github like the many [FsUno](https://github.com/thinkbeforecoding/UnoCore) implementations, the [crazyeight](https://github.com/thinkbeforecoding/hackyourjob-crazyeights) workshop
as well as the source code of [Crazy Farmers](https://github.com/thinkbeforecoding/crazy)' port for [Board Game Arena](https://boardgamearena.com/gamepanel?game=crazyfarmers) that is using both
Server Side and Client Side event sourcing, the client side being transpiled to JS using Fable, and the server side to PhP using peeble.

I'm currently working on a book about it, but writing books takes times. And digging
the topic led me to discover new thinkgs that make it even longer to write.

So here it is, a beginner introduction to Functional Event Sourcing and it's main
pattern, the Decider.

## Anatomy of a Service

Let's look at a System. Any System.

Without interactions with the outside, such system would be pretty useless.
We can represent these interactions in a generic way as inputs and outputs

    [lang=txt]
                  ┌────────────────────┐
                  │                    │ 
     Inputs       │                    │     Outputs
        ─────────►│       System       │───────────►
                  │                    │
                  │                    │
                  └────────────────────┘

A system that accepts inputs but produce no outputs would be akind of black hole.

    [lang=txt]
                  ┌────────────────────┐
                  │       System       │ 
     Inputs       │                    │ 
        ─────────►│     (the void)     │
                  │                    │
                  │                    │
                  └────────────────────┘

this is not totally useless (cf /dev/null), but quite limited.

On the other hand, a system that produce outputs without inputs seems also very simplistic.
Without introducing time, it would necessarily be a constant:

    [lang=txt]
                  ┌────────────────────┐
                  │                    │  Constant
                  │                    │     Outputs
                  │       System       │───────────►
                  │                    │
                  │                    │
                  └────────────────────┘

So let's introduce time. Lets add a clock to our system.

    [lang=txt]
                  ┌────────────────────┐
                  │  Clock             │  
                  │                    │     Outputs
                  │       System       │───────────►
                  │                    │
                  │                    │
                  └────────────────────┘

The problem of clock is that is is changing over time (obviously), so let's
refactor to treat clock as an input (it also becommes a trigger):

    [lang=txt]
                  ┌────────────────────┐
                  │  Clock    System   │ 
                  │   │                │
                  │   ▼                │    Outputs
                  │  ┌──────────────┐  │
                  │  │  Subsystem   │───────────────►
                  │  └──────────────┘  │
                  └────────────────────┘

Here, our system is a pure function of time. This can be usefull, but most of the
systems we deal with are a bit more complicated.

As we wanted interactions, we can reintroduce inputs as triggers. For instance a user action.

    [lang=txt]
                  ┌───────────────────┐
                  │  Clock    System  │ 
                  │   │               │
     Actions      │   ▼               │    Outputs
                  │  ┌─────────────┐  │
        ────────────►│  Subsystem  │───────────────►
                  │  └─────────────┘  │
                  └───────────────────┘

To go back on the question of time, some time is passed in the system during
code execution. But at today’s execution speed, especially outside of inputs/outputs,
code execution can be considered as instant. To be executed the code needs some trigger to be called and run.

A user action can be such a trigger, in this case, current time can be seen as one parameter of the action.
    
The trigger can also be the clock (a timer, an alarm..) but still the code is called for some reason 
(it's time for the Market to close, a check must be made every 5 mintes...). In this case, this is a scheduled
automatic action, but it's still an action with current time as additional data.


Most of the time, the output not only depends on the input action, but also on what happened before.

The system needs a way to keep a trace of the effects of previous action to react accordingly.

Without such memory, the system is always reacting the same way to the same input action, regardless of
anything that happened in the past. Some systems behave like this, but is a constrained case of more
general systems  that keep a trace of past actions.

This memory is called state and is accessed and modified by the system code. It can be stored
in memory (RAM) or saved to a database. Reading it can be considered a sort of input, changing it a sort of output:

    [lang=txt]
                  ┌───────────────────┐
                  │  Clock    System  │ 
                  │   │               │
     Actions      │   ▼               │    Outputs
                  │  ┌─────────────┐  │
        ────────────►│  Subsystem  │───────────────►
                  │  └─────────────┘  │
                  │     ▲        │    │
                  │     │        ▼    │
                  │  ┌─────────────┐  │
                  │  │    State    │  │
                  │  └─────────────┘  │
                  └───────────────────┘


This is a useful step. The subsystem can be cleard of a lot of technical concerns.
The input interfaces can be abstracted (HTTP API, message queue, UI events, command line argumes,
timers or alarms...) and parameters can be passed to the subsystem directly.
The output interaction can also be abstracted and hidden behind interfaces (Outbound HTTP calls,
messages or notification sending...). State loding an saving can also be hidden behind an interface
to protect the subsystem from implementation details.

Once carrfully cleared from technical concers, the code in the subsystem is merely composed
of the system logic. What we commonly call the business logic, or Domain. We call the rest of the code the
Application layer.

We described here the hexagonal architecture where the application core, the Domain, is loosely
coupled to it's environment through port interfaces, implemented as adapters to the technical infrastructure. 

## Functional Core

We can go a bit further by applying [Functional Core](/post/2018/01/25/functional-core) to our model.
The idea is to write the Subsystem as a pure function. 

The input Actions and Clock will be passed as input values (the current time, if needed in the case of the clock).
Actually we can consider current time as being a parameter of the action, and will not mention it again.

For state, we can also load its current value, and pass it to the function. And the function will return
a new state that can be stored.

Outputs and side effects can also be returned as values by the functions, and the side effects will be performed
by the application layer:

*)
(*** hide ***)
type Action = Action
type State = State
type Output = Output
(*** show ***)
let subsystem (action: Action) (state: State) : State * Output list =
    state, []
(**
This is here, obsviously a system that does nothing. By returning a new state,
it will be persisted for the next call, and by returning output, it will be able to product other side effects.


## Actions and Commands

The Functional Core Domain is called on external triggers we called Actions. We can group them initially in two groups,
Commands and Events. 

A command is an action on the system with an intention to change it. A user places an order,
cancels a subscription, books a hotel room. It can succeed of fail, but should have an effect on the system.

On the other side, something can happen outside of the system that should have an effect on it. The weather changed,
a customer checked out, a cargo leaved a harbor, a scheduled alarm expired. Those things
happened and there's nothing we can do about it. This can trigger a change in the system. We call the External Events.

Our Functional Core domain could directly take External Events as an input, be we can find in the Domain
internal Commands associated. The WeatherChanged event leads to a PrepareForRain command. It's 4PM, the market bell rang, 
this is a CloseMarket command.

In the same way, in user interface code it is advised to not take action directly in event handler's code.
In the onButtonClicked function, it is better to call another DoAction() function that does the actual action.
The code is clearer and the action can then be called on different triggers, and just tested without having to 
raise the event itself.

There is also a distinction between External Command and Internal Command. Current time can be
important to process a command, and will be part of input data. However, current time will not be provided by the Command 
sender as it would be highly unsafe, caller could ante/postdate the Command. The External Command is
enriched with current time in the Application layer. Same thing for external data as exchange rates, weather conditions or
resources protected by access rights. Caller identity should also be used with care, it can be either validated using a password,
access-key, signature or login check.

We will now consider any Domain input action as an internal Command containing all enrichment data
and call it a Command from brevity. A Command can be implemented as a data structure with a name indicating its intent
and containing the associated data. Its name is always a verbal phrase in the imperative.

## Decision

The next step is to look inside the Functional Core Domain and split it even further.

It's a pure function that takes a Command and current State and returns a State an some Outputs.

The usual way to proceed in fulfilling a Command given current State is to modify State according to business rules.
The code checks what the situation is and follows the rules defined by the domain to modify the State in a consistent way.

Doing this, we're going a bit too fast. We take some shortcuts. We take decisions and apply the effects of these decisions at the same time.
There are two quite different steps, and making the conflation obscures the actual decision process.

Letting the decision process implicit proves to be problematic. Looking at data, the business is
wondering why we ended up in current state. Is it a bug or legit actions that produced this unexpected result?
Tracing back observable side effects and saved intermediate state is time consuming and 
cannot always give definitive answers.

### Against log

This often leads to bloating the code with logs. Logs are initially useful, but they don't age well. They are added
on second thought, and seldom tested. Like commands, after a few refactorings, the most often end up outdated, reporting
incorrect and/or partial traces. Having up to date correct logs requires to be careful. On the first carelessness occurrence the
data in the log will become unreliable. This becomes quickly a time hog during diagnosis, especially when unexpected cases happen,
everything need to be double checked, in the traces and in the code that mey have changed in-between.

As an afterthought, their operation is also not designed with care. Log retention is costly, and
choosing the right log level is difficult. Too verbose, logs storage price can become prohibitive, forcing a short retention period.
Not verbose enough, important information can be missing. Cleaning logs is also difficult because not all logs are of equal importance, nor is
everything equally relevant after some time. Sadly log systems rarely give much flexibility in the cleaning/archiving process. All this
becomes useless anyway if the log cannot be trusted.

### Events

To avoid this, we decide to untagle decision taking and state changing. We'll be explicit about
what happens by materlializing decisions outcome before changing state, and changing state only
using data from this decision.

We will express these Commands processing outcomes as Events expressing as past tense verbs what just happend to the system, 
leading to a new state.

A Switch Light On Command will produce Light Switched On Event if any. 
A Transfer Money Command will produce a Money Transfered Event, or a Money Transfer Rejected Event due to insufficient funds.

Decision then just take this simple form:

    [lang=txt]
     Command      ┌───────────────────┐    
        ─────────►│                   │    Events
     State        │      Decide       │─────────►
        ─────────►│                   │
                  └───────────────────┘

If we use simple data structures for Commands, State and Events, the decision can be
implemented as a function with the following signature:
*)
(*** hide ***)
type Command = Command
type Event = Event
(*** show ***)

type Decide = Command -> State -> Event list

(**
Since we decided to not modify state at this stage, the State can be immutable  (read only)
as should Command and Event be.

This function has no reason to produce any side effects, like reading or writing external state, or 
calling external services. We've seen how to deal with external data like current time or exchange rates already.
So it can be implemented as a **pure function**:

* Its return value should depend only on the input parameters
* It should not produce any noticeable external side effect.

The first point is really interesting for testing and reasoning about the code.
Given current State and Command, the resulting Event list is always the same. This is true whatever the day and time and whatever
the current state anywhere else. It eases the tests that will never be
dependent on external concerns. It also eases debugging since if we logged the Command
data and are able to know State at that point in time - which we’ll do - there is only a single possible outcome. This force us to
not depend on any external data that is not already in State or Command parameters, as current time or external data retrieval.

The second point stems from our choice to make decision explicit. If the function produces any side effect, it will break our
principle to first express the decision taken before making any change. We’ll also see why even logging is useless inside this function.

This constraints give a lot of interesting properties.

In summary decision function is a way to write: 

    [lang=txt]
    When asked to process this Command while in given State, here is what happens, expressed as Events. 


## Evolution

We’ve decided to split taking decision and applying resulting changes. The goal was to make the actual decision
taken more explicit in the code base. So in previous part, we devised a decide function that returns Events but do
not actually change State. Now, let’s see how to apprehend change in a simple and highly testable way. This will
actually be quite straight forward.

To compute the new State, we need of course the current State and the Event that just happened. 

We could modify current State to mutate it so it becomes the new State, but this is not how we’ll proceed.
Mutation will not automatically lead you to bad code and bugs, but we can consider it a premature optimization at this point.
If you’re not familiar with Functional Programming, immutability can seem very costly and inefficient, and this can be correct in some contexts.
But most modern languages, functional or not, are fast enough in immutable scenarios compared to querying a database or making a network call.
The result is  that the overhead of immutability at the level of domain code can often be ignored. We’ll see how immutable data structure for state helps reasoning about the code,
and how it has interesting composition properties. So let’s just go on with immutable State, and you’ll decide of your implementation based
on your own stack constraints later.

The decision function was returning an Event list containing zero, one or more events, and we will focus first on how to deal with a single Event. It will be easy from there do handle multiple events.

With immutable read only State data structure and read only Event, it becomes obvious that computing new State will be implemented also as a pure function.
There is now way to change provided current State and Event,
they are read only. The Event and the State should contain all the data we need,
so there is no point fetching data from somewhere else. No external service calls.
Then, mutating data outside would be equivalent to use global state which would defeat the purpose of all this.
We have all data at hand, and just have to compute the new state to return it.
We’ll see further that there is also no reason for things like logging or tracing in this code.

Let’s call this function evolve, its signature is:
*)
type Evolve = State -> Event -> State
(**
It takes a State and an Event and returns a State. It's meaning is: Given current State, when Event occurs, here is the new State.

The code of the evolve function should be extremely simple. Decision as already been taken.
It should probably not be more that setting a field, adding a element to a list, or incrementing a value, or setting/reseting a flag.

The evolve function is core to Event Sourcing but it will rarely be more complicated than a few lines of codes.

## Initial State

For the first Command, we find ourselves in a specific situation. We have a Command and a decide function.
But the decide function need an input state, and we have not State for now that we can pass as a parameter.
Same thing with the evolve function on the first Event.

If we consider the state returned by the evolve function and this first Event, we can obviously say that it's the State
after the first Event occurs. Se the input state is the State before the first Event occurs, when nothing occured yet. We will call it
Initial State.

Initial State is important as it has to be explicitly defined. Some times it can be chosen between possible futher States.
For a light, for instance, it could be On or Off. For an account, it could be defined with a 0 balance. It will just use specific values for the proporties,
 and it is totally possible that it wil be back in the same state later. The light will be Off again, and the account could return to a 0 balance in the future.

 However, the first Event is sometime irreversible. For a card game, before the first event happens,
  the game is not yet started. After the first event, it is started, it will eventually end, bit will not be in the "not started yet state" anymore.
  This situation often arise when some initial properties have to be defined, when an initial actions sets the system up.
  In a card game, the dealer will put the first card on the table. This is anction that can only happen at the beginning, it's the game setup.
  It marks the actual start of the game, before that, not other action can take place.

  In this case, the initial State has not other property than beeing the action Initial State.
  It doesn't need to contain more information. It is easy to represent such state in languages with sum types or
  discriminated unions:
*)
(*** hide ***)
type Card = Card
module  CardGame =
(*** show ***)
  type State =
      | InitialState
      | Started of topCard: Card

  let initialState = InitialState

(**
This indicates that the state is either InitialState without any more information, or the game is Stated with the specified top card.

In languages without sum types, we'll usually use a boolean or enum value to mark the difference:
*)
(*** hide ***)
module CardGame2 =
(*** show ***)
  type State =
    { Started: bool 
      TopCard: Card}
  let initialState =
    { Started = false
      TopCard = Unchecked.defaultof<Card> }
(**
You can notice here that two problem arise. First, we have to be careful not to use the TopCard property
when Started is false. Should we use an exception or a similar error mechanism to prevent this from happening?
Probably, but it would be far better to not be able to write such code in a first place. Second, what is a good
value for TopCard in the initial state when Started is false ? Here I chose a club three which can seems a bit arbitrary,
but any value would be. There is no good Card type value that naturally represents a default value. In some language,
an option would be to use null, but this is also dangerous if null is never a valid value once started.
Null Reference Exceptions are frequent errors that are better avoided from the start. If ready to use null the next option would
be to use it and get rid of Started altogether.

On the pro side, this makes state more compact, and avoids requiring a check on a property to know if the other is valid potentially
leading to consistency problems. On the cons, the initial state is less explicit. The code doesn’t express clearly when it is valid
to have a TopCard or not.

## Stiching it together

Now we have all the parts needed for Event Sourcing. Let's use them together.
We have three types and tree elements described in the previous chapters that have tye following signatures:

    type Command
    type Event
    type State
    initialState: State
    decide: Command -> State -> Event list
    evolve: State -> Event -> State

The Command will come from the outside, but the State and Event list will be fed from the decide function output to
the evolve function input while the output State from the evolve function will be passed to the next call to the decide function.
The output State will also be used as an input parameter for the next call to the evolve function.

    [lang=txt]
             Command  ┌──────────────┐
            ─────────►│              │ Events
             State    │    decide    ├───────┐
            ┌────────►│              │       │
            │         └──────────────┘       │
            │         ┌──────────────┐ Event │
            │  State  │              │◄──────┘
            ├───◄─────┤    evolve    │ State
            │         │              │◄──────┐
            │         └──────────────┘       │
            └────────────────────────────────┘

When the system initially starts, it is in the Initial State. For now, nothing happened yet.

When the first command arrives, we have a Command and a State that we can pass to the decide function.
It will return the outcomes of the Command as a list of Event, the things that happened in reaction to the input Command.

If the output Event list is empty, nothing happened, no need to compute a new state, we are ready for the next Command.
This should not happen frequently for at least two reasons. A system that does mostly nothing is not very interesting and
may probably be implemented without Event Sourcing... But doing nothing can also make some
situation harder to diagnose. When a Command was intended to something but resulted in no change, it can be interesting
to know the reasons. Emitting no Events will make the distinction between an infrastructure problem and a 
motivated decision to do nothing harder to find. Did the system crash, or did it just decide to do nothing?

It is still possible to know by taking the Command that has been persisted for diagnostic and State that can be reconstructed from past Events, pass them to the decide function and see the result, but
it will request careful code analysis or creating a new test with this specific data. This takes on developers time and is better avoided.

If the decision to do nothing results from a business rule, it is advised to make it explicit, and return an Event that will
not affect State. It will clearly appear in the produced Events and diagnostics will be easy. This can be done 
by support teams that have no access to the code.

If nothing happens because we are already in expected state, we are in a case of idempotent Command. In this case, it is actually better to return no Events. It avoids bloating the Event Store with useless duplicate Events. 

When the Event list contains an Event, we can call the evolve function with current State and this Event. It will return the new State, to be used for the next Command instead of the Initial State.

Sometimes the Event list will contain several Events. In this case we will call the evolve function for each event passing previous computed State along.

This could give something like:

    let newState1 = evolve currentState event1
    let newState2 = evolve newState1 event2
    let newState3 = evolve newState2 event3

We can do better, be we now have the next current State and we are ready to process the next Command.

## Fold

When we have several events, we have to call the evolve function for each.

A simple way to do it is to use a loop:
*)
(*** hide ***)
let evolve (state: State) (event: Event) : State = failwith "not implemented"
(*** show ***)
let computeNextState currentState events =
    let mutable state = currentState
    for event in events do
        state <- evolve state event
    state
(**
As you can see, F# makes mutability explicit through the mutable keyworkd. Without it, state cannot be
assigned to a new value... It also use this backward looking left arrow `<-` which seems to suffest that we're
going in the wrong direction. F# IDEs also tend to display mutable variables in oranges to signal them as a kind
of warning...

Another way to proceed without mutation is to use a recursive function that we will call fold: 
*)

let rec fold state events =
    match events with
    | [] ->     // there is no events..
        state   // nothing changed
    | event :: rest ->
        // compute state after first event
        let newState = evolve state event
        // do it again with next events
        fold newState rest 

(**
When the list is empty [] we just return input state. When the list is not empty, we deconstruct the list as event :: rest,
calling event the first Event, and rest the rest of the list. We can then compute the new intermediated State using evolve
and call fold again with this newState and the remaining events rest until we processed them all.
*)

let rec fld f s es =
    match es with
    |[] ->
        s
    | e :: es ->
        let ns = f s e
        fld f ns es

(**
Most languages provide libraries containing an implementation of this function. It is called fold, left fold or aggregate.
It takes a list, an aggregation functions, and an initial value. It calls the aggregation function for each item in the
list with this item and previous result. In F# its signature is:

    List.fold: ('s -> 'e -> 's) -> 's -> 'e list -> 's

It seems a bit cryptic at fist, but follow with me. The tick “'” marks generic types names that can be any actual type.
The only constraint is that all 's should have the same type, and same thing for all 'e. The first argument is a 's -> 'e -> 's a function that
takes a 's and a 'e and returns a 's. We can notice that it’s the same form as our evolve function when replacing 's with State and 'e with Event.
The second argument is a 's, in our case a State. It’s the initial value. The 'e list in our case is the Event list. Finally the result is
a 's which will be a State.

Computing the new state after events when initially in given state is simply:

    let newState = List.fold evolve state events

It will work as expected with no event in the list, in which case it will return the input state.
It will also process a single event as we did by calling the evolve function once.

The nice thing it that it is also useful to rebuild the current State from Initial State and the full list of Events that happened:

    let currentState = List.fold evolve initialState events

This is exactly what we’ll need when restarting the system from a persisted Event Log.

## Or not fold

While the previous description represents a vast majority of event sourced systems, we should still consider a possibility that could be useful in some cases.

It would be totally legit to use the following evolve function:

    type State = Event list
    let initialState = []

    let evolve state (event: Event) =
        List.append state  [event]

The state would then just be the full list of all events that happened before. It would definitely contain all the information needed by the decide function:

    let decide command (state: Event list) =
        (* ... *)

This decide function can now search through the event list to find relevant data to take decision.

Once going this direction, the evolve function looks rather useless. Folding the state would look like:

    events |> List.fold evolve initialState

Which is equivalent to:

    events |> List.fold (fun state event -> List.append state [event]) []

This rebuilds an exact copy of the initial events list ! We can just pass the events list directly to the decide function.

In that setting, there is just a decide function with the following signature:

    decide: Command -> Event list -> Event list

A function that takes a command and a list of Events and returns a list of new Events.

No need for state, neither of an evolve function.

This can definitely be of use in context with few events, or when performance doesn’t come into play. 

State built by the evolve function is just a projection to compact the information contained in the events to make it easier and/or faster for the decide function to take decision. If it does not make it easier/faster, just use the raw event list. 

The full list of past Event is the the form of the State that contains maximal information. This represents the full history.


## Terminal State

For now our systems has an Initial State to start and some functions, decide and evolve, to change over time.

At first it seems enough, but this is due to one of our typical engineer’s bias… We’re very good at setting things up,
but bad at disposing them. I’ve seen many systems where loads of data remained in the system mostly because nobody knew
if it was safe to get rid of it. 

This can be seen in the industry at large. Mobiles phones have been produced in astronomical numbers before realizing something should be done to dispose them properly,
and this is still not handled by the manufacturers. Same thing for nuclear power plants that have not been especially designed to
be dismantled.

Whether you consider using event sourcing or not, it is best to think about it upfront and explicitly design for disposal. 

Some things have already a definite lifetime. For instance, once an order has been shipped and every post shipment tasks have been processed,
it should never change anymore. Same thing for a card game, when the game is over, the result can be taken into account for statistics etc, but the game will not change
anymore, the decide function would never be called.

It can have many implications. For instance, if you keep state in memory for fast response, you can now unload it to save space.
It also means that the history can be archived or deleted depending on your retention needs. 

Removing old data from your system (or at least moving it to a colder area) will keep your hot area slim and far more manageable.
This is an efficient way to lower costs and simplify updates and migrations.

Even parts that have a definite life time are sometimes hard to dispose. For instance, what happens when an order shipment was lost
and there’s nobody to reclaim it, or where all players in a game left the table to never return?
It could lead to data dangling forever. To avoid this, you have to determine a policy with domain experts.
It probably already exists in the domain, for instance, lost shipment cases are considered closed after 1 month in absence of reclamation.
Or you could establish a new one. For a card game, players have a maximum time to play. It could range from a few hours for a real time game,
to a few weeks for turn by turn. A process could be put in place to alert users, but after this delay a command is sent to the system to close it,
and it then transition to a terminal state.


The question is more tricky for systems whose behavior can span continuously over many years with no foreseeable end. But this is something that has already been solved by accountants.
The accounting records for a company can easily span decades. The current balance is impacted by all the records since the beginning, but it would be
impractical to recompute everything from the first record. Especially after a few year the relevance of records for the current state drops significantly.
This is where accountants define an accounting period. At the end of the period, a book balance is computed, reconciled with the bank.
The current book is closed and a new one is opened, starting from previous book balance. This way once a book is closed, it is never changed anymore and can
be archived for reference. If an error is found after closing the book, a compensation is recorded in the current book.

It is highly advised to put such mechanism in place for your own system, especially if you keep data over time, like saving events.
Even if storage is going cheaper over time, your system will evolve, and since data that is considered hot can change at any time, it must be migrated and evolved in a transactional
or high availability, zero downtime way. This place a high burden on operations. For streams of events, recomputing current state with such strategy will be done in a time dependent
on your business size. Without it will be dependent on both business size and time since it started (it’s actually a integral of business size over time).

Once we’ve defined a condition for disposability, we can implement it with a simple function:

    IsTerminal: 'state -> bool

This function takes a state, and returns a bool indicating if it is a terminal state. I really encourage you to make it part of your
design to avoid maintenance problems later.

## Decider

We’ve seen that State and the evolve function are just an optimization to compact information, to avoid going through the full list of Events at each call of the decide function.
And this clearly indicates that the most important part in what we’ve seen so far is the decide function.

We will call a Decider the combination of the seven elements we’ve identified so far:

* A Command type that represents all commands that can be submitted to the Decider
* An Event type that represents all events that can be produced by the Decider
* A State type that represents all possible states of the Decider (can just be the list of all events)
* An Initial State that is the state of the Decider before anything happened to it
* A decide function that takes a Command and a State and returns a list of Event
* An evolve function that takes a State and an Event an returns a new State
* An isTerminal function that takes a State and returns a boolean value


In F# this can be modeled as:
*)
type Decider<'c,'e,'s> = 
    { decide: 'c -> 's -> 'e list
      evolve: 's -> 'e -> 's
      initialState: 's
      isTerminal: 's -> bool }

(**
The Decider is a conceptual way to think about systems that change in time. An concepty interface between the Application layer
and the Domain code. It has the advantage to create extremely low friction between them.

## Run in memory

We can easily make a decider run in memory on a mutable State variable:
*)
module InMemory =
    let start (decider: Decider<'c,'e,'s>) =
        let mutable state = decider.initialState

        fun (command: 'c) ->
            let events = decider.decide command state
            state <- List.fold decider.evolve state events
            events
(**
For any decider we can no call the start method function. It will return a function which
given a command, returns the list of Events and is ready for the next command.

## Run on a database

The previous implementation is not persistent, and will loose any state once closed. We can
persist state in a database like any classic application:
*)
(*** hide ***)
module Storage =
    let loadState(id) : 's = failwith "not implemented"
    let saveState(id, state: 's) : unit = failwith "not implemented"
(*** show ***)
module WithPersistence =
    let start (decider: Decider<'c,'e,'s>) =
        fun id (command: 'c) ->
            // load state from database
            let state = Storage.loadState(id)
            // this is the decision
            let events = decider.decide command state
            // compute new state
            let newState = List.fold decider.evolve state events
            // save state in database
            Storage.saveState(id, newState)
            events
(**

This version is simple but can be dangerous if the state can be modified concurrently. If operations are
never concurrent, this code is simple and perfectly fine.

It is then easily protected using an etag. This is usually a string returned by the database that is modified on each change.
The database can check that the provided etag is matching current etag before saving the data.
*)
(*** hide ***)
type Etag = Etag
module StorageWithEtag =
    let loadState(id) : Etag * 's  = failwith "not implemented"
    let trySaveState(id, etag: Etag, state: 's) : bool = failwith "not implemented"
(*** show ***)
module PersistenceWithEtag =
    let start (decider: Decider<'c,'e,'s>) =
        fun id (command: 'c) ->
            let rec handle() =
                // load state and etag
                let etag, state = StorageWithEtag.loadState(id)
                // this is the decision
                let events = decider.decide command state
                // compute the new state
                let newState = List.fold decider.evolve state events
                // try to save, checking the state still match etag
                if StorageWithEtag.trySaveState(id, etag, newState) then
                    events
                else
                    handle()
            handle()            
(**
This version will stubornly retry until we avoid the concurrency problem.

It is also possible to keep state in memory to avoid loading state on each call. However in case of conflict,
we will reload the state as well as the current ETag
*)
(*** hide ***)
module StorageWithEtagAndRetry =
    let loadState(id) : Etag * 's  = failwith "not implemented"
    let trySaveState(id, etag: Etag, state: 's) : Etag option = failwith "not implemented"
(*** show ***)
module PersistenceWithEtagAndRetry =
    let start (decider: Decider<'c,'e,'s>) =
        // load initial state
        let mutable etagAndState = StorageWithEtagAndRetry.loadState(id)
        fun id (command: 'c) ->
            let rec handle (etag, state) =
                let (etag, state) = etagAndState
                // this is the decision
                let events = decider.decide command state
                // compute new state
                let newState = List.fold decider.evolve state events
                // try to save, checking the state still match etag
                match StorageWithEtagAndRetry.trySaveState(id, etag, state)  with
                | Some newEtag ->
                    // state has been saved
                    etagAndState <- (newEtag, newState)
                    events
                | None ->
                    // a conflic occured, reload etag and state
                    // from database and retry
                    handle (StorageWithEtagAndRetry.loadState(id))
            handle etagAndState
(**
It is possible to also store events in an Event Store, but still load/save state on each call.
In this case, events can be used to recompute current state when a refactoring change its shape.
Instead of migrating old state version to new version using a one time script, you can fold all saved
events through the evolve function and save state with this new version. This is actually the equivalent
of saving a [snapshot](#Snapshots) on each call.

## Run on an event store

It can also easily be implemented using an event store
*)
(*** hide ***)
type Version = int
module EventStore =
    let loadEvents(stream, version: Version) : 'e list = failwith "not implemented"
    let appendEvents(stream, events: 'e list) = failwith "not implemented"
(*** show ***)
module WithEventStore =
    let start (decider: Decider<'c,'e,'s>) =
        fun stream (command: 'c) ->
            // load all past events to compute current state
            let state = 
                EventStore.loadEvents(stream, 0)
                |> List.fold decider.evolve decider.initialState
            // get events from the decision
            let events = decider.decide command state
            // append events to stream
            EventStore.appendEvents(stream, events)
            events

(**
This version is reloading all the events from the begining on each command. This
is totally acceptable for short streams. Since events are usually small in size, loading
less that 100 events is very fast and folding them, almost instance (think of it as a loop of 100 iteration
that do a few basic operations). 

As for the database backed version, we can protect it against concurrent appends to the stream. Here we use
stream version which is usually provided by the Event Store. The appendEvents function now takes
 an expected version and will fail if the stream version is not matching. In case it is not matching it will
 return the new version as well as events that have been appended since.
*)
(*** hide ***)
module EventStoreWithVersion =
    let loadEvents(stream) : Version * 'e list = failwith "not implemented"
    let tryAppendEvents(stream, expectedVersion : Version, events: 'e list) : Result<Version, Version * 'e list> = failwith "not implemented"
(*** show ***)
module WithEventStoreAndVersion =
    let start (decider: Decider<'c,'e,'s>) =
        fun stream (command: 'c) ->
            let rec handle version state =
                // get events from decision
                let events = decider.decide command state
                match EventStoreWithVersion.tryAppendEvents(stream, version, events) with
                | Ok version ->
                    // save succeeded, we can return events
                    events
                | Error (newVersion, newEvents) ->
                    // it failed, but we have events that we missed
                    // compute current state
                    let newState = List.fold decider.evolve state newEvents
                    // and try again
                    handle newVersion newState 
            // load past events 
            let version, pastEvents = EventStoreWithVersion.loadEvents(stream)
            // compute current state
            let state = List.fold decider.evolve decider.initialState pastEvents
            handle version state
(**
This can obviously be extended to keep state in memory:
*)
module WithEventStoreInMemory =
    let start stream (decider: Decider<'c,'e,'s>) =
        let mutable versionAndState =
            let version, pastEvents = EventStoreWithVersion.loadEvents(stream)
            // compute current state
            let state = List.fold decider.evolve decider.initialState pastEvents
            version, state

        fun (command: 'c) ->
            let rec handle (version, state) =
                // get events from decision
                let events = decider.decide command state
                match EventStoreWithVersion.tryAppendEvents(stream, version, events) with
                | Ok newVersion ->
                    // save succeeded, we can return events
                    let newState = List.fold decider.evolve state events
                    versionAndState <-(newVersion, newState)
                    events
                | Error (newVersion, newEvents) ->
                    // it failed, but we have events that we missed
                    // compute current state
                    let newState = List.fold decider.evolve state newEvents
                    // and try again
                    handle (newVersion, newState)

            handle versionAndState

(**
## Snapshots

Once you have many events, it can become long to reload everything. It is then possible to save the state
periodically along with the version of the stream that produced this state. The snapshots can be saved in a
key value store:
*)
(*** hide ***)
module Snapshots =
    let tryLoadSnapshot(stream) : (Version * 's) option = failwith "not implemented"
    let saveSnapshot(stream, version: Version, state : 's) : unit = failwith "not implemented"

let isTimeToSnapshot(version ) : bool = failwith "not implemented"

(*** show ***)
module WithSnapshots =
    let start (decider: Decider<'c,'e,'s>) =
        // load state using snapshot if any
        let loadState stream =
            // load snapshot
            let snapVersion, snapState =
                Snapshots.tryLoadSnapshot(stream)
                    // fallback to version 0 and initialState if not found
                |> Option.defaultValue (0, decider.initialState)

            // load version and events after snapshot
            let version, events = 
                EventStoreWithVersion.loadEvents(stream, snapVersion)
            // fold events after snapshot
            let state = List.fold decider.evolve snapState events
            version, state

        fun stream (command: 'c) ->
            let rec handle (version, state) =
                // get events from the decision
                let events = decider.decide command state
                // append events to stream
                match EventStoreWithVersion.tryAppendEvents(stream, version, events) with
                | Ok newVersion ->
                    if isTimeToSnapshot version then
                        // it is time to save snapshot
                        // compute state
                        let newState = List.fold decider.evolve state events
                        // save it
                        Snapshots.saveSnapshot(stream, newVersion, newState)
                    events
                | Error(newVersion, newEvents) ->
                    // there was a concurrent write
                    // catchup missing events and retry
                    let newState = List.fold decider.evolve state newEvents
                    handle (newVersion, newState)


            // load all past events to compute current state
            // using snapshot if any
            let version, state = loadState stream
            handle (version, state)

(**
Here again we have the problem of snapshot invalidation. The easiest way to deal with it
is to store all snapshot for a same version of the code in a same collection, container or database, and 
change it when snapshot are not valid anymore. This happens when the structure of the state change after a 
refactoring. The saved snapshots won't have the expected shape which could cause some errors. 

You can just change the collection/container/database name when this happen. The collection will be empty on start
and snapshot will be recomputed. You can also recompute snapshots in advance before deploying a new version.

Once deployed and checked that old version is not needed anymore, you can just delete the old collection.

Ideally you would compute the collection name has a hash of the evolve function, this way, snapshots will be automatically
discarded when the evolve function changes (which always happens when the state structure changes)
*)
(*** hide ***)
module SnapshotsWithContainer =
    let tryLoadSnapshot(stream, container) : (Version * 's) option = failwith "not implemented"
    let saveSnapshot(stream, container, version: Version, state : 's) : unit = failwith "not implemented"
let getContainerFromDecideHash(_) = failwith "not implemented"
(*** show ***)

module WithSnapshotsInContainers =
    let start (decider: Decider<'c,'e,'s>) =
        let container = getContainerFromDecideHash(decider)
        // load state using snapshot if any
        let loadState stream =
            // load snapshot.. it will not be found id container has
            // changed since last run
            let snapVersion, snapState =
                SnapshotsWithContainer.tryLoadSnapshot(stream, container)
                    // fallback to version 0 and initialState if not found
                |> Option.defaultValue (0, decider.initialState)

            // load version and events after snapshot
            let version, events = 
                EventStoreWithVersion.loadEvents(stream, snapVersion)
            // fold events after snapshot
            let state = List.fold decider.evolve snapState events
            version, state

        fun stream (command: 'c) ->
            let rec handle (version, state) =
                // get events from the decision
                let events = decider.decide command state
                // append events to stream
                match EventStoreWithVersion.tryAppendEvents(stream, version, events) with
                | Ok newVersion ->
                    if isTimeToSnapshot version then
                        // it is time to save snapshot
                        // compute state
                        let newState = List.fold decider.evolve state events
                        // save it
                        SnapshotsWithContainer.saveSnapshot(stream, container, newVersion, newState)
                    events
                | Error(newVersion, newEvents) ->
                    // there was a concurrent write
                    // catchup missing events and retry
                    let newState = List.fold decider.evolve state newEvents
                    handle (newVersion, newState)


            // load all past events to compute current state
            // using snapshot if any
            let version, state = loadState stream
            handle (version, state)

(**
## Conclusion

You may have noticed that all the infrastructure code we wrote to run the Decider is totally agnostic
of the actual Domain Code. It could be running a simple game or a complex business system, it will still
stay the same.

The other interesting point is that decider can be run in many ways. Purely in memory, storing state in
a database or an event store. No change on the domain code was required. This indicates a high level
of independence on infrastructure.

You can write the Domain code as Deciders, and chose afterward which kind of persistence you want to use, if any.

The last interesting point, is that Deciders can be composed. But that's another story.

*)