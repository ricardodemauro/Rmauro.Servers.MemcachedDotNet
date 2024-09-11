using System;

namespace Rmauro.Servers.Memcached.Connections;

public interface IConnectionResolver
{
    Task StartAsync(CancellationToken cancellationToken);
}
