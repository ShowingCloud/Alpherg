namespace radarUdp

module radarUdp =
    open System.Net.Sockets
    open System.Net
    open System

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

        member radar.Receive callback =
            let rec loopDeconstructed () = async {
                (fun x ->
                    udp.EndReceive(x, ref ip)
                    |> function
                        | x when Seq.length x = 900
                            -> (x
                                |> Seq.chunkBySize 4
                                |> Seq.map (fun x -> BitConverter.ToUInt32(x, 0))
                                |> radar.Parse
                                |> radar.Return
                                |> callback
                                )
                        | _ -> Console.WriteLine("Dropped one misconstrued packet.")
                )
                |> fun x -> new System.AsyncCallback(x)
                |> fun x -> udp.BeginReceive(x, null)
                |> Async.AwaitIAsyncResult
                |> ignore
                return! loopDeconstructed()
            }

            let rec loop () = async {
                Async.FromBeginEnd (udp.BeginReceive, fun acb -> udp.EndReceive(acb, ref ip))
                    |> Async.RunSynchronously
                    |> function
                        | x when Seq.length x = 900
                            -> (x
                                |> Seq.chunkBySize 4
                                |> Seq.map (fun x -> BitConverter.ToUInt32(x, 0))
                                |> radar.Parse
                                |> radar.Return
                                |> callback
                                )
                        | _ -> Console.WriteLine("Dropped one misconstructed packet.")
                return! loop()
            }
            loop() |> Async.StartAsTask

        member radar.Parse msg =
            {
                targetAddr      = msg |> Seq.take 3 |> radar.toAddr TargetAddr
                sourceAddr      = msg |> Seq.take 3 |> radar.toAddr SourceAddr
                sectorId        = msg |> Seq.item 3
                trackNum        = msg |> Seq.item 4
                tracks          = [|0..9|]
                    |> Array.filter (fun i -> msg |> Seq.item (5 + i * 22) <> uint32 0)
                    |> Array.map (fun i -> {
                        trackId     = msg |> Seq.item (5 + i * 22)
                        timestamp   = msg |> Seq.skip (5 + i * 22) |> Seq.take 2 |> radar.toInt64
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
                        reserved    = msg |> Seq.skip (24 + i * 22) |> Seq.take 2 |> radar.toInt64
                    })
            }

        member radar.Return msg =
            msg

        member radar.toFloat32 (data: uint32) =
            data
            |> BitConverter.GetBytes
            |> fun x -> BitConverter.ToSingle(x, 0)

        member radar.toInt64 (data: seq<uint32>) =
            data
            |> Seq.toArray
            |> Array.map BitConverter.GetBytes
            |> Array.concat
            |> fun x -> BitConverter.ToUInt64(x, 0)

        member radar.toAddr addr (data: seq<uint32>) =
            data
            |> Seq.toArray
            |> Array.map BitConverter.GetBytes
            |> Array.concat
            |> match addr with
               | TargetAddr -> (fun x -> [|
                    BitConverter.ToUInt16(x, 0)
                    BitConverter.ToUInt16(x, 2)
                    BitConverter.ToUInt16(x, 4)
                |])
               | SourceAddr -> (fun x -> [|
                    BitConverter.ToUInt16(x, 6)
                    BitConverter.ToUInt16(x, 8)
                    BitConverter.ToUInt16(x, 10)
                |])

    let Receive (port: int, callback: radarUdpProtocol -> unit) : Threading.Tasks.Task<unit> =
        radarUdp(port).Receive callback