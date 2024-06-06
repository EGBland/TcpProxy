// See https://aka.ms/new-console-template for more information

using System.Net;
using System.Net.Sockets;

var endpoint = new IPEndPoint(IPAddress.Any, 8001);
using var service = new TcpListener(endpoint);
service.Start();
var buf = new byte[4096];
Console.WriteLine($"Listening on {endpoint}");
while (true)
{
    var peer = service.AcceptTcpClient();
    var netStream = peer.GetStream();
    while (true)
    {
        var n = netStream.Read(buf);
        if (n == 0)
            continue;
        var str = System.Text.Encoding.UTF8.GetString(buf, 0, n);
        Console.Write(str);
        netStream.Write(buf, 0, n);
    }
}