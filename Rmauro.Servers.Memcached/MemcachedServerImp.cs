using System.Collections.Concurrent;
using Rmauro.Servers.Memcached.Connections;
using Serilog;

namespace Rmauro.Servers.Memcached;

public class MemcachedServerImp(int port = 11211) : IMemcachedServer
{
    readonly CommandResolver _commandResolver = new();

    // readonly ConcurrentDictionary<string, string> _state = new(Environment.ProcessorCount * 2, 20);
    readonly ConcurrentDictionary<string, string> _state = new(Environment.ProcessorCount * 2, 200);

    public async Task StartAsync()
    {
        Log.Information("Starting server at {Port}", port);

        await Listen(CancellationToken.None);
    }

    IConnectionResolver BuildResolver(int port)
    {
        var env = Environment.GetEnvironmentVariable("TCP_RESOLVER");

        if (env == "SOCKET_RESOLVER") return new SocketConnectionResolver(port, this);

        if (env == "LIBUV_RESOLVER") return new LibuvConnectionResolver(port, this);

        if (env == "TCP_RESOLVER") return new TCPConnectionResolver(port, this);

        if (env == "PIPE_READER_RESOLVER") return new PipeReaderConnectionResolver(port, this);

        if (env == "PIPELINES_RESOLVER") return new PipelinesConnectionResolver(port, this);

        if (env == "IOCP_RESOLVER") return new IOCPConnectionResolver(port, this);

        return new SemaphoneTCPConnectionResolver(port, this);
    }

    async Task Listen(CancellationToken cancellationToken)
    {
        var resolver = BuildResolver(port);

        await resolver.StartAsync(cancellationToken);
    }

    public string ProcessMessage(ReadOnlySpan<char> message)
    {
        (string cmd, string key, string value) = _commandResolver.CommandArgs(message);

        return cmd switch
        {
            "" => string.Empty,
            Commands.Get => ProcessGet(ref key),
            Commands.FlushAll => ProcessFlushAll(),
            Commands.Add => ProcessAdd(ref key, ref value),
            Commands.Set => ProcessSet(ref key, ref value),
            _ => "ERROR\r\n"
        };
    }

    private string ProcessAdd(ref string key, ref string data)
    {
        //if (_state.ContainsKey(key)) return "NOT_STORED\r\n";
        //_state[key] = data;
        return "STORED\r\n";
    }

    private string ProcessFlushAll()
    {
        _state.Clear();
        return "OK\r\n";
    }

    string ProcessGet(ref string key)
    {
        if (!_state.TryGetValue(key, out string val) && string.IsNullOrEmpty(val))
            return "END\r\n";

        return $"{val}\nEND\r\n";
    }

    string ProcessSet(ref string key, ref string data)
    {
        //_state.TryAdd(key, data);
        //_state[key] = data;
        return "STORED\r\n";
    }
}
