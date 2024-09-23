using Microsoft.Extensions.DependencyInjection;
using Rmauro.Servers.Memcached.Listeners.Options;
using Rmauro.Servers.Memcached.Servers;
using System.Net;

namespace Rmauro.Servers.Memcached.Listeners;

public static class TcpClientListenerExtensions
{
    public static IServerBuilder UseTcpClientListener(
        this IServerBuilder builder,
        Action<ListenerOptions> configure)
    {
        builder.ConfigureServices(s =>
        {
            s.Configure<ListenerOptions>(configure);
            s.AddSingleton<ISocketListener, TcpClientListener>();
        });
        return builder;
    }

    public static IServerBuilder UseTcpClientListener(
        this IServerBuilder builder,
        int port)
    {
        return builder.UseTcpClientListener(new IPEndPoint(IPAddress.Any, port));
    }

    public static IServerBuilder UseTcpClientListener(
        this IServerBuilder builder,
        IPEndPoint endpoint)
    {
        builder.ConfigureServices(s =>
        {
            s.Configure<ListenerOptions>(c =>
            {
                c.EndPoint = endpoint;
            });

            s.AddSingleton<ISocketListener, TcpClientListener>();
        });
        return builder;
    }
}
