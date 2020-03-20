namespace radarUdp

module radarUdp =
    open System.Net.Sockets
    open System.Net
    open System.Threading
    open System.Text

    type System.Net.Sockets.UdpClient with
        member udpclient.AsyncReceive(endPoint: IPEndPoint ref) =
            Async.FromBeginEnd(udpclient.BeginReceive, fun acb -> udpclient.EndReceive(acb, endPoint))

    type radarUdp(port: int) =
        let udp = new UdpClient(port)

        member radar.Receive f =
            let rec loop() = async {
                let ip = new IPEndPoint(IPAddress.Any, port)
                let! bytes = udp.AsyncReceive(ref ip)
                let _ = f (Encoding.UTF8.GetString(bytes))
                return! loop()
            }
            loop() |> Async.Start

    let Receive (f : string -> int) =
        let receiver = radarUdp(15281)
        let thread = receiver.Receive f
        Thread.Sleep(60 * 1000)
        0