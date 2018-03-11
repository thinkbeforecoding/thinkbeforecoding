On a thread started on twitter by Rinat Abdullin, the following question was raised by Marcel Popescu:

> Is there actually a requirement to have aggregate roots in a CQRS system?

and the answer is worth a blog post...

## What is an aggregate ?

The context is Domain Driven Design, and an aggregate is a pattern that has always been a bit difficult to grasp.

As defined in [Domain Driven Design Reference](http://domainlanguage.com/wp-content/uploads/2016/05/DDD_Reference_2015-03.pdf) by Eric Evans:

> **Cluster the entities and value objects into aggregates and define boundaries around each. Choose one entity to be the root of each aggregate, and allow external objects to hold references to the root only (references to internal members passed out for use within a single operation only). Define properties and invariants for the aggregate as a whole and give enforcement responsibility to the root or some designated framework mechanism.**
>
> Use the same aggregate boundaries to govern transactions and distribution.
>
> Within an aggregate boundary, apply consistency rules synchronously. Across boundaries, handle updates asynchronously.
>
> Keep an aggregate together on one server. Allow different aggregates to be distributed among nodes. 

An aggregate is about its boundary. What is inside, what is outside.

And since what is inside have to be consistent and transactional, *what is outside doesn't have to*. 

**Aside being an organizational pattern, Aggregate is a distribution/partitionability pattern.**

## Should I care ?

The Entity pattern is usually well understood by DDD beginners, but the Aggregate pattern usually takes longer to grok.

In particular, people feel that not being permitted to have transaction spanning multiple aggregates is too limiting.

But why would you want a transaction to spans multiple aggregates ?

This is often due to:
* a bad grouping in the model, data that should be together but has been split over two distinct aggregates.
* a process hiding, the change could be split in several steps, each step could be in a separate transaction. If a step fails, others would then be reverted following the Saga pattern.

But as long as a bigger transaction could do the job, is it useful ?

## Information locality

When taking decisions, you need information.

If all information needed to take the decision is local, things are quite easy: Load it process it, save it. Done.

But what is local ?

* On the same processor ? 
* On the same machine ?
* On the same local network ?
* On the internet / in the cloud ?

for most applications, almost everything can be considered local. These applications call distant web APIs as they would call a subroutine.

But at some point, it starts to break. Not scalable enough.
The internet is not considered local anymore, the system must be changed. Big distributed transaction spanning services on several machines on the local network are still ok.
And at some point it breaks.

The problem is that when the system need to do a few transactions per hour, transactions can afford to be long, and fetch far data even if it's costly.

When the number of transaction increases, less time can be attributed to each transaction. It is not possible to fetch data from sources that are too remote.

With a few transactions per second, it's still ok to get data from other machines on the local network.
With thousands of transactions per second, it's still ok to get data on the same machine. But you have to organize data in small consistent chunks if you need to fetch it from another machine.
With millions of transactions per second, it can be costly to get data managed on another thread !

What is local depends on the time you have to process the transaction.

# Relativity

Say you have a ping of 10ms between machines A and B. Using data from A and B to take a decision is ok if you need up to 10 transactions per second.
But once you need 100 transactions per second, it's better to collocate data from A and B on a shorter network, or on the same machine.

And inversely, the locality of the data will determine the maximum speed at which you'll be able to process it !

If all your data can fit on a single machine, problem is solved.

> First law of distributed computing:
>
> **Don't**

But once data can't fit you have too distribute, and you get this kind of schemas:

![Event propagation and causality](https://www.cs.rit.edu/~ark/winter2012/730/module04/fig03.png "Event propagation and causality in a distributed system")

Which are actually the same as:

![Relativity](https://i.pinimg.com/736x/c3/06/e8/c306e89fc560172b153d5e55099743af--special-relativity-received-by.jpg "Light cones defining causality in Einstein relativity")

We see that:

> **Once we reach information propagation speed, information space time cannot be considered flat anymore**

Time cannot be considered absolute anymore like in Cartesian space time. Time is only defined locally. 
This is what Einstein discovered in 1905 with the [Special Relativity](https://en.wikipedia.org/wiki/Theory_of_relativity#Special_relativity).


Any information you get from remote computer can be considered stall.

When information changes slowly it's of limited importance but when there are several changes happening during the time of a single ping, it becomes critical to take decisions appropriately.

## Deciding near the ping speed limit

Deciding near the ping speed limit is not something new.

When messages between distant cities where transmitted by horsemen, the ping was a few days long. It was difficult to take more that a few decision per week affecting distant places.

Trains made it drop to about a day or a few hours.

Telegraphs and phones made it drop to between one hour and a few minutes.

The internet made it progressively drop to a few milliseconds, raising decision frequency to several thousands per second.

Business and administrations had to organize decision taking and information locality to be as efficient as possible under long ping.

Business have been used to live with distribution for ages without computers, and distribution patterns have been implemented in most of the existing processes long before computers.

This is why this pattern is one of the most important of DDD. Those aggregates exist in Domains to avoid inefficient information passing between distant offices or people.

## Why does it often feels overkill ?

When computer arrived in the business in the 50's and 60's, the world was mostly organized around a ping of a few hours. 

Once using computers at that business speed, everything looked flat. You put all the data in a single computer, got the result in a dozen minutes witch was way shorter than previously computed by hand or electromechanical machines. Problem solved.

But then it was not competitive anymore to continue to process a few transactions per second. Arm race.

As memory size and processor speed increase, more information can be brought locally. Arm race.

And at some point, if you want competitive advantage, you need to break current memory size and processor limit on a single machine, so you have to distribute. And space time is not flat anymore, and you have to define clearly information locality to take decisions quickly enough.

This is why Aggregates.

Do you need it ?

Depends on you context...