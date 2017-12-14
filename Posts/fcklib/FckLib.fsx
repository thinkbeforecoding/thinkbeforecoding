module FckLib
#r "System.Xml.Linq"
#I "../../packages/FAKE/tools"
#r "FakeLib.dll"

open Fake
open System

/// culture invariant, case insensitive string comparison
let (==) x y = String.Equals(x,y, StringComparison.InvariantCultureIgnoreCase)

/// uncurry a function: f (x,y) => f x y
let uncurry2 f (x,y)= f x y

open System.Xml.Linq

module CommandLine =
    /// get the command line, fck style...
    let getCommandLine() = 
        System.Environment.GetCommandLineArgs() 
        |> Array.toList
        |> List.skipWhile ((<>) "--")
        |> List.tail

    /// check whether the command line starts with specified command
    let (|Cmd|_|) str cmdLine =
        match cmdLine with
        | s :: _ when s == str -> Some()
        | _ -> None 


module List =
    /// pair x with y when y = Some (f x) 
    let choosePair f = List.choose (fun x -> match f x with None -> None | Some y -> Some(x,y)) 


/// Railway programming for error management
module Railway =
    open Fake.TargetHelper

    /// a success or a failure 
    type Result<'a,'e> =
        /// representation of a success with a value
        | Success of 'a
        /// representation of a failure with a value
        | Failure of 'e

    /// indicates whether result is a success
    let isSuccess = function Success _ -> true | Failure _ -> false

    /// indicates whether result is a Failure
    let isFailure = function Failure _ -> true | Success _ -> true

    /// returns a success when all sources succeeded
    /// results: the results to test
    let allSucceeded results =
        match Seq.tryFind isFailure results with
        | Some f -> f
        | None -> Success ()

    /// sets the script exit code 0 for Success or 1 for Failure
    /// result: the result to report
    let exit = function
        | Success _ -> ExitCode.exitCode := 0 
        | Failure _ -> ExitCode.exitCode := 1

module Xml =
    /// creates a XNamespace instance
    /// name: the namespace name
    let xns = XNamespace.op_Implicit
    
    let xn = XName.op_Implicit
    /// gets the given attribte or an empty string
    /// name: the attribute name
    /// e: the XML element
    let attribute name (e: XElement) = 
        match e.Attribute(XName.op_Implicit name) with
        | null -> ""
        | a -> a.Value 

    /// gets child elements with given name
    /// name: the element name
    /// e: the parent XML element
    let elements name (e: XElement) = e.Elements name

    /// gets child element with given name
    /// name: the element name
    /// e: the parent XML element
    let element name (e: XElement) = e.Element name


module Help =
    /// output help for specified command
    /// command: the command to dispay
    let show command =
        let root = __SOURCE_DIRECTORY__ </> ".."
        let startFsi info =
            FSIHelper.fsiStartInfo "fck-help.fsx" root [] info
            info.Arguments <- info.Arguments + " " + command

        ExecProcess startFsi TimeSpan.MaxValue |> ignore
