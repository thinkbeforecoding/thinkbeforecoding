I had several interesting feedbacks about the [Previous post](/post/2018/01/20/functional-core), and a few more
points to share about the pattern.

First, [Cyrille Martraire](https://twitter.com/cyriux)

<blockquote class="twitter-tweet" data-lang="en"><p lang="en" dir="ltr">Excellent (and brief) post on Functional Core &amp; Imperative Shell wrt Hexagonal Architecture, by my mate <a href="https://twitter.com/thinkb4coding?ref_src=twsrc%5Etfw">@thinkb4coding</a> <a href="https://t.co/K6hDr7YaEU">https://t.co/K6hDr7YaEU</a></p>&mdash; cyrille martraire (&commat;cyriux) <a href="https://twitter.com/cyriux/status/957223150172991490?ref_src=twsrc%5Etfw">27 janvier 2018</a></blockquote>

who reminded me to not forget to mention [Gary Bernhardt‏](https://twitter.com/garybernhardt)'s post eight years ago about [Functional Core/Imperative Shell](https://www.destroyallsoftware.com/screencasts/catalog/functional-core-imperative-shell), which was probably in the air when we discussed about this at BuildStuff in 2013. I didn't hear about it until recently, and I don't know if Michael Feathers had read it at that time. 

Thank you very much Cyrille for your enthusiastic comment.

Then there is this point by [Dragan Stepanović](https://twitter.com/d_stepanovic)

<blockquote class="twitter-tweet" data-lang="fr"><p lang="en" dir="ltr">and thanks for that! btw, I think that the downside of the approach is that in cases where the location from where to retrieve the data is dependent on business logic that resides in the domain, execution has to get back from the core to the shell and then back in to the core. </p>&mdash; Dragan Stepanović (&commat;d_stepanovic) <a href="https://twitter.com/d_stepanovic/status/958461117629632512?ref_src=twsrc%5Etfw">30 janvier 2018</a></blockquote>

which I already addressed partially in previous post, but I think it really needs a more in-depth answer.

## Applicability

The Functional Core approach is best suitable in parts of a system where the core part will be complex enough
to be interesting to be separated from the infrastructure that runs it.

> Apply when Domain >>> Infrastructure

This is the usual application case of Domain Driven Design.

When infrastructure code is more complex than domain, the functional core approach can still be used but with few
benefits because error categorization will have no meaning, and test will focus on infrastructure parts and not domain parts.

## Application of DDD modeling

Domain Driven Design model transactions around the notion of Aggregates. To explain it in a few word, an Aggregate is what is inside the Aggregate boundary. Things inside this boundary should be changed consistently and atomically, while what is outside don't have to.

All changes of the systems are done on aggregates.

Aggregates define a notion of locality. When a change needs to be applied to an Aggregate, there is no need for data outside of the command itself and the aggregate state.

So with a good Domain Driven Design modeling there is usually no case for data location dependent on other data.

When this is still needed, making several steps is the usual way to go since finding the right data to fetch is
then part of Domain logic.

## Application with CQRS

CQRS is a pattern often used in combination with Domain Driven Design.

It segregates Commands, that change the state of the system, from Queries that returns information about the current state of the system.

When using CQRS with DDD, the Aggregate pattern is used to process Commands, to change the state of the system.

The Functional Core pattern is especially useful here to isolate the Domain Logic from the infrastructure concerns
needed to run this Domain code.

This is where Functional Core has the more value, because this is where most of the Domain complexity is located. 

It can also be used on the Query side to isolate projection logic from infrastructure but this part will anyway
involve no decision taking, so it is already quite natural to use pure functions for transformations in this case.

### Application with Event Sourcing

I've blogged already a lot about Event Sourcing, and especially about [Functional Event Sourcing](/post/2014/01/04/Event-Sourcing.-Draw-it).

Functional Event Sourcing is a direct application of the Functional Core pattern.

The Functional Core is written around two pure functions:

* A decision function that takes a Command and current state, and returns Events.
* An evolution function that takes current State and an Event and returns the new State.

This functions can be combined to make a function that can be fed with past Events and a Command, 
and returns new Events that will then be stored by infrastructure code for persistence and to produce side effects.

This leads to a clean separation of Domain Code and infrastructure code as you can see in [FsUno](https://github.com/thinkbeforecoding/UnoCore), where all Domain code is at the begin of the project. Since in F#
a function can only reference things that have been previously defined, there is now way for Domain code to take
dependencies on infrastructure code. And tests induce no fake/mocks. Only passing input data and checking that the result - returned events - are the one expected.

<script async src="https://platform.twitter.com/widgets.js" charset="utf-8"></script>