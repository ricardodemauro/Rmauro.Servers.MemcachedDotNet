using Microsoft.Extensions.Logging;
using Rmauro.Servers.Memcached.Listeners;
using Rmauro.Servers.Memcached.Servers.Commands;

namespace Rmauro.Servers.Memcached.Servers;

public class MemcachedServer
{
    readonly ISocketListener _listener;

    readonly ILogger _logger;

    readonly ICommandParser _commandResolver;

    readonly MemcachedState _cache = new();

    public MemcachedServer(ISocketListener listener, ICommandParser commandParser, ILogger<MemcachedServer> logger)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _commandResolver = commandParser ?? throw new ArgumentOutOfRangeException(nameof(commandParser));
    }

    public static ServerBuilder CreateBuilder(string[] args)
        => new();

    public async Task Start(CancellationToken cancellationToken)
    {
        var processMessage = new ProcessRequestDelegate(ProcessRequest);

        await _listener.Start(processMessage, cancellationToken);
    }

    public Memory<byte> ProcessRequest(in ReadOnlySpan<byte> data)
    {
        var command = _commandResolver.CommandArgs(data);

        if (command.CommandType == CommandType.Get)
        {
            return _cache.Get(command.Key);
        }
        if (command.CommandType == CommandType.Set)
        {
            return _cache.Set(command.Key, command.Value);
        }
        if (command.CommandType == CommandType.Add)
        {
            return _cache.Add(command.Key, command.Value);
        }
        if (command.CommandType == CommandType.FlushAll)
        {
            return _cache.FlushAll();
        }
        return Array.Empty<byte>();
    }
}
