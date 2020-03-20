namespace radarUdp

module radarUdp =
    open System.Net.Sockets
    open System.Net

    type System.Net.Sockets.UdpClient with
        member udpclient.AsyncReceive(endPoint: IPEndPoint ref) =
            Async.FromBeginEnd(udpclient.BeginReceive, fun acb -> udpclient.EndReceive(acb, endPoint))

    type radarUdp(port: int) =
        let udp = new UdpClient(port)

        member radar.Receive =
            let rec loop() = async {
                let ip = new IPEndPoint(IPAddress.Any, port)
                let! bytes = udp.AsyncReceive(ref ip)
                // printfn "%s" (bytes.ToString())
                printfn "Got UDP"
                return! loop()
            }
            loop() |> Async.Start

    let Main argv = 
        let receiver = radarUdp(15281)
        receiver.Receive
        0