(*** hide ***)
open System
(**
*this post is part of the [F# Advent Calendar 2021](https://sergeytihon.com/2021/10/18/f-advent-calendar-2021/)*

After years of talks and workshops about Functional Event Sourcing, I noticed that I published
only 4 posts on the topic.

The first is a [rant in 2013](/post/2013/07/28/Event-Sourcing-vs-Command-Sourcing) about [Martin Fowler's article from 2005](https://www.martinfowler.com/eaaDev/EventSourcing.html)
describing something that could be called Command Sourcing and is misleading people into thinking they are doing Event Sourcing.

The second is a [simple drawing](/post/2014/01/04/Event-Sourcing.-Draw-it) describing the flow of Functional Event Sourcing. Really useful, many people told
me something clicked once they saw this picture. But still a bit short. I made a few wording changes since then.

I then made two posts about [Monoidal](/post/2014/04/11/Monoidal-Event-Sourcing) [Event Sourcing](/post/2014/04/27/Monoidal-Event-Sourcing-Examples)
trying theoretical ideas to change this [external monoid](http://thinkbeforecoding.github.io/FsUno.Prod/Dynamical%20Systems.html) to a plain monoid.

It was accompanied by many samples on github, such as the various [FsUno](https://github.com/thinkbeforecoding/UnoCore) implementations, the [crazyeights](https://github.com/thinkbeforecoding/hackyourjob-crazyeights) workshop
as well as the source code of [Crazy Farmers](https://github.com/thinkbeforecoding/crazy)' port for [Board Game Arena](https://boardgamearena.com/gamepanel?game=crazyfarmers) that uses both
Server Side and Client Side event sourcing -- the client side being transpiled to JS using Fable, and the server side to PHP using peeble.

I'm currently working on a book about it, but writing books takes time. And digging deep into
the topic led me to discover new things that make it even longer to write.

So here it is, a beginner's introduction to Functional Event Sourcing and its main
pattern, the Decider.

## Anatomy of a Service

Let's look at a System. Any System.

Without interactions with the outside, such a system would be pretty useless.
We can represent these interactions in a generic way as inputs and outputs

    [lang=diag]
                 .────────────────────.
                 │                    │
    Inputs       │                    │     Outputs
       ─────────►│       System       │───────────►
                 │                    │
                 │                    │
                 '────────────────────'

A system that accepts inputs but produces no outputs would be a kind of black hole.

    [lang=diag]
                 .────────────────────.
                 │       System       │
    Inputs       │                    │  No Outputs
       ─────────►│     (the void)     │
                 │                    │
                 │                    │
                 '────────────────────'

this is not totally useless (cf /dev/null), but quite limited.

On the other hand, a system that produces outputs without inputs seems also very simplistic.
Without introducing time, it would necessarily be a constant:

    [lang=diag]
                 .────────────────────.
                 │                    │  Constant
                 │                    │     Outputs
                 │       System       │───────────►
                 │                    │
                 │                    │
                 '────────────────────'

So, let's introduce time; let's add a clock to our system:

    [lang=diag]
                 .────────────────────.
                 │  Clock             │
                 │                    │     Outputs
                 │       System       │───────────►
                 │                    │
                 │                    │
                 '────────────────────'

The problem with a clock is that it's (obviously) changing over time, so let's
refactor to treat clock as an input (it also becomes a trigger):

    [lang=diag]
                 .────────────────────.
                 │  Clock    System   │
                 │   │                │
                 │   ▼                │    Outputs
                 │  .──────────────.  │
                 │  │  Subsystem   │───────────────►
                 │  '──────────────'  │
                 '────────────────────'

Here, our system is a pure function of time. This can be useful; however, most of the
systems we deal with are a bit more complicated.

As we wanted interactions, we can reintroduce inputs as triggers. For instance a user action.

    [lang=diag]
                 .───────────────────.
                 │  Clock    System  │
                 │   │               │
    Actions      │   ▼               │    Outputs
                 │  .─────────────.  │
       ────────────►│  Subsystem  │───────────────►
                 │  '─────────────'  │
                 '───────────────────'

To go back on the question of time, some time is passed in the system during
code execution. But at today’s execution speed, especially outside of inputs/outputs,
code execution can be considered as instant. To be executed the code needs some trigger to be called and run.

A user action can be such a trigger, in this case, the current time can be seen as one parameter of the action.

The trigger can also be the clock (a timer, an alarm..), but still the code is called for some reason
(it's time for the Market to close, a check must be made every 5 minutes...). In this case, this is a scheduled
automatic action, but it's still an action with current time as additional data.


Most of the time, the output not only depends on the input action, but also on what happened before.

The system needs a way to keep a trace of the effects of previous actions to react accordingly.

Without such memory, the system is always reacting the same way to the same input action, regardless of
anything that happened in the past. Some systems behave like this, but are a constrained case of more
general systems that keep a trace of past actions.

This memory is called state and is accessed and modified by the system code. It can be stored
in memory (RAM) or saved to a database. Reading it can be considered a sort of input, changing it a sort of output:

    [lang=diag]
                 .───────────────────.
                 │  Clock    System  │
                 │   │               │
    Actions      │   ▼               │    Outputs
                 │  .─────────────.  │
       ────────────►│  Subsystem  │───────────────►
                 │  '─────────────'  │
                 │     ▲        │    │
                 │     │        ▼    │
                 │  .─────────────.  │
                 │  │    State    │  │
                 │  '─────────────'  │
                 '───────────────────'


This is a useful step. The subsystem can be cleared of a lot of technical concerns.
The input interfaces can be abstracted (HTTP API, message queue, UI events, command line arguments,
timers or alarms...) and parameters can be passed to the subsystem directly.
The output interaction can also be abstracted and hidden behind interfaces (Outbound HTTP calls,
messages or notification sending...). State loading and saving can also be hidden behind an interface
to protect the subsystem from implementation details.

Once carefully stripped of technical concerns, the code in the subsystem is merely composed
of the system logic; what we commonly call the business logic, or the Domain. We call the rest of the code the
Application layer.

We described here the hexagonal architecture where the application core and the Domain are loosely
coupled to their environment through port interfaces, implemented as adapters to the technical infrastructure.

## Functional Core

We can go a bit further by applying [Functional Core](/post/2018/01/25/functional-core) to our model.
The idea is to write the Subsystem as a pure function.

The input Actions and Clock will be passed as input values (the current time, if needed, in the case of the clock).
Actually we can consider current time as being a parameter of the action, and will not mention it again.

For state, we can also load its current value, and pass it to the function. The function will return
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
This is here, obviously a system that does nothing. By returning a new state,
it will be persisted for the next call, and by returning output, it will be able to product other side effects.


## Actions and Commands

The Functional Core Domain is called on external triggers we called Actions. We can split them initially into two groups
we'll call Commands and Events.

A command is an action on the system with an intention to change it. A user places an order,
cancels a subscription or books a hotel room. It can succeed or fail, but should have an effect on the system.

On the other side, something can happen outside of the system that should have an effect on it. The weather changed,
a customer checked out, a cargo left a harbor, a scheduled alarm expired. Those things
happened and there's nothing we can do to change the fact. This can trigger a change in the system. We call them External Events.

Our Functional Core domain could directly take External Events as an input, be we can find in the Domain
internal Commands associated. The WeatherChanged event leads to a PrepareForRain command. It's 4PM, the market bell rang;
that's a CloseMarket command.

In the same way, in user interface code, it is advised to not take action directly in event handler's code.
In the onButtonClicked function, it is better to call another DoAction() function that does the actual action.
The code is clearer and the action can then be called on different triggers, and just tested without having to
raise the event itself.

There is also a distinction between External Commands and Internal Commands. Current time can be
important to process a command, and will be part of input data. However, current time will not be provided by the Command
sender as that would be highly unsafe: caller could ante/postdate the Command. The External Command is instead
enriched with current time in the Application layer. Similarly for external data such as exchange rates, weather conditions or
resources protected by access rights. Caller identity should also be used with care, it can be either validated using a password,
access-key, signature or a login check.

We will now consider any Domain input action as an internal Command containing all enrichment data
and call it a Command for brevity. A Command can be implemented as a data structure with a name indicating its intent
and containing the associated data. Its name is always a verb phrase in the imperative tense.

## Decision

The next step is to look inside the Functional Core Domain and split it even further.

It's a pure function that takes a Command and current State, and returns a State and some Outputs.

The typical way to proceed in fulfilling a Command given current State is to modify State according to business rules.
The code checks what the situation is and follows the rules defined by the domain to modify the State in a consistent way.

Doing this, we're going a bit too fast. We take some shortcuts. We take decisions and apply the effects of these decisions at the same time.
These are two quite different steps, and conflating them obscures the actual decision process.

Leaving the decision process implicit proves to be problematic. Looking at data, the business is
wondering why we ended up in the current state. Is it a bug, or did a legitimate chain of actions produce this unexpected result?
Tracing back observable side effects and saved intermediate state is time consuming and
cannot always give definitive answers.

### Against logging

This often leads to bloating the code with logs. Logs are initially useful, but they don't age well. They are added
on second thought, and seldom tested. Like commands, after a few refactorings, they most often end up outdated, reporting
incorrect and/or partial traces. Having up correct logging in sync with the model requires a lot of care. The first slip in that care
erodes trust in the traces. This quickly becomes a time hog during diagnosis. Especially when unexpected cases happen,
everything needs to be double checked, in the traces and in the code that may have changed in-between.

As an afterthought, their operation is also not designed with care. Log retention is costly, and
choosing the right log level is difficult. Too verbose, and the expense of log storage can become prohibitive, forcing a short retention period.
Not verbose enough, and critical information can be missing. Cleaning logs is also difficult because not all logs are of equal importance, nor is
everything equally relevant over the same timespan. Sadly ,log systems rarely give much flexibility in the cleaning/archiving process. All this
becomes useless anyway if the log cannot be trusted.

### Events

To avoid these conflicts, we untangle decision making from the changing of the state. We'll be explicit about
what happens by materializing decision outcomes before changing state, and changing state based only
on the decision outcome data.

We can express these Commands processing outcomes as Events. Each one expresses what just happened to the system as a past tense verb,
which then leads to a new state.

A Switch Light On Command will, if valid, produce a Light Switched On Event.
A Transfer Money Command will produce a Money Transferred Event, or a Money Transfer Rejected Event due to insufficient funds.

A Decision process then takes this simple form:

    [lang=diag]
    Command      .───────────────────.
       ─────────►│                   │    Events
    State        │      Decide       │─────────►
       ─────────►│                   │
                 '───────────────────'

If we use simple data structures for Commands, State and Events, the decision can be
implemented as a function with the following signature:
*)
(*** hide ***)
type Command = Command
type Event = Event
(*** show ***)

type Decide = Command -> State -> Event list

(**
Since we decided to not modify state at this stage, the State can be immutable (read only),
as the Commands and Events should also be.

This function has no reason to produce any side effects, such as reading or writing external state, or
calling external services. We've seen how to deal with external data like current time or exchange rates already.
So it can be implemented as a **pure function**:

* Its return value should depend only on the input parameters
* It should not produce any noticeable external side effect.

The first point is really interesting for testing and reasoning about the code.
Given a current State and a Command, the resulting Event list is always identical. This is true whatever the day and time and whatever
the current state anywhere else. It eases the testing as they will never be
dependent on external concerns. It also eases debugging; if we logged the Command
data and have a way to know the State at that point in time - which we’ll do - there is only a single possible outcome. This forces us to
not depend on any external data that is not already in State or Command parameters, such as current time or external data retrieval.

The second point stems from our choice to make decisions outcomes explicit. If the function produces any side effect, that breaks our
principle of first expressing the decision made before applying any change. We’ll also see why even logging is useless inside this function.

These constraints give rise to many interesting properties.

In summary, a decision function is a way to write:

    [lang=txt]
    When asked to process this Command in a given State, here is what happens, expressed as Events.


## Evolution

We’ve decided to split taking a decision and applying the resulting changes. The goal was to make the actual decision
taken more explicit in the code base. So, in the previous part, we devised a Decide function that returns Events but does
not actually change State. Now, let’s see how to capture change in a simple and highly testable way. This will
actually be quite straight forward.

To compute the new State, we need the current State and the Event that just happened.

We could modify the current State, mutating it so it becomes the new State, but this is not how we’ll proceed.
Mutation will not automatically lead you to bad code and bugs, but we can consider it a premature optimization at this point.
If you’re not familiar with Functional Programming, immutability can seem very costly and inefficient (and this can be correct in some contexts).
But most modern languages, functional or not, are fast enough in immutable scenarios compared to querying a database or making a network call.
The result is that the overhead of immutability at the level of Domain code can often be ignored. We’ll see how using immutable data structures for State enables reasoning about the code,
and how it has interesting composition properties. So let’s proceed with immutable State, and you’ll decide your implementation strategy based
on your own stack constraints later.

Earlier, our Decision function was returning an Event list containing zero, one or more events. We will focus first on how to deal with a single Event. It will be easy to extend from there to handling multiple events.

With immutable read only State data structures and read only Events, it becomes obvious that computing new State will also be implemented as a pure function.
There is no way to change the supplied current State and Event;
they are read only. The Event and the State must contain all the data we need,
so there is no point fetching data from somewhere else. No external service calls.
In that structure, mutating data outside would be akin to using global state, which would defeat the purpose of all this.
We have all the required data at hand, and just have to compute the new State to return it.
We’ll see further that there is also no reason for things like logging or tracing in this code.

Let’s call this function evolve; its signature is:
*)
type Evolve = State -> Event -> State
(**
It takes a State and an Event and returns a State. Its meaning is: Given a current State, when Event occurs, here is the new State.

The code of the evolve function should be extremely simple; the Decision has already been taken.
It should probably not be more that setting a field, adding a element to a list, incrementing a value, or setting/resetting a flag.

The evolve function is central to Event Sourcing but will rarely be more complicated than a few lines of codes.

## Initial State

For the first Command, we find ourselves in a specific situation. We have a Command and a decide function.
But the decide function needs an input state, and we have no State yet that we can pass as a parameter.
It's the same situation with the evolve function on the first Event.

If we consider the state returned by the evolve function and this first Event, we can obviously say that it's the State
after the first Event occurs. So the input state is the State before the first Event occurs, when nothing occurred yet. We will call this the
Initial State.

The Initial State is important as it has to be explicitly defined. Sometimes we may opt to use a possible future State for it.
For a light, for instance, it could be On or Off. For an account, it could be defined with a 0 balance. It will just use specific values for the properties,
 and it is totally possible that it will be back in the same state later. The light will be Off again, and the account could return to a 0 balance in the future.

 However, the first Event is sometimes irreversible. For a card game, before the first event happens,
  the game is not yet started. After the first event, it is started. It will eventually end, but will not return to the "not started yet state" anymore.
  This situation often arises when some initial properties have to be defined; some initial actions sets the system up.
  In a card game, the dealer will place the first card on the table. This is an action that can only happen at the beginning, it's the game setup.
  It marks the actual start of the game; before that, no other action can take place.

  In this case, the initial State has no other purpose than representing that we are ready to handle the initial action.
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
This indicates that the state is always either a) InitialState (without any more information), or b) the game is in progress, with the specified card on top of the pile.

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
You can notice here that two problems arise. First, we have to be careful not to use the TopCard property
when Started is false. Should we use an exception or a similar error mechanism to prevent this from happening?
Probably, but it would be far better to not be able to write such code in the first place. Second, what is a good
value for TopCard in the initial state when Started is false? Here I chose a three of clubs, which seems a bit arbitrary;
but any value would be. There is no good value of the Card type that naturally represents a default value. In some languages,
an option might be to use null, but this is also dangerous if null is never a valid value once started.
Null Reference Exceptions are frequent errors that are better avoided from the start. If we are prepared to use null, the next option would
be to use it and get rid of Started altogether.

On the pro side, this makes state more compact, and avoids requiring a check on a property to know if the other is valid, potentially
leading to consistency problems. On the con side, the initial state is less explicit. The code doesn’t clearly express when it is valid
to have a TopCard or not.

## Stitching it together

Now we have all the parts needed for Event Sourcing. Let's put them together.
We described three types and three elements in the previous sections, with the following signatures:

    type Command
    type Event
    type State
    initialState: State
    decide: Command -> State -> Event list
    evolve: State -> Event -> State

The Command will come from the outside, but the State and Event list will be fed from the decide function output to
the evolve function input while the output State from the evolve function will be passed to the next call to the decide function.
The same output State will also be used as an input parameter for the next call to the evolve function.

    [lang=diag]
     Command  .──────────────.
    ─────────►│              │ Events
     State    │    decide    ├───────┐
    ┌────────►│              │       │
    │         '──────────────'       │
    │         .──────────────. Event │
    │  State  │              │◄──────┘
    ├───◄─────┤    evolve    │ State
    │         │              │◄──────┐
    │         '──────────────'       │
    └────────────────────────────────┘

When processing in the system begins, it is in the Initial State. For now, nothing has happened yet.

When the first command arrives, we have a Command and a State that we can pass to the decide function.
It will return the outcomes of the Command as a list of Events: the things that happened in reaction to the input Command.

If the output Event list is empty, nothing happened; there's no need to compute a new state and we are ready for the next Command.
This should not happen frequently for at least two reasons. A system that does mostly nothing is not very interesting and
can probably be implemented without Event Sourcing... But doing nothing can also make some
situations harder to diagnose. When a Command was intended to do something but resulted in no change, it can be interesting
to know the reasons. Emitting no Events will make the distinction between an infrastructure problem and a
motivated decision to do nothing harder to find. Did the system crash, or did it just decide to do nothing?

It is still possible to know by taking a copy og the Command that has been persisted for diagnostic purposes, and a State reconstructed from past Events, passing them to the decide function and examining the result, but
it will require careful code analysis or creating a new test with this specific data. This drains developers time and is better avoided.

If the decision to do nothing results from a business rule, it is advised to make it explicit, returning an Event that will
not affect State. It will clearly appear in the produced Events and diagnostics will be trivial. This validation can be done
by support teams without any access to the code.

However, if nothing happened simply because we are already in some expected state, that's a case of an idempotent Command. In such cases, it is actually better to return no Events. This avoids bloating the Event Store with useless duplicate Events.

When the Event list contains an Event, we can call the evolve function with current State and that Event. The resulting new State is then used for the next Command instead of the Initial State.

Sometimes the Event list will contain several Events. In this case we will call the evolve function for each Event, passing previous computed State along.

This could give something like:

    let newState1 = evolve currentState event1
    let newState2 = evolve newState1 event2
    let newState3 = evolve newState2 event3

We can do better, but we now have the next current State and we are ready to process the next Command.

## Fold

When we have several events, we have to call the evolve function for each.

A simple way to do that is to use a loop:
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
As you can see, F# makes mutability explicit through the mutable keyword. Without it, state cannot be
changed to a new value... It also uses this backward looking left arrow `<-` which seems to suggest that we're
going in the wrong direction. F# IDEs also tend to display mutable variables in orange to signal them as a kind
of warning...

Another way to proceed without mutation is to use a recursive function that we will call fold:
*)

let rec fold state events =
    match events with
    | [] ->     // there are no events..
        state   // nothing changed
    | event :: rest ->
        // compute state after first event
        let newState = evolve state event
        // do it again with next events
        fold newState rest

(**
When the list is empty [] we just return the input state. When the list is not empty, we deconstruct the list as event :: rest,
event is first Event, and rest is rest of the list. We can then compute the new intermediate State using evolve
and call fold again with this newState and the remaining events rest, until we have applied them all.
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
It takes a list, an aggregation function, and an initial value. It calls the aggregation function for each item in the
list with this item and previous result. In F# its signature is:

    List.fold: ('s -> 'e -> 's) -> 's -> 'e list -> 's

It seems a bit cryptic at first, but bear with me. The tick (') marks generic type names that can be any actual type.
The only constraint is that all 's should have the same type, and same thing for all 'e. The first argument is a 's -> 'e -> 's - a function that
takes a 's and an 'e and returns a 's. We can notice that it’s the same form as our evolve function when replacing 's with State and 'e with Event.
The second argument is a 's, in our case a State. That's the initial value. The 'e list in our case is the Event list. Finally, the result is
a 's, which will be a State.

Computing a new state after events when initially in a given state is simply:

    let newState = List.fold evolve state events

It will work as expected with no event in the list, in which case it will return the input state.
It will also process a single event as we did by calling the evolve function once.

The nice thing it that it is also useful for rebuilding the current State from the Initial State and the full list of Events that happened:

    let currentState = List.fold evolve initialState events

This is exactly what we’ll need when restarting the system from a persisted Event Log.

## Or not fold

While the previous description represents a vast majority of event sourced systems, we should still consider a possibility that could be useful in some cases.

It would be totally legit to use the following evolve function:

    type State = Event list
    let initialState = []

    let evolve state (event: Event) =
        List.append state [event]

The state would then just be the full list of all events that happened so far. That would definitely contain all the information needed by the decide function:

    let decide command (state: Event list) =
        (* ... *)

This decide function can now work through the event list to find relevant data to take decision.

Once going this direction, the evolve function looks rather useless. Folding the state would look like:

    events |> List.fold evolve initialState

Which is equivalent to:

    events |> List.fold (fun state event -> List.append state [event]) []

This rebuilds an exact copy of the initial events list! We can just pass the events list directly to the decide function.

In that setting, there is just a decide function with the following signature:

    decide: Command -> Event list -> Event list

A function that takes a command and a list of Events and returns a list of new Events.

There's no need for state nor an evolve function.

This can definitely be of use in contexts with few events, or when performance doesn’t come into play.

State built by the evolve function is just a projection to compact the information contained in the events to make it easier and/or faster for the decide function to take decisions. If it does not make it easier/faster, just use the raw event list.

The full list of past Events is the form of the State that contains maximal information. This represents the full history.


## Terminal State

For now our system has an Initial State to start, and the decide and evolve fucntions, to handle change over time.

At first it seems enough, but this is due to one of our typical engineer’s bias… We’re very good at setting things up,
but bad at disposing them. I’ve seen many systems where loads of data remained in the system mostly because nobody knew
if it was safe to get rid of it.

This can be seen in the industry at large. Mobile phones have been produced in astronomical numbers before realizing something should be done to dispose them properly,
and this is still not handled by the manufacturers. Similarly for nuclear power plants that have not been designed considering how they can
be dismantled.

Whether you consider using event sourcing or not, it is best to consider this up front and explicitly design for disposal.

Some things naturally have a definite lifetime. For instance, once an order has been shipped and every post shipment task have been processed,
it should never change anymore. Same thing for a card game; when the game is over, the result can be taken into account for statistics etc, but the game will not change
anymore, the decide function will never be called again.

This can have many implications. For instance, if you keep state in memory for fast response, you can now unload it to save space.
It also means that the history can be archived or deleted depending on your retention needs.

Removing old data from your system (or at least moving it to a colder area) will keep your hot area slim and far more manageable.
This is an efficient way to lower costs and simplify updates and migrations.

Even parts that have a definite lifetime are sometimes hard to dispose. For instance, what happens when an order shipment was lost
and there’s nobody to reclaim it? or where all players in a game left the table to never return?
That could lead to data dangling forever. To avoid this, you have to determine a policy with domain experts.
Appropriate solutions probably already exist in the domain. For instance, lost shipment cases are considered closed after 1 month in absence of reclamation.
Or you could establish a new one. For a card game, players have a maximum time to play. That could range from a few hours for a real time game,
to a few weeks for turn by turn. A process could be put in place to alert users, but after this delay a command is sent to the system to close it,
and it then transitions to a terminal state.


The question is more tricky for systems whose behavior can span continuously over many years with no foreseeable end. But this is something that has already been solved by accountants.
Accounting records for a company can easily span decades. The current balance is impacted by all the records since the beginning, but it would be
impractical to recompute everything from the first record. Especially after a few years, the relevance of such records to the current state drops significantly.
This is where accountants define an accounting period. At the end of the period, a book balance is computed, and reconciled with the bank balances.
The current book is closed and a new one is opened, starting from previous book balance. This way, once a book is closed, it is never changed anymore and can
be archived for reference. If an error is found after closing the book, a compensation record is added in the current book.

It is highly advised to put such mechanisms in place for your own system, especially if you keep data over time, like saving events.
Even if storage is getting cheaper over time, your system will evolve, and since data that is considered hot can change at any time, it must be migrated and evolved in a transactional
or high availability, zero downtime way. This places a high burden on operations. For streams of events, the time takes to recompute the current state with such strategy will depend on your business size. Without it, it will be dependent on both business size and time since it started (it’s actually an integral of business size over time).

Once we’ve defined a condition for disposability, we can implement it with a simple function:

    IsTerminal: 'state -> bool

This function takes a state, and returns a bool indicating if it is a terminal state. I really encourage you to make it part of your
design to avoid maintenance problems later.

## Decider

We’ve seen that State and the evolve function are just an optimization to compact information, to avoid going through the full list of Events at each call of the decide function.
This clearly indicates that the most important part in what we’ve seen so far is the decide function.

We will call a Decider the combination of the seven elements we’ve identified so far:

* A Command type that represents all commands that can be submitted to the Decider
* An Event type that represents all events that can be produced by the Decider
* A State type that represents all possible states of the Decider (can just be the list of all events)
* An Initial State that is the state of the Decider before anything happens
* A decide function that takes a Command and a State and returns a list of Event
* An evolve function that takes a State and an Event and returns a new State
* An isTerminal function that takes a State and returns a boolean value


In F# this can be modeled as:
*)
type Decider<'c,'e,'s> =
    { decide: 'c -> 's -> 'e list
      evolve: 's -> 'e -> 's
      initialState: 's
      isTerminal: 's -> bool }

(**
The Decider is a conceptual way to think about systems that change in time; a concepty interface between the Application layer
and the Domain code. It ensures extremely low friction between them.

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
For any decider we can now call the start function. It will return a function which
given a command, returns the list of Events and is ready to handle the next command.

## Run on a database

The previous implementation is not persistent, and will lose any state once closed. We can
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

This version is simple, but can be dangerous if the state can be modified concurrently. If operations can
never be concurrent, this code is simple and perfectly fine.

It is then easily protected using an etag. This is usually a string returned by the database that is modified on each change.
The database can check that the provided etag matches the actual etag before applying the update.
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
This version will stubbornly retry until we avoid the concurrency problem.

It is also possible to keep state in memory to avoid loading state on each call. However in case of conflict,
we will reload the state as well as the current Etag
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
                // try to save, checking the state still matches the etag
                match StorageWithEtagAndRetry.trySaveState(id, etag, state)  with
                | Some newEtag ->
                    // state has been saved
                    etagAndState <- (newEtag, newState)
                    events
                | None ->
                    // a conflict occurred, reload etag and state
                    // from database and retry
                    handle (StorageWithEtagAndRetry.loadState(id))
            handle etagAndState
(**
It is possible to also store events in an Event Store, but still load/save state on each call.
In this case, events can be used to recompute the current State when a refactoring alters its shape.
Instead of migrating the old state version to new version using a one time script, you can fold all saved
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
This version is reloading all the events from the beginning on each command. This
is perfectly reasonable for short streams. Since events are usually small in size, loading
less that 100 events is very fast and folding them, almost instant (think of it as a loop of 100 iterations
that does a few basic operations).

As with the database-backed version, we can protect it against concurrent appends to the stream. Here we use
stream version which is usually provided by the Event Store. The appendEvents function now takes
 an expected version and will reject if the stream version is not matching. If it is not matching it will
 return the new version as well as events that have been appended since the one anticipated.
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
                | Error (actualVersion, catchupEvents) ->
                    // it failed, but we now have the events that we missed
                    // catch up the current state to the actual state in the database
                    let actualState = List.fold decider.evolve state catchupEvents
                    // and try again
                    handle actualVersion actualState
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
                    versionAndState <- (newVersion, newState)
                    events
                | Error (actualVersion, catchupEvents) ->
                    // it failed, but we have events that we missed
                    // compute current state
                    let actualState = List.fold decider.evolve state catchupEvents
                    // and try again
                    handle (actualVersion, actualState)

            handle versionAndState

(**
## Snapshots

Once you have many events, it can start taking a long time to reload everything. It is then possible to save the state
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
                | Error(actualVersion, catchupEvents) ->
                    // there was a concurrent write
                    // catch-up missing events and retry
                    let actualState = List.fold decider.evolve state catchupEvents
                    handle (actualVersion, actualState)


            // load all past events to compute current state
            // using snapshot if any
            let version, state = loadState stream
            handle (version, state)

(**
Here again we have the problem of snapshot invalidation. The easiest way to deal with it
is to store all snapshots for a same version of the code in a same collection, container or database, and
change it when snapshots are not valid anymore. This happens when the structure of the state changes after a
refactoring. The saved snapshots won't have the expected shape which could cause some errors.

You can just change the collection/container/database name when this happen. The collection will be empty on start
and snapshots will be recomputed. You may even opt to recompute snapshots in advance before deploying a new version.

Once we've deployed and verified that old version is not needed anymore, you can just delete the old collection.

Conceptually, you would compute the collection name as a hash of the evolve function. This way, snapshots will be automatically
invalidated when the evolve function changes (which always happens when the state structure changes)
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
                | Error(actualVersion, catchupEvents) ->
                    // there was a concurrent write
                    // catch-up missing events and retry
                    let actualState = List.fold decider.evolve state catchupEvents
                    handle (actualVersion, actualState)


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
a database or an event store. No changes to the domain code were required. This indicates a high level
of independence from infrastructure concerns.

You can write the Domain code as Deciders, and choose afterward which kind of persistence you want to use, if any.

The last interesting point, is that Deciders can be composed. But that's another story...


*)
