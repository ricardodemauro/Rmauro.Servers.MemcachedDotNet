using Microsoft.Extensions.DependencyInjection;
using Rmauro.Servers.Memcached.Listeners.Options;
using Rmauro.Servers.Memcached.Servers;
using System.Net;

namespace Rmauro.Servers.Memcached.Listeners;

public static class IOCPSocketListenerExtensions
{
    public static IServerBuilder UseIOCPSocketListener(
        this IServerBuilder builder,
        int port, int maxConnections = 1024)
    {
        return builder.UseIOCPSocketListener(new IPEndPoint(IPAddress.Any, port), maxConnections);
    }

    public static IServerBuilder UseIOCPSocketListener(
        this IServerBuilder builder,
        IPEndPoint endpoint, int maxConnections)
    {
        builder.ConfigureServices(s =>
        {
            s.Configure<ListenerOptions>(c =>
            {
                c.EndPoint = endpoint;
                c.MaxConnections = maxConnections;
            });

            s.AddSingleton<ISocketListener, IOCPSocketListener>();
        });
        return builder;
    }
}
