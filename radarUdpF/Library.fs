namespace radarUdp

module radarUdp =
    open System.Net.Sockets
    open System.Net
    open System
    open FSharpPlus

    type radarUdpProtocolTracks =
        {
            /// 航迹编号
            /// 1 - 999,999
            trackId     : uint32
            /// 时间戳 (ms)
            /// Epoch时间
            timestamp   : uint64
            /// 距离 (m)
            /// 斜距
            distance    : float32
            /// 方位角 (度)
            /// 雷达坐标系
            orientation : float32
            /// 俯仰角 (度)
            /// 水平面以上
            pitch       : float32
            /// 径向速度 (m/s)
            /// 远离为负，靠近为正
            speedRadial : float32
            /// 目标强度
            /// 无量纲
            strength    : uint32
            /// 经度 (度)
            latitude    : float32
            /// 纬度 (度)
            longitude   : float32
            /// 海拔 (m)
            altitude    : float32
            /// 东向速度 (m/s)
            /// x 方向的速度，增大为正
            speedEast   : float32
            /// 北向速度 (m/s)
            /// y 方向的速度，增大为正
            speedNorth  : float32
            /// 纵向速度 (m/s)
            /// z 方向的速度，增大为正
            speedVert   : float32
            /// x (m)
            /// x = r * cos(theta) * cos(phi)
            distanceX   : float32
            /// y (m)
            /// y = r * cos(theta) * cos(phi)
            distanceY   : float32
            /// z (m)
            /// z = r * sin(theta)
            distanceZ   : float32
            /// 本次是否相关上
            /// 判断是否市外推点
            relative    : uint32
            /// 已跟踪次数
            trackedNum  : uint32
            /// 丢失次数
            /// 丢失次数为 4 航迹终止
            lostNum     : uint32
            /// 保留字段
            reserved    : uint64
            /// 根据距离、俯仰、方位换算得到的经度
            mutable calculatedLatitude  : float32 option
            /// 根据距离、俯仰、方位换算得到的纬度
            mutable calculatedLongitude : float32 option
            /// 根据距离、俯仰、方位换算得到的海拔
            mutable calculatedHeight    : float32 option
        }

    type radarUdpProtocol =
        {
            /// 目标地址
            targetAddr  : uint16[]
            /// 源地址
            sourceAddr  : uint16[]
            /// 所在方位扇区编号
            sectorId    : uint32
            /// 本包航迹数量
            trackNum    : uint32
            /// 10 个航迹信息，每条航迹只发送最新的点迹
            tracks      : radarUdpProtocolTracks[]
        }

    type Addr =
        | TargetAddr
        | SourceAddr

    type radarUdp (port: int) =
        let ip = IPEndPoint(IPAddress.Any, port)
        static let mutable udp = new UdpClient(15281)
        static let mutable cts = new Threading.CancellationTokenSource()
      
        member __.Receive (callback: radarUdpProtocol -> unit) : Threading.Tasks.Task<unit> =
            cts.Cancel()
            cts.Dispose()
            cts <- new Threading.CancellationTokenSource()

            udp.Close()
            udp.Dispose()
            udp <- new UdpClient(port)

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
                | _ -> printfn "Dropped one misconstrued packet."
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
                |> fun x -> Async.RunSynchronously(x, cancellationToken = cts.Token)
                |> function
                | msg when length msg = 900
                    -> msg
                    |> Seq.chunkBySize 4
                    |>> (curry >> flip) BitConverter.ToUInt32 0
                    |> __.Parse
                    |> __.Transform
                    |> __.Return
                    |> callback
                | _ -> printfn "Dropped one misconstrued packet."
                return! loop()
            }
            loop()
            |> fun x -> Async.RunSynchronously(x, cancellationToken = cts.Token)

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
                        latitude    = msg |> nth (13 + i * 22) |> float32 |> Operators.(/) 1000000.0f
                        longitude   = msg |> nth (14 + i * 22) |> float32 |> Operators.(/) 1000000.0f
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
                        calculatedLatitude  = None
                        calculatedLongitude = None
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
            printfn "%A" GeoToolkit

            msg.tracks
            //|>> fun x -> 
            //    x.calculatedLatitude <- Some (float32 (P2 |> P1.Distance))
            //    x.calculatedLongitude <- Some (float32 Q2.X)
            //    x.calculatedHeight <- Some (float32 (Q2 |> Q1.Distance))
            |> ignore

            msg

    let StartReceive (port: int, callback: radarUdpProtocol -> unit) : Threading.Tasks.Task<unit> =
        radarUdp(port).Receive callback

    [<EntryPoint>]
    let main (args: string[]) : int =
        curry StartReceive 15281 (printfn "%A")
        |> Async.AwaitTask
        |> ignore

        0