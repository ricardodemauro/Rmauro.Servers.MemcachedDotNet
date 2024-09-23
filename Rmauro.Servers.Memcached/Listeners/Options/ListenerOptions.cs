using System.Net;

namespace Rmauro.Servers.Memcached.Listeners.Options;

public sealed class ListenerOptions
{
    public IPEndPoint EndPoint { get; set; }

    public int MaxConnections { get; set; } = 4096;
}
