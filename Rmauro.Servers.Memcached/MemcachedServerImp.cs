using System.Collections.Concurrent;
using Rmauro.Servers.Memcached.Connections;
using Serilog;

namespace Rmauro.Servers.Memcached;

public class MemcachedServerImp(int port = 11211) : IMemcachedServer
{
    readonly CommandResolver _commandResolver = new CommandResolver();

    // readonly ConcurrentDictionary<string, string> _state = new(Environment.ProcessorCount * 2, 20);
    readonly ConcurrentDictionary<string, string> _state = new(Environment.ProcessorCount * 2, 200);

    public async Task StartAsync()
    {
        Log.Information("Starting server at {Port}", port);

        await Listen(CancellationToken.None);
    }

    async Task Listen(CancellationToken cancellationToken)
    {
#if SOCKET_RESOLVER
        SocketConnectionResolver resolver = new(port, this);
#elif LIBUV_RESOLVER
        LibuvConnectionResolver resolver = new(port, this);
#elif TCP_RESOLVER
        TCPConnectionResolver resolver = new (port, this);
#elif PIPE_READER_RESOLVER
        PipeReaderConnectionResolver resolver = new (port, this);
#elif PIPELINES_RESOLVER
        PipelinesConnectionResolver resolver = new (port, this);
#elif IOCP_RESOLVER
        IOCPConnectionResolver resolver = new (port, this);
#else
        SemaphoneTCPConnectionResolver resolver = new (port, this);
#endif

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
