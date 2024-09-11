using System.Collections.Concurrent;
using Rmauro.Servers.Memcached.Connections;
using Serilog;

namespace Rmauro.Servers.Memcached;

public class MemcachedServerImp(int port = 8888) : IMemcachedServer
{
    readonly ICommandResolver _commandResolver = new CommandResolver();

    readonly ConcurrentDictionary<string, string> _state = new();

    public async Task StartAsync()
    {
        Log.Information("Starting server at {Port}", port);

        await Listen(CancellationToken.None);
    }

    async Task Listen(CancellationToken cancellationToken)
    {
        IConnectionResolver resolver = new TCPConnectionResolver(port, this);

        await resolver.StartAsync(cancellationToken);
    }

    public string? ProcessMessage(string message)
    {
        string[] cmd = _commandResolver.CommandArgs(message);

        return cmd[0] switch
        {
            "" => string.Empty,
            Commands.Get => ProcessGet(cmd[1]),
            Commands.FlushAll => ProcessFlushAll(),
            Commands.Add => ProcessAdd(cmd[1], cmd[2], cmd[3], cmd[4], cmd[5]),
            Commands.Set => ProcessSet(cmd[1], cmd[2], cmd[3], cmd[4], cmd[5]),
            _ => throw new ArgumentOutOfRangeException("command"),
        };
    }

    private string ProcessAdd(string key, string flags, string expiration, string bytesLen, string data)
    {
        if(_state.ContainsKey(key)) return "NOT_STORED\n";
       _state[key] = data;
        return "STORED\n";   
    }

    private string ProcessFlushAll()
    {
        _state.Clear();
        return "OK\n";
    }

    string? ProcessGet(string args)
    {
        string? val = _state.GetValueOrDefault(args);
        if(string.IsNullOrEmpty(val)) return "END\n";

        return $"{val}\nEND\n";
    }

    string ProcessSet(string key, string flags, string expiration, string bytesLen, string data)
    {
        _state[key] = data;
        return "STORED\n";
    }
}
