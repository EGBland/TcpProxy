// See https://aka.ms/new-console-template for more information

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Serilog;
using TcpProxy;

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

var config = new ConfigurationBuilder().AddXmlFile("Config.xml").Build();
var forwardingConfig = config.GetSection("ForwardTo");
var listeningConfig = config.GetSection("ListenOn");

var forwardAddr = ParseAddress(forwardingConfig.GetValue<string>("Address")!);
var forwardPort = forwardingConfig.GetValue<int>("Port");
var forwardEndpoint = new IPEndPoint(forwardAddr, forwardPort);

var listenAddr = IPAddress.Any;
var listenPort = listeningConfig.GetValue<int>("Port");
var listenEndpoint = new IPEndPoint(listenAddr, listenPort);

var timeout = config.GetValue<int>("Timeout");

//Console.WriteLine($"Will proxy {listenAddr}:{listenPort} -> {forwardAddr}:{forwardPort}");
Log.Information("Will proxy {ListenEndpoint} -> {ForwardEndpoint}", listenEndpoint, forwardEndpoint);

var listener = new TcpListener(listenEndpoint);
listener.Start();
while (true)
{
    var peer = listener.AcceptTcpClient();
    ThreadPool.QueueUserWorkItem((_) =>
    {
        try
        {
            var session = new ProxySession(peer, forwardEndpoint);
            session.Run();
            peer.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Proxy session encountered exception");
        }
    });
}

Log.CloseAndFlush();
return 0;

IPAddress ParseAddress(string addr)
{
    var fields = addr.Split(".");
    if (fields.Length != 4)
        throw new ArgumentException($"Provided string \"{addr}\" does not conform to \"(0-255).(0-255).(0-255).(0-255)\".");

    ulong addrAcc = 0;
    foreach (var field in fields.Reverse())
    {
        var fieldVal = ulong.Parse(field);
        if (fieldVal > 255)
        {
            throw new ArgumentException($"Provided string \"{addr}\" does not conform to \"(0-255).(0-255).(0-255).(0-255)\".");
        }

        addrAcc <<= 8;
        addrAcc |= fieldVal;
    }

    return new IPAddress((long)addrAcc);
}