(**
Yeah it's christmas time again, and santa's elves are quite busy.

And when I say busy, I don't mean:

![Santa's elves](https://s.hswstatic.com/gif/santa-claus-stories-ga-the-tiny-elf-8b.jpg)


I mean busy like this:



![Santa's elves](https://theredphoenix.files.wordpress.com/2013/12/amazon-warehouse-assemblyline.jpg) 

So they decided to build some automation productivity tools,
and they choose Santa's favorite language to do the job:

**F#** of course !

## F# scripting

No body would seriously use a compiled language for automation tools. Requiring compilation or a CI server
for this kind of things usually kills motivation.

Of course it is possible to write bash/batch files but the syntax if fugly once you start to make more advanced
tools.

Python, JavaScript, Ruby or PowerShell are cool, but you end up as often with scripted languages with dynamic typing which you'll
come to regret when you have to maintain it on the long term.

F# is a staticaly typed language that can be easily scripted. Type inference make it feel like shorter JavaScript but 
with far higher safety !

Writing F# script is easy and fast. Test it from the command line:
    
    vim test.fsx

Then write:
*)
printfn "Merry Christmas !"
(**
press `:q` to exit

now launch it on linux with:

    fsharpi --exec test.fsx

or on windows:

    fsianycpu --exec test.fsx

Excellent.

The only problem is that typing the `fshapi --exec` this is a bit tedious.

## Bash/Batch to the rescue

We can create a bash/batch script to puth in the path that will launch the script (for linux):

    vim test
*)
(**
    fsharpi --exec test.fsx
*)
(**
    chmod +x test
or one windows

    vim test.cmd
*)
(**
    fsianycpu --exec test.fsx    

Done !

Better, but now we need to write a bash and/or a batch script for each F# script.

## fck bash/batch dispatcher FTW !

We create a fck file (don't forget to chmod +x it) that takes a command

    #!/usr/bin/env bash


    #the fck tool path
    fckpath=$(readlink -f "$0")
    #this fck tool dir
    dir=$(dirname $fckpath)
    script="$dir/fck-cmd/fck-$1.fsx"
    shell="$dir/fck-cmd/fck-$1.sh"
    cmd="$1"
    shift

    #restore packages if needed
    if [ ! -d "$dir/fck-cmd/packages" ]
    then
    pushd "$dir/fck-cmd" > /dev/null
        mono "$dir/fck-cmd/.paket/paket.bootstrapper.exe" --run restore
    popd > /dev/null
    fi

    #execute script command if it exists
    if [ -e $script ]
    then
        mono "$dir/fck-cmd/packages/FAKE/tools/FAKE.exe" "$script" -- $@

    #execute shell command if it exists
    elif [ -e $shell ]
    then
        eval $shell $@

    #show help
    else
    pushd "$dir/fck-cmd" > /dev/null
        mono "$dir/fck-cmd/packages/FAKE/tools/FAKE.exe" "$dir/fck-cmd/fck-help.fsx" -- $cmd $@
    popd > /dev/null
    fi
*)
(**
and the batch version:

    @echo off
    set encoding=utf-8

    set dir=%~dp0
    set cmd=%1
    set script="%dir%\fck-cmd\fck-%cmd%.fsx"
    set batch="%dir%\fck-cmd\fck-%cmd%.cmd"
    shift

    set "args="
    :parse
    if "%~1" neq "" (
      set args=%args% %1
      shift
      goto :parse
    )
    if defined args set args=%args:~1%

    if not exist "%dir%\fck-cmd\packages" (
    pushd "%dir%\fck-cmd\\"
    "%dir%\fck-cmd\.paket\paket.bootstrapper.exe" --run restore
    popd
    )

    if exist  "%script%" (
    "%dir%/fck-cmd/packages/fake/tools/fake.exe" "%script%" -- %args%
    ) else if exist "%batch%" (
    pushd "%dir%\fck-cmd\\"
    "%batch%" %cmd% %*
    popd
    ) else (
    "%dir%/fck-cmd/packages/fake/tools/fake.exe" "%dir%\fck-cmd\fck-help.fsx" -- %cmd% %*
    )

*)
(**
Forget the paket part for now.

The bash take a command argument, and check whether a fck-cmd/fck-\$cmd.fsx file exists.
If it does, run it !
It also works with shell scripts name fck-\$cmd.sh or batch scripts fck-\$cmd.cmd to integrate quickly with existing tools.

## Fake for faster startups

When F# scripts start to grow big, especially with things like Json or Xml type providers,
load time can start to raise above acceptable limits for a cli.

Using Fake to launch scripts takes adventage of it's compilation cache. We get the best of both world:

* scriptability for quick changes and easy deployment
* automaticly cached jit compilation for fast startup and execution

We could have written all commands in a single fsx file and pattern maching on the command name,
but once we start to have more commands, the script becomes bigger and compilation longer.
The problem is also that the pattern matching becomes a friction point in the source control.

## FckLib

At some point we have recuring code in the tools. So we can create helper scripts that will be included by 
command scripts.

For instance parsing the command line is often useful so I created a helper:

*)

#r "System.Xml.Linq"
#I "../packages/FAKE/tools"
#r "FakeLib.dll"

open Fake
open System

// culture invariant, case insensitive string comparison
let (==) x y = String.Equals(x,y, StringComparison.InvariantCultureIgnoreCase)

open System.Xml.Linq

module CommandLine =
    // get the command line, fck style...
    let getCommandLine() = 
        System.Environment.GetCommandLineArgs() 
        |> Array.toList
        |> List.skipWhile ((<>) "--")
        |> List.tail

    // check whether the command line starts with specified command
    let (|Cmd|_|) str cmdLine =
        match cmdLine with
        | s :: _ when s == str -> Some()
        | _ -> None 

(**
We use the `--` to delimit arguments reserved for the script.
Since Fake is used to launch scripts, we can also include FakeLib for all the fantastic helpers it contains.

Here is a sample fck-cmd/fck-hello.fsx script that can write hello.
*)
#load "fcklib/FckLib.fsx"
#r "packages/FAKE/tools/FakeLib.dll"

open FckLib.CommandLine
open Fake

let name =
    match getCommandLine() with
    | name :: _ -> name
    | _ -> "you"

tracefn "Hello %s" name
(**
It uses FakeLib for the `tracefn` function and FckLib for `getCommandLine`.

You can call it with (once fck is in your Path environment variable):

    fck hello Santa

## Help

A tool without help is just a nightmare, and writing help should be easy.

The last part of fck bash script lanch the fck-help.fsx script:
*)

#load "fcklib/FckLib.fsx"
open System.IO
open Printf
open FckLib.CommandLine
let root = __SOURCE_DIRECTORY__
let cmd =
    match getCommandLine() with
    | cmd :: _ -> cmd
    | _ -> "help"

let file cmd = sprintf "%s/fck-%s.txt" root cmd
 
let filename = file cmd

if File.Exists filename then
    File.ReadAllText filename
    |> printfn "%s"
else
    printfn "The command %s doesn't exist" cmd 
    printfn ""
    file "help"
    |> File.ReadAllText
    |> printfn "%s" 
(**
This script tries to find a fck-xxx.txt file and display it, or fallbacks to fck-help.txt.

For exemple the help for our fck hello command will be in fck-hello.txt:

    Usage:
    fck hello [<name>]

    Display a friendly message to <name> or to you if <name> is omitted.

Of course we can the pimp the fck-help.fsx to parse the txt help files and add codes for colors, verbosity etc.


## Deployment

Deployment is really easy. We can clone the git repository, and add it to $PATH.

Run the commands, it will automatically restore packages if missing, and lanch the script.

To upgrade to a new version, call fck update, defined in fck-update.sh :

    script=$(readlink -f "$0")
    dir=$(dirname $script)

    pushd "$dir" > /dev/null
    git pull
    mono "$dir/.paket/paket.bootstrapper.exe" --run restore
    popd > /dev/null

or batch fck-update.cmd:

    git pull
    .paket\paket.bootstrapper.exe --run restore
    
Yep, that's that easy

## Happy Christmas

Using Santa's elves tools, I hope you won't be stuck at work on xmas eve ! Enjoy !

[The full source is on github](https://github.com/thinkbeforecoding/fck)
*)

