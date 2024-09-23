using Microsoft.Extensions.DependencyInjection;
using Rmauro.Servers.Memcached.Listeners.Options;
using Rmauro.Servers.Memcached.Servers;
using System.Net;

namespace Rmauro.Servers.Memcached.Listeners;

public static class IOCPSocketListenerExtensions
{
    public static IServerBuilder UseIOCPSocketListener(
        this IServerBuilder builder,
        int port)
    {
        return builder.UseIOCPSocketListener(new IPEndPoint(IPAddress.Any, port));
    }

    public static IServerBuilder UseIOCPSocketListener(
        this IServerBuilder builder,
        IPEndPoint endpoint)
    {
        builder.ConfigureServices(s =>
        {
            s.Configure<ListenerOptions>(c =>
            {
                c.EndPoint = endpoint;
            });

            s.AddSingleton<ISocketListener, IOCPSocketListener>();
        });
        return builder;
    }
}
