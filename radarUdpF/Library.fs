namespace radarUdp

module radarUdp =
    open System.Net.Sockets
    open System.Net
    open System
    open FSharpPlus
    open GeoAPI
    open ProjNet
    open NetTopologySuite

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
            speedNorth  : float32
            speedVert   : float32
            distanceX   : float32
            distanceY   : float32
            distanceZ   : float32
            relative    : uint32
            trackedNum  : uint32
            lostNum     : uint32
            reserved    : uint64
            mutable calculatedLongitude : float32 option
            mutable calculatedLatitude  : float32 option
            mutable calculatedHeight    : float32 option
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
        let ip = IPEndPoint(IPAddress.Any, port)

        member __.Receive (callback: radarUdpProtocol -> unit) : Threading.Tasks.Task<unit> =
            let rec loopDeconstructed () = async {
                (fun x ->
                curry udp.EndReceive x (ref ip)
                |> function
                | msg when length msg = 900
                    -> msg
                    |> Seq.chunkBySize 4
                    |>> (curry >> flip) BitConverter.ToUInt32 0
                    |> __.Parse
                    |> __.Return
                    |> callback
                | _ -> Console.WriteLine "Dropped one misconstrued packet."
                )
                |> fun x -> AsyncCallback x
                |> (curry >> flip) udp.BeginReceive null
                |> Async.AwaitIAsyncResult
                |> ignore
                return! loopDeconstructed()
            }

            let rec loop () = async {
                (curry >> flip) udp.EndReceive (ref ip)
                |> fun x -> Async.FromBeginEnd(udp.BeginReceive, x)
                |> Async.RunSynchronously
                |> function
                | msg when length msg = 900
                    -> msg
                    |> Seq.chunkBySize 4
                    |>> (curry >> flip) BitConverter.ToUInt32 0
                    |> __.Parse
                    |> __.Transform
                    |> __.Return
                    |> callback
                | _ -> Console.WriteLine "Dropped one misconstructed packet."
                return! loop()
            }
            loop() |> Async.RunSynchronously

        member __.Parse (msg: seq<uint32>) : radarUdpProtocol =
            {
                targetAddr      = msg |> take 3 |> __.toAddr TargetAddr
                sourceAddr      = msg |> take 3 |> __.toAddr SourceAddr
                sectorId        = msg |> nth 3
                trackNum        = msg |> nth 4
                tracks          = [|0..9|]
                    |> ((fun i -> msg |> nth (5 + i * 22) <> uint32 0) |> filter)
                    |>> (fun i -> {
                        trackId     = msg |> nth (5 + i * 22)
                        timestamp   = msg |> skip (5 + i * 22) |> take 2 |> __.toInt64
                        distance    = msg |> nth (8 + i * 22) |> __.toFloat32
                        orientation = msg |> nth (9 + i * 22) |> __.toFloat32
                        pitch       = msg |> nth (10 + i * 22) |> __.toFloat32
                        speedRadial = msg |> nth (11 + i * 22) |> __.toFloat32
                        strength    = msg |> nth (12 + i * 22)
                        latitude    = msg |> nth (13 + i * 22) |> __.toFloat32
                        longitude   = msg |> nth (14 + i * 22) |> __.toFloat32
                        altitude    = msg |> nth (15 + i * 22) |> __.toFloat32
                        speedEast   = msg |> nth (16 + i * 22) |> __.toFloat32
                        speedNorth  = msg |> nth (17 + i * 22) |> __.toFloat32
                        speedVert   = msg |> nth (18 + i * 22) |> __.toFloat32
                        distanceX   = msg |> nth (19 + i * 22) |> __.toFloat32
                        distanceY   = msg |> nth (20 + i * 22) |> __.toFloat32
                        distanceZ   = msg |> nth (21 + i * 22) |> __.toFloat32
                        relative    = msg |> nth (22 + i * 22)
                        trackedNum  = msg |> nth (23 + i * 22)
                        lostNum     = msg |> nth (24 + i * 22)
                        reserved    = msg |> skip (24 + i * 22) |> take 2 |> __.toInt64
                        calculatedLongitude = None
                        calculatedLatitude  = None
                        calculatedHeight    = None
                    })
            }

        member __.Return (msg: radarUdpProtocol) : radarUdpProtocol =
            msg

        member __.toFloat32 (data: uint32) : float32 =
            data
            |> BitConverter.GetBytes
            |> (curry >> flip) BitConverter.ToSingle 0

        member __.toInt64 (data: seq<uint32>) : uint64 =
            data
            |>> BitConverter.GetBytes
            |> Array.concat
            |> (curry >> flip) BitConverter.ToUInt64 0

        member __.toAddr (addr: Addr) (data: seq<uint32>) : array<uint16> =
            data
            |>> BitConverter.GetBytes
            |> Array.concat
            |> curry BitConverter.ToUInt16
            <<| match addr with
                | TargetAddr -> [|0; 2; 4|]
                | SourceAddr -> [|6; 8; 10|]

        member __.Transform (msg: radarUdpProtocol) : radarUdpProtocol =
            let precisionModel =
                Geometries.PrecisionModels.Floating
                |> Geometries.PrecisionModel

            let factory_wgs84 =
                CoordinateSystems.GeographicCoordinateSystem.WGS84.AuthorityCode
                |> Convert.ToInt32
                |> curry Geometries.GeometryFactory precisionModel

            let factory_target =
                (51, true)
                |> CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM
                |> fun x -> x.AuthorityCode
                |> Convert.ToInt32
                |> curry Geometries.GeometryFactory precisionModel

            let P1 =
                (121.3837, 31.0579)
                |> Geometries.Coordinate
                |> factory_wgs84.CreatePoint

            let P2 =
                (121.5837, 31.2579)
                |> Geometries.Coordinate
                |> factory_wgs84.CreatePoint

            let transformation =
                (51, true)
                |> CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM
                |> curry (CoordinateSystems.Transformations.CoordinateTransformationFactory().CreateFromCoordinateSystems)
                    CoordinateSystems.GeographicCoordinateSystem.WGS84

            let Q1 =
                [|P1.X; P1.Y|]
                |> transformation.MathTransform.Transform
                |> fun x -> curry Geometries.Coordinate x.[0] x.[1]
                |> factory_target.CreatePoint

            let Q2 =
                [|P2.X; P2.Y|]
                |> transformation.MathTransform.Transform
                |> fun x -> curry Geometries.Coordinate x.[0] x.[1]
                |> factory_target.CreatePoint

            msg.tracks
            |>> fun x -> 
                x.calculatedLongitude <- Some (float32 Q2.X)
                x.calculatedLatitude <- Some (float32 (P2 |> P1.Distance))
                x.calculatedHeight <- Some (float32 (Q2 |> Q1.Distance))
            |> ignore

            msg

    let Receive (port: int, callback: radarUdpProtocol -> unit) : Threading.Tasks.Task<unit> =
            radarUdp(port).Receive callback

    [<EntryPoint>]
    let main args =
        Receive(15281, (printfn "%A"))
        |> Async.AwaitTask
        |> ignore

        0