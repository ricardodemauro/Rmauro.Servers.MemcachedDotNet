using Microsoft.Extensions.DependencyInjection;
using Rmauro.Servers.Memcached.Listeners.Options;
using Rmauro.Servers.Memcached.Servers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Rmauro.Servers.Memcached.Listeners;

public static class EnvironmentSocketExtension
{
    public static IServerBuilder UseListener(
        this IServerBuilder builder,
        int port,
        int maxConcurrentConnections)
    {
        return builder.UseListener(new IPEndPoint(IPAddress.Any, port), maxConcurrentConnections);
    }

    public static IServerBuilder UseListener(
        this IServerBuilder builder,
        IPEndPoint endpoint,
        int maxConcurrentConnections)
    {
        builder.ConfigureServices(s =>
        {
            s.Configure<ListenerOptions>(c =>
            {
                c.EndPoint = endpoint;
                c.MaxConnections = maxConcurrentConnections;
                c.UseMemoryPool = string.Equals(Environment.GetEnvironmentVariable("USE_OBJECT_POOL") ?? string.Empty, bool.TrueString, StringComparison.OrdinalIgnoreCase);
            });

            var env = Environment.GetEnvironmentVariable("SOCKET_LISTENER") ?? "IOCPSocketListener";

            if (env == "IOCPSocketListener")
            {
                s.AddSingleton<ISocketListener, IOCPSocketListener>();
            }
            if (env == "IOCP2SocketListener")
            {
                s.AddSingleton<ISocketListener, IOCP2SocketListener>();
            }
            if (env == "TcpClientListener")
            {
                s.AddSingleton<ISocketListener, TcpClientListener>();
            }
        });
        return builder;
    }
}
