// See https://aka.ms/new-console-template for more information
using System.Collections.Concurrent;
using System.Net.Sockets;

Console.WriteLine("Hello, World!");

var listener = new TcpListener(System.Net.IPAddress.Any, 11211);
listener.Start();

var memCached = new MemcachedClone();

var client = await listener.AcceptTcpClientAsync();
await memCached.HandleConnection(client);

class MemcachedClone
{
    readonly ConcurrentDictionary<string, string> _cachedValues = new();

    public async Task HandleConnection(TcpClient tcpClient)
    {

    }

    string Execute(List<string> arguments)
    {
        switch(arguments[0].ToUpperInvariant())
        {
            case "GET":
                return _cachedValues.GetValueOrDefault(arguments[1]) ?? string.Empty;
            case "SET":
                _cachedValues[arguments[1]] = arguments[2];
                return string.Empty;
            default:
                return string.Empty;
        }
    }
}