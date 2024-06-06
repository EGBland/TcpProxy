using System.Net;
using System.Net.Sockets;
using Serilog;

namespace TcpProxy;

public class ProxySession(TcpClient peer, IPEndPoint proxyEndpoint)
{
    private const int BufferSize = 4096;
    
    private TcpClient Peer { get; } = peer;
    private IPEndPoint ProxyEndpoint { get; } = proxyEndpoint;

    public void Run()
    {
        Log.Information("Peer received: {RemoteAddr} -> {LocalAddr}", peer.Client.RemoteEndPoint, peer.Client.LocalEndPoint);

        using var proxy = new TcpClient();
        try
        {
            proxy.Connect(ProxyEndpoint);
            Log.Information("Proxy connected: {LocalAddr} -> {RemoteAddr}", proxy.Client.LocalEndPoint, proxy.Client.RemoteEndPoint);
        }
        catch (SocketException ex)
        {
            Log.Error(ex, "Could not connect to proxy");
            return;
        }
        
        using var peerNetStream = Peer.GetStream();
        using var proxyNetStream = proxy.GetStream();

        peerNetStream.ReadTimeout = 15000;
        proxyNetStream.ReadTimeout = 15000;
        
        var receiver = new Receiver(proxyNetStream);
        receiver.BytesReceived += (bytes, n) =>
        {
            Log.Debug("Forwarding proxy->peer {NumBytes} bytes {ProxyAddr} -> {PeerAddr}", n, proxy.Client.RemoteEndPoint, peer.Client.RemoteEndPoint);
            peerNetStream.Write(bytes, 0, n);
            peerNetStream.Flush();
        };
        
        ThreadPool.QueueUserWorkItem((_) =>
        {
            receiver.Run();
        });
        
        var buf = new byte[BufferSize];
        while (peer.Connected)
        {
            var n = peerNetStream.Read(buf);
            if (n == 0)
                continue;
            Log.Debug("Forwarding peer->proxy {NumBytes} bytes {PeerAddr} -> {ProxyAddr}", n, peer.Client.RemoteEndPoint, proxy.Client.RemoteEndPoint);
            proxyNetStream.Write(buf, 0, n);
            proxyNetStream.Flush();
        }
    }

    private class Receiver(NetworkStream stream)
    {
        private NetworkStream ProxyStream { get; } = stream;
        
        public delegate void BytesReceivedHandler(byte[] bytes, int n);

        public event BytesReceivedHandler? BytesReceived;

        public void Run()
        {
            try
            {
                var buf = new byte[BufferSize];
                while (true)
                {
                    var n = ProxyStream.Read(buf);
                    if (n == 0)
                        continue;
                    Log.Verbose("Receiver got {NumBytes} bytes", n);
                    BytesReceived?.Invoke(buf, n);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Receiver got exception");
            }
        }
    }
}