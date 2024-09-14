using System.Collections.Concurrent;
using Rmauro.Servers.Memcached.Connections;
using Serilog;

namespace Rmauro.Servers.Memcached;

public class MemcachedServerImp(int port = 11211) : IMemcachedServer
{
    readonly CommandResolver _commandResolver = new CommandResolver();

    // readonly ConcurrentDictionary<string, string> _state = new(Environment.ProcessorCount * 2, 20);
    readonly ConcurrentDictionary<string, string> _state = new();

    public async Task StartAsync()
    {
        Log.Information("Starting server at {Port}", port);

        await Listen(CancellationToken.None);
    }

    async Task Listen(CancellationToken cancellationToken)
    {
        #if TCP_RESOLVER
        TCPConnectionResolver resolver = new TCPConnectionResolver(port, this);
        #elif PIPE_RESOLVER
        PipeReaderConnectionResolver resolver = new PipeReaderConnectionResolver(port, this);
        #elif PIPELINES_RESOLVER
        PipelinesConnectionResolver resolver = new PipelinesConnectionResolver(port, this);
        #elif IOCP_RESOLVER
        IOCPConnectionResolver resolver = new IOCPConnectionResolver(port, this);
        #else
        SemaphoneTCPConnectionResolver resolver = new SemaphoneTCPConnectionResolver(port, this);
        #endif

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
            _ => "ERROR\r\n"
        };
    }

    private string ProcessAdd(string key, string flags, string expiration, string bytesLen, string data)
    {
        if (_state.ContainsKey(key)) return "NOT_STORED\r\n";
        _state[key] = data;
        return "STORED\r\n";
    }

    private string ProcessFlushAll()
    {
        _state.Clear();
        return "OK\r\n";
    }

    string? ProcessGet(string key)
    {
        if (!_state.TryGetValue(key, out string? val) && string.IsNullOrEmpty(val))
            return "END\r\n";

        return $"{val}\nEND\r\n";
    }

    string ProcessSet(string key, string flags, string expiration, string bytesLen, string data)
    {
        _state[key] = data;
        return "STORED\r\n";
    }
}
