(**
_This post is part of the [F# Advent Calendar in English 2015](https://sergeytihon.wordpress.com/2015/10/25/f-advent-calendar-in-english-2015/) project._
_Check out all the other great posts there! And special thanks to Sergey Tihon for organizing this._

Hi something fun and not too technical for end the year !

As everyone knows, the [favorite instrument of Santa Claus is Ukulele](https://www.google.fr/search?q=santa+claus+ukulele&biw=1024&bih=677&tbm=isch&source=lnms&sa=X&ved=0ahUKEwiHw5H8p-HJAhVE0xQKHZTdDuEQ_AUIBigB) !
So let's play some music, and especialy some Ukulele !

First thing first, let's create functions for notes. We start with C at octave 0, 
and have a progression by half tones.

So C is 0, D is 2, E is 4.

Since there is only a half tone between E and F, F is 5.

F is 7, A is 9, B is 11, and we reach next octave at 12, which is C 1 :
*)
open System

let C n = 12 * n
let D n = C n + 2
let E n = C n + 4
let F n = C n + 5
let G n = C n + 7
let A n = C n + 9
let B n = C n + 11 

(** For sharps and flat, lets define two functions that had and remove a half tone *)
let sharp n = n + 1
let flat n = n - 1

(** We can now create names for each note : *)
let Cd = C >> sharp
let Db = D >> flat
let Dd = D >> sharp
let Eb = E >> flat
let Fd = F >> sharp
let Gb = G >> flat
let Gd = G >> sharp
let Ab = A >> flat
let Ad = A >> sharp
let Bb = B >> flat

(** There is no E sharp or F flat because it is F and E respectively, same thing for B and C... 

Will create a structure with a custome comparison/equality that doesn't
take the octave into account by using a 12 modulus, this will prove usefull to work with chords:
*)

[<Struct>]
[<CustomComparison>]
[<CustomEquality>]
[<StructuredFormatDisplay("{Display}")>]
type Note(note : int) =
    member __.Note = note
    
    override __.GetHashCode() = note % 12 

    override __.Equals other =
        match other with
        | :? Note as other ->
            note % 12 = other.Note % 12
        | _ -> false

    static member names = 
        [| "C"
           "C#"
           "D"
           "D#"
           "E"
           "F"
           "F#"
           "G"
           "G#"
           "A"
           "A#"
           "B" |]
    member __.Display = 
        let name = Note.names.[note % 12]
        let octave = note / 12
        sprintf "%s %d" name octave

    override this.ToString() = this.Display
        
    interface IEquatable<Note> with
        member __.Equals other =
            note % 12 = other.Note % 12
    interface IComparable<Note> with
        member __.CompareTo other =
            compare (note % 12) (other.Note % 12) 
    interface IComparable with
        member __.CompareTo other =
            match other with
            | :? Note as other -> 
                compare (note % 12) (other.Note % 12)
            | _ -> 1 

    static member (+) (string: Note, fret: int) =
        Note (string.Note + fret)

let notes = List.map Note

(**
## Ukulele Strings

A Ukulele has 4 strings.

The funy thing is that the 1st one is higher than the second one, where on most string instruments
strings are in progressive order.

This is simply due to the limited size of the Ukulele, a low first string would not sound good, so
it is adjusted to the next octave.

This gives use the following:
*)
let strings = notes [G 4;C 4;E 4; A 4]

(**
## Chords 

Instead of hard-encoding ukulele chords, we will compute them !

So a bit of theory about chords.

Chords are defined by their root note and the chord quality (major, minor).

The chords start on the root note, and the chord quality indicates the distance to other notes
to include in the chord.

On string instrument, the order and the height of the actual notes are not really important for
the chord to be ok. So we can use a note at any octave.

Now, let's define the chord qualities.

First, Major, uses the root note, 3rd and 5th,
for instance for C, it will be C, E, G, which gives intervals of 0, 4 and 7 half tones from root.
*)

let quality = notes >> Set.ofList

let M n = quality [n ; n + 4; n+7] 
(** Then, Minor, uses the root note, the lower 3rd and 5th.
For C it will be C, E flat, G, so intervals of 0, 3 and 7 half tones for root.  *)

let m n = quality [n; n + 3; n+7] 

(** The 7th adds a 4th note on the Major: *)
let M7 n = quality [n; n + 4; n+7; n+11 ]

(** 
## Frets
As on a gitare, a ukulele has frets, places where you press the string with your finger
to change the tone of a string.

0 usually represent when you don't press a string at all, and pinching the string will play
the string note.

When pressing fret 1, the note is one half tone higher, fret 2, two half tone (or one tone) higher.

So pressing the second fret on the C 4 string give a D 4.

Our first function will try pressing on frets to find frets for notes that belong to the chord
*)

let findFrets chord (string: Note) =
    [0..10]
    |> List.filter (fun fret -> 
        Set.contains (string + fret) chord)
    |> List.map (fun fret -> fret, string + fret)

(** The result is list of pair, (fret, note) that can be used on the strnig

The second function will explore the combinaison of frets/note and keep only those
that contains all notes of the chords.

Ex: for a C Major chord, we need at least a C, a E and a G.

using frets 0 on string G, 0 on string C, 3 on string E, and 3 on string A, we get G, C, G, C.

All notes are part of the chord, but there is no E... not enough. 0,0,0,3 is a better solution.

The function explore all possible solution by checking notes on string that belong to the chord,
and each time remove a note from the chord. At the end, there should be no missing note.

At each level sub solutions are sorted by a cost. Standard Ukulele chords try to place fingers
as close to the top as possible. So lewer frets are better.

The cost function for a chords is to sum square of frets.
If there is any solution, we keep the one with the lowest cost.
*)

let rec filterChord chord missingNotes solution stringFrets  =
    match stringFrets with
    | [] -> 
        if Set.isEmpty missingNotes then 
            Some (List.rev solution)
        else
            None
    | string :: tail -> 
        string
        |> List.filter (fun (_,note) -> 
            chord |> Set.contains note)
        |> List.choose (fun (fret,note) -> 
            filterChord chord (Set.remove note missingNotes) ((fret,note) :: solution) tail)
        |> List.sortBy(fun s -> 
            List.sumBy (fun (fret,_) -> fret*fret) s)
        |> List.tryHead
       

(**
making a cord is now simple.
    
Compute the note in the chord using quality and root.

For each string, map possible frets the belong to the chord, then filter it.
*)
let chord root quality =    
    let chord = quality (root 4)
    strings
    |> List.map (findFrets chord)
    |> filterChord chord chord []
    |> Option.get
    

(** We can now try with classic chords: *)
let CM = chord C M

(** and the result is: *)
(*** include-value: CM ***)

(** Now C minor: *)
let Cm = chord C m

(** which is exactly what you can find on a tab sheet: *)
(*** include-value: Cm ***)

chord D m
    
chord A M
chord A m

chord G m
chord E M

(** 
## Printing chords
To print chords, we will simply use pretty unicode chars, and place a small 'o'
on the fret where we should place fingers:
 *)

let print chord  =
    let fret n frt = 
        if n = frt then 
            "o" 
        else 
            "│"
    let line chord n  =
            chord 
            |> List.map (fst >> fret n)
            |> String.concat ""       
    printfn "┬┬┬┬"
    [1..4] 
    |> List.map (line chord)
    |> String.concat "\n┼┼┼┼\n" 
    |> printfn "%s"

(** Let's try it *)
(*** define-output: chordCM ***)
chord C M |> print

(** It prints *)
(*** include-output: chordCM ***)

(** Another one *)
(*** define-output: chordGM ***)
chord G M |> print

(** and we get *)
(*** include-output: chordGM ***)

(**
## Playing chords 

We can also play chords using NAudio.

You can find NAudio on nuget.org

For simplicity I will use the midi synthetizer:
*)
#r @"../packages\NAudio\lib\net35\NAudio.dll"

open NAudio.Midi
let device = new MidiOut(0)
MidiOut.DeviceInfo 0
let midi (m:MidiMessage) =  device.Send m.RawData

let startNote note volume = 
    MidiMessage.StartNote(note, volume, 2) |> midi

let stopNote note volume = 
    MidiMessage.StopNote(note, volume, 2) |> midi

let sleep n = System.Threading.Thread.Sleep(n: int)

(** 
Now we can define a function that will play a chord.

The tempo is used as a multiplicator for a the chord length.
 
Longer tempo means slower.
 
For better result we introduce an arpegio, a small delay between each note.
Don't forget to remove this time from the waiting length...

The direction indicate if the cords are strumed Up, or Down.
In the Up case we reverse the chord.
 *)
type Direction = Dn of int | Up of int

let play tempo arpegio (chord, strum)  =
    let strings, length = 
        match strum with 
        | Dn length -> chord, length
        | Up length -> List.rev chord, length 

    strings
    |> List.iter (fun (_,(n: Note)) -> 
        startNote n.Note 100 ; sleep arpegio )

    let arpegioLength = 
        List.length chord * arpegio

    sleep (length * tempo - arpegioLength)

    strings
    |> List.iter (fun (_,(n: Note)) -> 
        stopNote n.Note 100 )


(** To strum a chord, we give a list of length, and a chord, and it will apply the cord to each length: *)
let strum strm chord =
    let repeatedChord = 
        strm 
        |> List.map (fun _ -> chord)
    
    List.zip repeatedChord strm

(** Now here is Santa Clause favorite song, Get Lucky by Daft Punk.

First the chords : *)

let luckyChords = 
    [ //Like the legend of the Phoenix,
      chord B m
      // All ends with beginnings.
      chord D M
      // What keeps the planets spinning,
      chord (Fd) m
      // The force from the beginning.
      chord E M ]

(** Then strum, this is the rythm used to play the same chord,
it goes like, Dam, Dam, Dam Dala Dam Dam: *)
let luckyStrum = 
    [ Dn 4; Dn 3; Dn 2; Dn 1; Up 2; Dn 2; Up 2]

(** and the full song : *)
let getLucky =
    luckyChords
    |> List.collect (strum luckyStrum)

(*** hide ***)
#if BLOG
module Hidden =
    let play _ _ _ = ()
open Hidden
#endif
(** And now, let's play it : *)
getLucky
|> List.replicate 2
|> List.concat
|> List.iter (play 130 25)

(** And the tab notations for the song ! *)
(*** define-output: song ***)
luckyChords
|> List.iter print

(*** include-output: song ***)


(**
# Conclusion

I hope this small thing was entertaining and that it'll get you into ukulele !

For excercise you can:

* [implements more chords](https://en.wikipedia.org/wiki/Chord_(music))
* Better printing
* add more liveliness and groove by adding some jitter to the strum...
* add the lyrics for Karaoke ! 
* try with other songs !
* try the same for a [6 string gitar](https://en.wikipedia.org/wiki/Guitar) ! 

Now it's your turn to rock !

*)