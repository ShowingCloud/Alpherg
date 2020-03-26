namespace radarUdp

module radarUdp =
    open System.Net.Sockets
    open System.Net
    open System
    open FSharpPlus
    open ProjNet.CoordinateSystems

    type radarUdpProtocolTracks =
        {
            trackId     : uint32
            timestamp   : uint64
            distance    : float32
            orientation : float32
            pitch       : float32
            speedRadial : float32
            strength    : uint32
            latitude    : float32
            longitude   : float32
            altitude    : float32
            speedEast   : float32
            speedWest   : float32
            speedVert   : float32
            distanceX   : float32
            distanceY   : float32
            distanceZ   : float32
            relative    : uint32
            trackedNum  : uint32
            lostNum     : uint32
            reserved    : uint64
        }

    type radarUdpProtocol =
        {
            targetAddr  : uint16[]
            sourceAddr  : uint16[]
            sectorId    : uint32
            trackNum    : uint32
            tracks      : radarUdpProtocolTracks[]
        }

    type Addr =
        | TargetAddr
        | SourceAddr

    type radarUdp (port: int) =
        let udp = new UdpClient(port)
        let ip = new IPEndPoint(IPAddress.Any, port)

        member radar.Receive (callback: radarUdpProtocol -> unit) : Threading.Tasks.Task<unit> =
            let rec loopDeconstructed () = async {
                (fun x -> new System.AsyncCallback(x))
                <| (fun x ->
                    curry udp.EndReceive x (ref ip)
                    |> function
                        | x when length x = 900
                            -> (x
                            |> Seq.chunkBySize 4
                            |>> (curry >> flip) BitConverter.ToUInt32 0
                            |> radar.Parse
                            |> radar.Return
                            |> callback
                            )
                        | _ -> Console.WriteLine "Dropped one misconstrued packet."
                )
                |> (curry >> flip) udp.BeginReceive null
                |> Async.AwaitIAsyncResult
                |> ignore
                return! loopDeconstructed()
            }

            let rec loop () = async {
                (fun x -> Async.FromBeginEnd(udp.BeginReceive, x))
                <| (curry >> flip) udp.EndReceive (ref ip)
                |> Async.RunSynchronously
                |> function
                    | x when length x = 900
                        -> (x
                        |> Seq.chunkBySize 4
                        |>> (curry >> flip) BitConverter.ToUInt32 0
                        |> radar.Parse
                        // |> radar.Transform
                        |> radar.Return
                        |> callback
                        )
                        | _ -> Console.WriteLine "Dropped one misconstructed packet."
                return! loop()
            }
            loop() |> Async.StartAsTask

        member radar.Parse (msg: seq<uint32>) : radarUdpProtocol =
            {
                targetAddr      = msg |> take 3 |> radar.toAddr TargetAddr
                sourceAddr      = msg |> take 3 |> radar.toAddr SourceAddr
                sectorId        = msg |> Seq.item 3
                trackNum        = msg |> Seq.item 4
                tracks          = [|0..9|]
                    |> ((fun i -> msg |> Seq.item (5 + i * 22) <> uint32 0) |> filter)
                    |>> (fun i -> {
                        trackId     = msg |> Seq.item (5 + i * 22)
                        timestamp   = msg |> skip (5 + i * 22) |> take 2 |> radar.toInt64
                        distance    = msg |> Seq.item (8 + i * 22) |> radar.toFloat32
                        orientation = msg |> Seq.item (9 + i * 22) |> radar.toFloat32
                        pitch       = msg |> Seq.item (10 + i * 22) |> radar.toFloat32
                        speedRadial = msg |> Seq.item (11 + i * 22) |> radar.toFloat32
                        strength    = msg |> Seq.item (12 + i * 22)
                        latitude    = msg |> Seq.item (13 + i * 22) |> radar.toFloat32
                        longitude   = msg |> Seq.item (14 + i * 22) |> radar.toFloat32
                        altitude    = msg |> Seq.item (15 + i * 22) |> radar.toFloat32
                        speedEast   = msg |> Seq.item (16 + i * 22) |> radar.toFloat32
                        speedWest   = msg |> Seq.item (17 + i * 22) |> radar.toFloat32
                        speedVert   = msg |> Seq.item (18 + i * 22) |> radar.toFloat32
                        distanceX   = msg |> Seq.item (19 + i * 22) |> radar.toFloat32
                        distanceY   = msg |> Seq.item (20 + i * 22) |> radar.toFloat32
                        distanceZ   = msg |> Seq.item (21 + i * 22) |> radar.toFloat32
                        relative    = msg |> Seq.item (22 + i * 22)
                        trackedNum  = msg |> Seq.item (23 + i * 22)
                        lostNum     = msg |> Seq.item (24 + i * 22)
                        reserved    = msg |> skip (24 + i * 22) |> take 2 |> radar.toInt64
                    })
            }

        member radar.Return (msg: radarUdpProtocol) : radarUdpProtocol =
            msg

        member radar.toFloat32 (data: uint32) : float32 =
            data
            |> BitConverter.GetBytes
            |> (curry >> flip) BitConverter.ToSingle 0

        member radar.toInt64 (data: seq<uint32>) : uint64 =
            data
            |>> BitConverter.GetBytes
            |> Array.concat
            |> (curry >> flip) BitConverter.ToUInt64 0

        member radar.toAddr (addr: Addr) (data: seq<uint32>) : array<uint16> =
            data
            |>> BitConverter.GetBytes
            |> Array.concat
            |> curry BitConverter.ToUInt16
            <<| match addr with
                | TargetAddr -> [|0; 2; 4|]
                | SourceAddr -> [|6; 8; 10|]

        // member radar.Transform (msg: radarUdpProtocol) : radarUdpProtocol =
        //    let factory = new Transformations.CoordinateTransformationFactory()
        //    let trans = factory.CreateFromCoordinateSystems(GeographicCoordinateSystem.WGS84, ProjectedCoordinateSystem.WGS84_UTM)
        //    let fromPoint = { 120, -3 }
        //    let toPoint = trans.MathTransform.Transform(fromPoint)
        //    msg

    let Receive (port: int, callback: radarUdpProtocol -> unit,
                    longitude: double , latitude: double, height: double,
                    orientation: double, pitch: double) : Threading.Tasks.Task<unit> =
            radarUdp(port).Receive callback