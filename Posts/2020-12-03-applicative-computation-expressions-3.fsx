(*** hide ***)
// #r @"..\packages\System.IO.Ports\lib\netstandard2.0\System.IO.Ports.dll"
#r @"..\packages\System.IO.Ports\runtimes\win\lib\netstandard2.0\System.IO.Ports.dll"
(** 
*this post is part of the [F# Advent Calendar 2020](https://sergeytihon.com/2020/10/22/f-advent-calendar-in-english-2020/)*

In this third installment of our series about [applicative](https://thinkbeforecoding.com/post/2020/10/03/applicatives-irl)
[computation](https://thinkbeforecoding.com/post/2020/10/07/applicative-computation-expressions)
[experessions](https://thinkbeforecoding.com/post/2020/10/08/applicative-computation-expressions-2), we'll jump to practice
with Lego Mindstorm Ev3. This should demonstrate a non trivial, real life use case for applicatives.

Even if you don't have a mindstorm set at home, follow this post. We'll mix binary protocols and category theory
- lightly explained - to provide a nice and safe API.

## Mindstorm control over bluetooth

A few years back, I wrote a quick fsx script to control lego mindstorm in F#. I worked again
on it recently to port it to net5.0.

Once paired and connected, the brick - the central component of Mindstom used to drive
motors and read sensors - appears as a **serial port**, COM9 on my machine. If you're using a
linux system, it should appear as /dev/ttyS9 (or another number).

`System.IO.SerialPort` is available directly in Net framework, and as a nuget in netcore and net5.0.
Add the reference to your project, or use the #r nuget reference in a F# 5.0 script:

    #r "nuget: System.IO.Ports"

We will read/write bytes using the `SerialPort` and `ReadOnlyMemory` buffers. We'll soon
go in more details.

### Request/Response

The most interesting part is the Request/Response mechanisme used for
sensors. We write the request on the serial port by using `port.Write`, but get the
response through an notification of the `port.DataReceived` event.

The request response is asynchronous, and it would be far easier to present it in the API
as an Async construct.

When sending a request, we must include a mandatory sequence number in int.
This sequence number will the be transmited in the corresponding response for correlation.
*)
type Sequence = uint16
(**
We will use this sequence number and a `MailboxProcessor` to implement Async in a responseDispatcher.
It accepts two kinds of messages. 

`Request` contains a Sequence number and a callback to call
when the response bytes are available. 

`Forward` contains a Sequence number and corresponding bytes.
*)

open System
open System.Threading
open System.Text
open System.Buffers
open System.Buffers.Binary

type Dispatch =
    | Request of sequence: Sequence * (ReadOnlyMemory<byte> -> unit)
    | Forward of sequence: Sequence * ReadOnlyMemory<byte>

(**
The mailbox processor is using an async rec function to process messages. On a `Request` message,
it adds the callback to a `Map` under the sequence number key. On a `Forward` message,
it finds the callback corresponding to the sequence number and call it with the data:
*)

let responseDispatcher() =
    MailboxProcessor.Start
    <| fun mailbox ->
        let rec loop requests =
            async {
                let! message = mailbox.Receive()
                let newMap =
                    match message with
                    | Request(sequence, reply) ->
                        Map.add sequence reply requests
                    | Forward(sequence, response) ->
                        match Map.tryFind sequence requests with
                        | Some reply -> 
                            reply response
                            Map.remove sequence requests
                        | None -> requests
                return! loop newMap }
        loop Map.empty


(**
The callback in the `Request` message is create using `PostAndAsyncReply`. The result
is an Async that is competed when the callback is called.


With this in place we can implement the Brick:
*)

type Brick( name ) =
    // create the port
    let port = new IO.Ports.SerialPort(name,115200)

    // the dispatcher for request/response async
    let dispatcher = responseDispatcher()

    // the mutable sequence number to provide unique numbers 
    let mutable sequence = 0


    member _.Connect() =
        // open the port
        port.Open()
        let reader = new IO.BinaryReader(port.BaseStream)

        // register to recieve data notifications
        port.DataReceived |> Event.add (fun e ->
            if e.EventType = IO.Ports.SerialData.Chars then
                //the response start with the size
                let size = reader.ReadInt16()
                let response = reader.ReadBytes(int size)
                // and contains the sequence number as first 2 bytes
                let sequence = BitConverter.ToUInt16(response, 0)
                // we can then send it to the dispatcher
                dispatcher.Post(Forward(sequence, ReadOnlyMemory response))
            ) 

    // gets new sequence number
    member _.GetNextSequence() =
        Interlocked.Increment(&sequence) |> uint16

    // write to the SerialPort
    member _.AsyncWrite (data: ReadOnlyMemory<byte>) = 
        let task = port.BaseStream.WriteAsync(data)
        // check synchronously if the ValueTask is completed
        if task.IsCompleted then
            async.Return ()
        else
            // else fallback on Task
            Async.AwaitTask (task.AsTask())

    // send a request using specified sequence number
    // and await the corresponding response
    member this.AsyncRequest(sequence, data: ReadOnlyMemory<byte>) =
        async {
            do! this.AsyncWrite(data)
            return!  dispatcher.PostAndAsyncReply(fun reply -> Request(sequence, fun response -> reply.Reply(response)))
        }

    interface IDisposable with
        member __.Dispose() = port.Close()

(** 
## Commands Serialization

Now that we can easily send bytes over the serial port, we need to serialize commands.

the format is :

    0 - ushort : bytes size
    2 - ushort : sequence number
    4 - byte   : command type (direct/system, reply/noreply)
    5 - ushort : global size (size of return values)
    7 ...      : commands bytes

It is possible to send several commands in a single call.

### Command type

The command type is encoded on a single byte. bit 0 indicates
whether it's a direct command (activating motors/sensors) 
or system command (uploading/downloading files). Bit 7 indicates
whether we expect a reply. We'll not use system commands in this post.
*)


module CommandType =
    let directReply = 0x00uy
    let directNoReply = 0x80uy
    let systemReply = 0x01uy
    let systemNoReply = 0x81uy

(**
### OpCode

The type of command is indicated by a 1 or 2 bytes opcode. Here is 
an enum of the different opcodes supported by Ev3:

*)
type Opcode =
    | UIRead_GetFirmware = 0x810a
    | UIWrite_LED = 0x821b
    | UIButton_Pressed = 0x8309
    | UIDraw_Update = 0x8400
    | UIDraw_Clean = 0x8401
    | UIDraw_Pixel = 0x8402
    | UIDraw_Line = 0x8403
    | UIDraw_Circle = 0x8404
    | UIDraw_Text = 0x8405
    | UIDraw_FillRect = 0x8409
    | UIDraw_Rect = 0x840a
    | UIDraw_InverseRect = 0x8410
    | UIDraw_SelectFont = 0x8411
    | UIDraw_Topline = 0x8412
    | UIDraw_FillWindow = 0x8413
    | UIDraw_DotLine = 0x8415
    | UIDraw_FillCircle = 0x8418
    | UIDraw_BmpFile = 0x841c
    | Sound_Break = 0x9400
    | Sound_Tone = 0x9401
    | Sound_Play = 0x9402
    | Sound_Repeat = 0x9403
    | Sound_Service = 0x9404
    | InputDevice_GetTypeMode = 0x9905
    | InputDevice_GetDeviceName = 0x9915
    | InputDevice_GetModeName = 0x9916
    | InputDevice_ReadyPct = 0x991b
    | InputDevice_ReadyRaw = 0x991c
    | InputDevice_ReadySI = 0x991d
    | InputDevice_ClearAll = 0x990a
    | InputDevice_ClearChanges = 0x991a
    | InputRead = 0x9a
    | InputReadExt = 0x9e
    | InputReadSI = 0x9d
    | OutputStop = 0xa3
    | OutputPower = 0xa4
    | OutputSpeed = 0xa5
    | OutputStart = 0xa6
    | OutputPolarity = 0xa7
    | OutputReady = 0xaa
    | OutputStepPower = 0xac
    | OutputTimePower = 0xad
    | OutputStepSpeed = 0xae
    | OutputTimeSpeed = 0xaf
    | OutputStepSync = 0xb0
    | OutputTimeSync = 0xb1

(** and the list of system opcodes (not used) *)

type SystemOpcode =
    | BeginDownload = 0x92
    | ContinueDownload = 0x93
    | CloseFileHandle = 0x98
    | CreateDirectory = 0x9b
    | DeleteFile = 0x9c

(**
To help with the serialization in a Span<T> we define
some inlines helpers:
*)


module Buffer =
    let inline write value (buffer: Span<byte>) =
        buffer.[0] <- value
        buffer.Slice(1)

    let inline writeUint16 (value:uint16) (buffer: Span<byte>) =
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value)
        buffer.Slice(2)

    let inline writeUint16BE (value:uint16) (buffer: Span<byte>) =
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value)
        buffer.Slice(2)

    let inline writeUInt32 (value:uint32) (buffer:Span<byte>) =
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value)
        buffer.Slice(4)

    let inline writeString (s: string) (buffer: Span<byte>) =
        let len = Encoding.UTF8.GetBytes(s.AsSpan(), buffer)
        buffer.Slice(len)

(**
Each of these functions write a value at the beginning of the span
and slice the appropriate size to return a new span starting just after the value
that was just written.


Using this was can write the serializeOpcode function:
*)

let serializeOpcode op buffer =
    if op > enum 0xff then
        // this is a 2 bytes opcode
        // it is serialized in big endian format
        Buffer.writeUint16BE (uint16 op) buffer
    else
        // this is a 1 byte opode
        Buffer.write (byte op) buffer

(**
In order to allocate the buffer, we need to compute the total size. The following
functions returns the size for an opcode:
*)
let opcodeLength code =
    if code > enum 0xff then 2 else 1


(**
### Parameters

Each opcode is followed by a given number of parameters containing the mandatory values
for the operation.

We define a union type to represents different parameter types and their values:
*)

type Parameter =
    | Byte of uint8
    | UShort of uint16
    | UInt of uint32
    | String of string
    | GlobalIndex of uint8

(**
All types are pretty explicit except the last one. GlobalIndex is used to indicate a
byte offset in a response data. We'll use it later.

Each argument is composed of a 1 byte prefix indicating it's type/size, followed
by the value itself. Here are the different prefixes:
*)

module ParamSize =
    let byte = 0x81uy
    let short = 0x82uy
    let int = 0x83uy
    let string = 0x84uy
    let globalIndex = 0xe1uy

(**
Now we can serialize a parameter and get its length:
*)

let serializeParam p (buffer: Span<byte>) =
    match p with
    | Byte v ->
        let b = Buffer.write ParamSize.byte buffer
        Buffer.write v b
    | UShort v ->
        let b = Buffer.write ParamSize.short buffer
        Buffer.writeUint16 v b
    | UInt v ->
        let b = Buffer.write ParamSize.int buffer
        Buffer.writeUInt32 v b
    | String s ->
        let b = Buffer.write ParamSize.string buffer
        let b =  Buffer.writeString s b
        Buffer.write 0uy b
    | GlobalIndex v -> 
        let b = Buffer.write ParamSize.globalIndex buffer
        Buffer.write v b

let paramLength = function
    | Byte _ | GlobalIndex _ -> 2
    | UShort _ -> 3
    | UInt _ -> 5
    | String l -> Encoding.UTF8.GetByteCount l + 2

(**
So we can define a command as a union:
*)

type Command =
    | Direct of Opcode * Parameter list
    // we will not model SystemCommand here
    // | SystemCommand of SystemOpcode * Parameter list

(**
Computing the size of a command is fairly straightforward:
*)

let length =
    function
    | Direct(code, parameters) ->
        opcodeLength code + List.sumBy paramLength parameters 

(**
The serialization is a bit more complicated if we want to loop on parameters
using a recursive function. The reason is that F# doesn't currently support
`Span<T> -> Span<T>` function parameters. This signature is used by our serializers
that take a Span, write at the begining an returns a shorter span starting after
writen bytes. To wrokaround this problem we have to use a plain delegate:
*)

type Serializer<'t> = delegate of 't * Span<byte> -> Span<byte>

let rec serializeAll (f: 't Serializer) (ps: 't list) (b: Span<byte>) =
    match ps with
    | [] -> b
    | p :: t ->
        let b' = f.Invoke(p, b)
        serializeAll f t b'

(**
The `serializeAll` function takes a Serializer, a list of values and a buffer.
It serializes all elements in the list using the serializer and returns a span
that starts just after the bytes we just wrote.

We use it to implement `serializeCommand`:
*)

let serializeCommand command (buffer: Span<byte>) =
    match command with
    | Direct (op, p) ->
        let b = serializeOpcode op buffer
        serializeAll (Serializer serializeParam) p b


(**
### Putting it all together

We can now proceed to the `serialize` function that allocates memory from the `MemoryPool`,
writes each part using the functions we defined above, and returns a memory owner.
The memory owner has to be disposed to return the memory to the pool.

It takes a command list and serialize all the commands using serializeAll:
*)
let serialize sequence commandType globalSize commands =
    let length = 5 + List.sumBy length commands // there is a 5 bytes header
    let rental = MemoryPool.Shared.Rent(length+2) // plus the 2 bytes for size
    let mem = rental.Memory.Slice(0,length+2)
    

    let buffer = mem.Span
    let b = Buffer.writeUint16 (uint16 length) buffer // 2 bytes size
    let b = Buffer.writeUint16 sequence b   // 2 bytes sequence number
    let b = Buffer.write commandType b      // 1 byte command type
    let b = Buffer.writeUint16 globalSize b // 2 bytes globalSize of response
    let _ = serializeAll (Serializer serializeCommand) commands b // commands bytes
    { new IMemoryOwner<byte> with 
        member _.Memory = mem
        member _.Dispose() = rental.Dispose() }

(**
And we write a `send` function that takes a list of commands, serialize them and
sends the result to the brick:
*)

let send commands =
    fun (brick: Brick) ->
        async {
            // generates a sequence number
            let sequence = brick.GetNextSequence()
            // serialize the commands in a Memory buffer
            // (that will be disposed at the end of the call)
            use memory =
                commands
                |> serialize sequence CommandType.directNoReply 0us
      
            // send it to the brick
            do! brick.AsyncWrite(Memory.op_Implicit memory.Memory)
        }

(**
### Defining commands

We're now ready to define commands in a more friendly way.

For instance the `startMotor` command can be called
with a list of output ports (the ones on the top of the brick calles A, B, C and D):
*)

type OutputPort =
    | A 
    | B
    | C
    | D

(**
Ports are encoded as a Byte parameter with bit 0 set for A, 1 for B, 2 for C, and 3 for D:
*)

let port p =
    p
    |> List.fold (fun v p ->
        v ||| match p with
              | A -> 0x01uy
              | B -> 0x02uy
              | C -> 0x04uy
              | D -> 0x08uy) 0uy
    |> Byte

(**
`startMotor` is using the `OutputStart` opcode followed by two parameters: a 0 byte , and a port byte.
*)
let startMotor ports =
    Direct(Opcode.OutputStart,
            [Byte 0uy; port ports])

(**
`stopMotor` is using the `OutputStop` opcode followed by the same parameters, and an extra Break value encoded
as a 0 or 1 byte:
*)


type Brake =
    | Brake
    | NoBrake
    with
    static member toByte b =
        match b with
        | Brake -> 0x01uy
        | NoBrake -> 0x00uy
        |> Byte 

let stopMotor ports brake = 
    Direct(Opcode.OutputStop, 
            [Byte 0uy; port ports; Brake.toByte brake ])

(**
Here are a few more commands:
*)
type Power = Power of uint8
let power p =
    if p < -100 || p > 100 then
        invalidArg "p" "Power should be between -100 and 100"
    Power (uint8 p)

/// wait for the end of prvious command
let outputReady ports =
    Direct(Opcode.OutputReady,
            [ Byte 0uy; port ports;])

let turnMotorAtPower ports (Power power) =
    Direct(Opcode.OutputPower,
            [Byte 0uy; port ports; Byte power])

let turnMotorAtSpeedForTime' ports speed msRampUp msConstant msRampDown brake =
    Direct(Opcode.OutputTimeSpeed,
            [Byte 0uy
             port ports
             Byte (byte speed)
             UInt msRampUp
             UInt msConstant
             UInt msRampDown
             Brake.toByte brake])

let turnMotorAtSpeedForTime ports speed msDuration brake =
    turnMotorAtSpeedForTime' ports speed 0u msDuration 0u brake

// yes, you can also play musique :)
let playTone volume frequency duration =
    Direct(Opcode.Sound_Tone,
        [Byte volume
         UShort frequency
         UShort duration ])

(**
You can try to put a few commands in the list and `send` them to the brick. It should
work.

## Computation Expression

To make it better, we continue with a `lego` computation expression that builds a full
program taking commands and async local computations.

It is used to compose functions that takes a Brick and return an async:
*)

type Lego<'a> = Brick -> Async<'a>

(** This is a classic Computation expression (not an applicative). *)
type LegoBuilder() =
(**
The Bind method takes a `Lego<'a>` and a function that takes a `'a` as an input to return a `Lego<'b>`.
The result should be a `Lego<'b>`, so we write a lambda that takes a Brick.

We pass this brick to x, to get an `Async<'a>`, we pass the value from this async to f using
`async.Bind`.
*)
    member __.Bind(x : Lego<'a>, f : 'a -> Lego<'b> ) : Lego<'b> = 
        fun brick ->
            async.Bind(x brick, fun v -> f v brick)

(**
This overload does quite the same but directly takes a Command list and send it
*)
    member __.Bind(command : Command list, f : unit -> Lego<'b> ) : Lego<'b> = 
        fun brick ->
            async.Bind(send command brick, fun () -> f () brick)

(**
Same thing for a single command
*)
    member __.Bind(command : Command, f : unit -> Lego<'b> ) : Lego<'b> = 
        fun brick ->
            async.Bind(send [command] brick, fun () -> f () brick)

(**
We can also do it for simple `Async<'a>` (for instance `Async.Sleep(1000)`).
In this case we just bind it directly.
*)
    member __.Bind(x : Async<'a>, f : 'a -> Lego<'b> ) : Lego<'b> = 
        fun brick ->
            async.Bind(x, fun v -> f v brick)

(**
`Return` creates a `Lego<'a>` from a value. It just takes a brick that it will not use
and returns an Async with the value.
*)
    member __.Return x : Lego<'a> = fun _ -> async.Return x

(**
Other methods implement return!, for, combination sequenced calls, etc.
*)

    // needed for return!
    member __.ReturnFrom x = x 

    // needed for for .. in .. do expressions
    member __.For<'T>(values : 'T seq, body: 'T -> Lego<unit>) = 
        fun brick ->
            async.For(values, fun t -> body t brick)

    // needed to have several do! combined
    member __.Combine(x, y) : Lego<'a>=
        fun ctx ->
            async.Combine(x ctx, y ctx)
    
    member __.Delay(f: unit -> Lego<'a>) =
        fun ctx ->
            async.Delay(fun () -> f () ctx)

    // needed for if without else
    member __.Zero() = fun ctx -> async.Zero()

    // needed for use
    member __.Using(d, f) = fun ctx ->
        async.Using(d, f ctx)

(**
The `run` functions takes a Lego<unit> function, starts it asynchronously and returns a cancelation
token to stop it.
*)
let run brick (f: Lego<unit>) = 
    let cancelToken = new CancellationTokenSource()
    Async.Start(f brick, cancelToken.Token)
    cancelToken

(**
The `runSynchronously` functions takes a Lego<'a> function, and runs it synchronously
*)
let runSynchronously brick (f: Lego<'a>) = 
    Async.RunSynchronously(f brick)
(**
We choose `lego` for our Computation Expression name
*)
let lego = LegoBuilder()

(**
## Let's play 

Here is a sample that connects to COM9 and repeat 3 times a batch of 
commands to turn motor in port A in one direction for 1 second, then in
the other direction for another second.
It stops motors once done.
*)


let sample() =
    use brick = new Brick("COM9")
    brick.Connect()

    lego {
            for i in 0..3 do
               do! [ turnMotorAtSpeedForTime [A] 50 1000u NoBrake
                     outputReady [A]
                     turnMotorAtSpeedForTime [A] -50 1000u NoBrake
                     outputReady [A] ] 
            do! stopMotor [A] NoBrake }
            |> runSynchronously brick

(**
## Sensors

The Ev3 comes with 3 default sensors, Color, Touch and IR. Extra gyroscopic and ultrasound
sensors can be acquierd separatly.

Sensors are connected to input ports (1 to 4). Motor ports (A to D) can also be read to get the
rotation of the motors:
*)
type InputPort =
    | In1
    | In2
    | In3
    | In4
    | InA
    | InB
    | InC
    | InD

/// gets the binary code for an input port
let inputPort = function 
                | In1 -> 0x00uy
                | In2 -> 0x01uy 
                | In3 -> 0x02uy
                | In4 -> 0x03uy
                | InA -> 0x10uy
                | InB -> 0x11uy
                | InC -> 0x12uy 
                | InD -> 0x13uy
                >> Byte

(**
Sensors can return different values 

* Color, ambiant light, reflected light for the color sensor
* Touch can return pressed/released state or bumps
* IR can return proximity, and remote buttons state...

each of this mode correspond to a 1 byte value
*)
type Mode =
    | TouchMode of TouchMode
    | ColorMode of ColorMode
    | IRMode of IRMode
and TouchMode = Touch | Bumps
and ColorMode = Reflective | Ambient | Color | ReflectiveRaw | ReflectiveRgb | Calibration
and IRMode = Proximity | Seek | Remote | RemoteA | SAlt | Calibrate

let modeToUInt8 = function
    | TouchMode Touch -> 0uy
    | TouchMode Bumps -> 1uy   
    | ColorMode Reflective -> 0uy
    | ColorMode Ambient -> 1uy
    | ColorMode Color -> 2uy
    | ColorMode ReflectiveRaw -> 3uy
    | ColorMode ReflectiveRgb -> 4uy
    | ColorMode Calibration -> 5uy
    | IRMode Proximity -> 0uy
    | IRMode Seek -> 1uy
    | IRMode Remote -> 2uy
    | IRMode RemoteA -> 3uy
    | IRMode SAlt -> 4uy
    | IRMode Calibrate -> 5uy


(**
Different units are used depending on the sensor value:
*)
type ReadDataType =
    | SI            // a 4 bytes single (floating) value
    | Raw           // a 4 bytes integer
    | Percent       // a 1 byte percent
    | RGB           // 3 2 bytes integers

/// gets the byte length for each data type
let readDataTypeLen = function
    | ReadDataType.SI -> 4
    | ReadDataType.Raw -> 4
    | ReadDataType.Percent -> 1
    | ReadDataType.RGB -> 6

(**
## Read command
Each of the datatypes correspond to a differents read opcode:
*)
let readOpcode = function
    | ReadDataType.SI -> Opcode.InputDevice_ReadySI
    | ReadDataType.Raw -> Opcode.InputDevice_ReadyRaw
    | ReadDataType.RGB -> Opcode.InputDevice_ReadyRaw // rgb is a specifiv Raw read
    | ReadDataType.Percent -> Opcode.InputDevice_ReadyPct

(**
With this opcode we can create a read command as we did previously. The `position` parameter
is the position of the corresponding value in the reponse bytes.
*)

let readCommand (inPort, dataType, mode) position =
    Direct(readOpcode dataType ,
            [Byte 0uy
             inputPort inPort
             Byte 0uy
             Byte (modeToUInt8 mode)
             Byte 1uy
             GlobalIndex (uint8 position)])

(**
For a single sensor request, it's easy, just pass `0` for position, and read the value
at the start of the response.


The `send` function defined before was not waiting for a response. We create a `request` function
the same way, but asks for a reply and use `AsyncRequest` to get a response.
It checks the reply type (a byte just after response length).
*)

type ReplyType =
| DirectReply = 0x02
| SystemReply = 0x03
| DirectReplyError = 0x04
| SystemReplyError = 0x05

let request commands globalSize =
    fun (brick: Brick) ->
        async {
            let sequence = brick.GetNextSequence()
            use data =
                commands
                |> serialize sequence CommandType.directReply globalSize

            let! response = brick.AsyncRequest(sequence, Memory.op_Implicit data.Memory)
            let replyType = enum<ReplyType> (int response.Span.[2])
            if replyType = ReplyType.DirectReplyError || replyType = ReplyType.SystemReplyError then
                failwith "An error occured"
            return response
        }
(**

To query multiple sensors at once, we can create multiple read commands with different positions,
and send them in a single batch. It avoids multiple roundtrip through bluetooth which can
be expensive. The problem is that setting response value position can be error prone.

With the `readDataTypeLen` we can know the size of the value, and accumulate them to compute
the offset. A `readLength` function that extracts the length from the triplet will make our life easier:

*)
let readLength (_,dataType,_) = readDataTypeLen dataType

(**
We accumulate the sizes as offsets using a `mapFold`. The accumulator in mapFold takes a state
(the offset) and an input (the sensor paramaters), and outputs a value (the the command built using the offset)
and a new state (the new offset computed by adding the value length).

This way we get a list of command with the rights offsets as well as the total length passed
as globalSize.

To interpret the result, we use `mapFold` again. Now the state is a `ReadOnlyMemory<byte>`
starting at the 3rd byte (to skip the size and response status, the reason for the `Slice(3)`).
For each item, we compute its length. The item is a pair of the sensors parameter and a slice
containing the value. The state is sliced to skip the bytes from current value and start at the next one.


We combine it with a call to request to have this function that takes a brick and a list of
sensor parameters and returns a map of sensor parameters and their value as a `ReadOnlyMemory<byte>`.


*)


let readAux brick inputs =
    async {
        if List.isEmpty inputs then
            return Map.empty
        else
            let commands, globalSize =
                inputs
                |> List.mapFold (fun offset input -> 
                    readCommand input offset, (offset + readLength input)
                    ) 
                    0

            let! data = 
                request commands (uint16 globalSize) brick

            let sensorsData = data.Slice(3)
            let response =
                inputs
                |> List.mapFold (fun (data: ReadOnlyMemory<byte>) input ->
                    let length = readLength input 
                    (input, data.Slice(0, length)),
                    data.Slice(length))
                    sensorsData
                        
                            
                |> fst
                |> Map.ofList
            return response
        }

(**
## Applicative

We solved the problem of values offsets, but a few challenge remain. First the result is still in the form of a `ReadOnlyMemory<byte>`,
and we should be carefull to read it accordingly to the requested data. Then we have to
be sure we don't try to look for values in the result that we did not request.

If you've read previous posts already, you'll recognize the `Query<'t>` applicative. Here
we will create a `Sensor<'t>` type:
*)

type InputRequest = InputPort * ReadDataType * Mode

type Sensor<'t> =
    { Inputs: InputRequest Set 
      Get: Map<InputRequest, ReadOnlyMemory<byte>> -> 't }

(**
It contains a set of inputs to  request, and a function to extract a value of type `'t`
from the result of our `readAux` function.

For instance the simplest way to create a sensor from an input:
*)

let input (req: InputRequest) =
    { Inputs = Set.singleton req 
      Get = fun m -> m.[req] }

(**
It indicates that it should request the given input and gets it from the map. The result
will be a `ReadOnlyMemory<byte>`.

Creating a `read` functions that takes a `Sensor<'t>` and calls `readAux` is straightforwad:
*)
let read (sensor: Sensor<'t>)  =
    fun brick ->
        async {
            let inputs = Set.toList sensor.Inputs 
            // gets the Map response for requested inputs
            let! response = readAux brick inputs 
            // use the sensor.Get to extract value from response
            return sensor.Get response
        }

(**
Using a `Sensor<'t>` and `read`, no way to get it wrong.

Now we want to create more complex sensors by combining simple ones.

### Ret

We can create a pure sensor, a sensor that request no inputs and return a given value:
*)

let ret x =
    { Inputs = Set.empty 
      Get = fun _ -> x }

(**
It will be useful later.

### Map

We can define a `map` operation on it (yes, it's a functor). It takes a `'a -> 'b` functions
and changes a `Sensor<'a>` to a `Sensor<'b>`:
*)
let map (f: 'a -> 'b) (s: Sensor<'a>) : Sensor<'b> =
    { Inputs = s.Inputs 
      Get = fun m -> s.Get m |> f }
(**
It requests the same inputs at the given sensor, gets its value and pass it to f.

### Map2

More interesting, we can define a `map2` that takes to sensors, and pass their values to
a function to compute a single result. This result will also be a sensor that request for
inputs of both, and combine their results using f:
*)
let map2 f sx sy =
    { Inputs = sx.Inputs + sy.Inputs  // union of inputs
      Get = fun m ->
        let x = sx.Get m    // get value for sensor sx
        let y = sy.Get m    // get value for sensor sy
        f x y }             // combine the values using f

(**
We can use `map2` to `zip` sensors. It takes to sensors and create a single sensor
that contains boths values as a tuple:
*)
let zip sx sy = map2 (fun x y -> x,y) sx sy

(**

### Apply

The problem with map2, is that it works only with 2 sensors. For 3 sensors we would
have to create a map3. And a map4, map5 etc.

But in F#, every function is a function of 1 argument due to currying. A function of type
`int -> bool -> string` can be used as a `int -> (bool -> string)`, it takes a single `int`
argument and returns a function that takes a `bool` and returns a `string`.

Passing this function to `map` with a sensor of type `Sensor<int>`, we get a
`Sensor<bool -> string>`. The meaning of this signature can seem obscure at first.
It says that if you give this to the `read` function above, it will make a request
and use the response values to build a `bool -> string` result. if you pass a 
`bool` to this result, you get a `string`.

But we would like the `bool` to also be read from a sensor. So we have a `Sensor<bool -> string>`
and a `Sensor<bool>`. We can obviously use `map2` on this pair of sensors. `map2` will pass the
values of the sensors to the function passed as an argument. The first argument of this function will
be a `bool -> string`, the second a `bool`. We can pass the `bool` value to the `bool -> string` function
to get a `string`. The result will be a `Sensor<string>`.

I used specific types to make it simpler by it works for any type:
*)

let apply (sf: Sensor<'a -> 'b>) (sx: Sensor<'a>) : Sensor<'b> =
    map2 (fun f x -> f x) sf sx


(** 
### Typed basic sensors

For a given data type, the conversion should always be the same. We create 
a function for each data type. 

Each of the inputXXX functions take a port and a mode are returns a typed sensor.
*)
let typedInput dataType convert =
    fun port mode ->
        input (port, dataType, mode) |> map convert

        
let inputSi = typedInput ReadDataType.SI (fun data -> BitConverter.ToSingle(data.Span))
let inputRaw = typedInput ReadDataType.Raw (fun data -> BitConverter.ToInt32(data.Span))
let inputPct = typedInput ReadDataType.Percent (fun data -> int data.Span.[0])
let inputRgb = typedInput ReadDataType.RGB (fun data -> 
    let span = data.Span
    let r = BitConverter.ToUInt16(span)
    let g = BitConverter.ToUInt16(span.Slice(2))
    let b = BitConverter.ToUInt16(span.Slice(4))
    int r, int g, int b
    )

(**
Using this functions we define more interesting sensors
*)

// the state of the touch sensor
type ButtonState =
    | Pushed
    | Released

// the colors of the color sensor
type Color =
    | Transparent
    | Black
    | Blue
    | Green
    | Yellow
    | Red
    | White
    | Brown

module Sensors =
    module Color =
        let reflective port = inputPct port (ColorMode ColorMode.Reflective)
        let ambient port = inputPct port (ColorMode ColorMode.Ambient)

        // here we use the the SI single result
        // and map it to a Color
        let color port =
            inputSi port (ColorMode ColorMode.Color)
            |> map (function
                    | 1.f -> Black
                    | 2.f -> Blue
                    | 3.f -> Green
                    | 4.f -> Yellow
                    | 5.f -> Red
                    | 6.f -> White
                    | 7.f -> Brown
                    | _ -> Transparent)
        module Raw =
            let reflective port = inputRaw port (ColorMode ColorMode.ReflectiveRaw)
            // this one use a RGB type, the result is read as a RGB triplet
            let rgb port = inputRgb port (ColorMode ColorMode.ReflectiveRgb)


    module IR =
        let proximity port = inputPct port (IRMode Proximity)

    module Touch =
        // the SI single result is mapped to Pushed/Released
        let button port = 
            inputSi port (TouchMode Touch)
            |> map (function 1.f -> Pushed | _ -> Released)

(**
### Operators

Prior to F# 5.0 it is not possible to implement applicatives with computation expressions.
Instead we can define `<!>` for `map` and `<*>` for `apply` to get infix syntax.

`<!>` takes a function on a left and a sensor on the right. We get a sensor with the argument
applied. If it was a simple 1 argument function, we get a sensor of the result:
*)
let (<!>) = map
let (<*>) = apply


let isRed port : Sensor<bool> =
    (fun c -> c = Red) 
    <!> Sensors.Color.color port

(**
If the function take several parameters, we get a sensor of a function as a result.
We can use it with `<*>`, the operator for `apply`: 
*)
let colorAndProximityIfPushed : Sensor<(Color * int) option> =
    (fun color proximity button ->
        match button with
        | Pushed -> Some (color, proximity)
        | Released -> None)
    <!> Sensors.Color.color In1
    <*> Sensors.IR.proximity In2
    <*> Sensors.Touch.button In3

(** This is a composit sensors that gets the state from the Touch sensor in input 3,
color from Color sensor in input 1, and proximity from IR sensor on input 2. It
returns `None` when the button is released, and `Some(color, proximity)` when the button
is pushed.
*)

(**
### Sensor Computation Expression

With F# 5.0 we can replace these operators by a computation expression.

We create a builder type as usual but implement `BindReturn` as `map` (with arguments swapped),
and `MergeSources` as `zip`.

We can also provide a `Bind2Return` for the 2 parameter cases as `map2`.

The `Return` is implemented using `ret`, the pure value sensor:
*)

type SensorBuilder() =
    member _.BindReturn(x: 'a Sensor,f : 'a -> 'b) : 'b Sensor = map f x

    member _.MergeSources(x: 'a Sensor, y: 'b Sensor) : ('a * 'b) Sensor = zip x y

    member _.Bind2Return(x: 'a Sensor,y: 'b Sensor,f : 'a*'b -> 'c) : 'c Sensor = 
        map2 (fun x y -> f(x,y)) x y

    member _.Return x = ret x

let sensor = SensorBuilder()

(**
Let's rewrite `colorAndProximityIfPushed` sensor with this computation expression:
*)


let colorWhenPushed =
    sensor {
        let! color = Sensors.Color.color In1
        and! proximity = Sensors.IR.proximity In2
        and! button = Sensors.Touch.button In3

        return match button with
               | Pushed -> Some (color, proximity)
               | Released -> None
    }

(**
Notice the usage of and! to combine all the sensors together.

We put it all together in a final sample:
*)

let sample2() =
    use brick = new Brick("COM9")
    brick.Connect()

    lego {

        for i in 0 .. 100 do
            let! value = read (
                            sensor {
                                let! color = Sensors.Color.Raw.rgb In1
                                and! b = Sensors.Touch.button In2
                                and! prox = Sensors.IR.proximity In3

                                match b with
                                | Pushed -> return Some (color,prox)
                                | Released -> return None
                                    }
                                )
            printfn "%A" value

    } |> runSynchronously brick

(**
Take your time to extend it to send any command and combine any sensors in a friendly and
readable way.

The applicative computation expression for sensors provide a safe API over an error prone
low level protocol. The syntax of computation expresions is also less obscure to
newcomers than the `<!>` and `<*>` operators that can be a bit hard to explain.

Now you know what to ask Santa for Xmas ! Happy Xmas !
*)