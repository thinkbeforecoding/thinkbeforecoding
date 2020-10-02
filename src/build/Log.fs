[<AutoOpen>]
module Log
open System
open Printf


let cprintf color fmt = 
    Printf.kprintf (fun s ->
        Console.ForegroundColor <- color
        Console.WriteLine(s)
        Console.ResetColor()
    ) fmt

let tracefn fmt = cprintf ConsoleColor.Green fmt
let logfn fmt = cprintf ConsoleColor.DarkGray fmt
