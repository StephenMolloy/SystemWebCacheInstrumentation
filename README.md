# ASP.Net Object Cache & Memory Management Extensibility

Way back in the release of .Net 4.7, the ASP.Net team overhauled the mechanics of how caching and memory
limits work. This internal reshuffling led to some separation of concerns as well as some exposed
points of extension for both *Caching* and *Memory Management*. However, there was very little
description or guidance provided with these changes other than a [brief note in the "Whats New" section
of the .Net 4.7 release announcement.](https://docs.microsoft.com/en-us/dotnet/framework/whats-new/#ASP-NET47)

### :warning: This is a sample. It is provided for educational puposes and not intended for use in production. :warning:

This repo is here mainly for convenience when I need to create some instrumentation for tracking down
cache bugs. But as I realized there is very little - or none really - documentation about these
features introduced in 4.7, I figured I might as well explain a little bit about what is going on.

The goals of the 4.7 overhaul were three-fold:
- Create logical separation between the ASP.Net Cache and Memory Management.
- Allow different cache implementations to be plugged into ASP.Net. (*MemoryCache* or *Redis* for example.)
- Allow developers some extra control over memory monitoring and recycling in the fringe cases where the default algorthms were getting in the way.

From the early days of ASP.Net, the ASP.Net Cache and Memory Management were inextricably tied together.
For better or for worse, the thinking was that the Cache is the primary source of memory use in an
application that runs into memory pressure, and trimming the cache is cheaper than recycling the
entire worker process. So the two kind of go hand in hand. But they are not exactly the same thing.
Cache implementations should not be required to have sophisticated memory monitoring algorithms. There
might be other features that want to be plugged into the memory/recycle monitoring mechanisms. In some
cases applications might want to tune the memory monitoring algorithm to be more cautious if frequent
recycling is a problem - or more permissive if it is known that the app is the primary tenant on the
server and may be allowed to monopolize resources.

So the old plumbing within ASP.Net was broken into three logical parts. ASP.Net provides in-box
implementations of these three parts, pre-wired so they "just work." Conveniently, these pre-wired
in-box implementations do exactly what the mangled mess of non-separated internal code did prior to
4.7. (With some minor unintented bugs. Most were obvious and [serviced quickly in ASP.Net 4.7](https://support.microsoft.com/en-us/help/4035412).
HT @mrahl for finding and helping to debug [these](https://world.optimizely.com/blogs/Magnus-Rahl/Dates/2017/11/two-bugs-in-aspnet-that-break-cache-memory-management/)
and getting it fixed in ASP.Net 4.7.2. Also, [this](https://stackoverflow.com/a/55272599) was reported
by many and fixed in the 4.8 release of ASP.Net.)
- [Cache Store](docs/CacheStoreProviders.md)
- [Memory/Recycle Limit Monitors](docs/MemoryManagement.md)
- [Responses to monitor events](docs/MemoryManagement.md)

I've not spent a lot of time reviewing or rewriting documentation for these. The links above go to
rough drafts of blog posts that never got published. They contain explanation and some super-simple
sample code.

## This Repo

As mentioned earlier, this repo is not a product offering. It is not intended for consumption in
production environments. It's primarily meant to be a place where I can build instrumented caches
for my own personal issue debugging. It's secondary goal is to be educational for anyone who really
wanted to dig into the ugly details of ASP.Net caching and memory management. We exposed some of that
in 4.7 and never explained ourselves. This repo is part of the poor explanation that never came.

The code here is pretty much an external port of what comes in the box with ASP.Net. Comparing the
repo here with [ReferenceSource](https://referencesource.microsoft.com/#System.Web) will find that the
similarly-named files are nearly identical - with the annoying inconvenience of having to go through
reflection for several bits that are System.Web internal. (External *Caches* and *Memory Monitors*
should be able to find their own way and not require access to these internals - but <ins>the goal
of this repo is to be a faithful replacation of the in-box implementation</ins>... with whatever added
instrumentation is needed for whatever bug I am chasing.)

## How to contribute

It's my personal debugging repo. Not meant for contributions.

## Blog Posts
- [What's new in .NET Framework - ASP.Net 4.7](https://docs.microsoft.com/en-us/dotnet/framework/whats-new/#ASP-NET47)

That's it. Really. Unless you count a handful of confused posts on StackOverflow.