using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Rmauro.Servers.Memcached;

public delegate string ProcessMessage(Memory<byte> message);

public interface IListener
{
    Task StartAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<IListenerClient> GetListenerClient(CancellationToken cancellationToken);
}

public interface IListenerClient : IDisposable
{
    IAsyncEnumerable<Memory<byte>> GetMessageIterator(CancellationToken cancellationToken);

    Task WriteBack(Memory<byte> message, CancellationToken cancellationToken);
}

public interface IServerBuilder
{
    IServerBuilder ConfigureServices(Action<IServiceCollection> configureServices);

    SupaServer Build();
}

public class SupaServerBuilder : IServerBuilder
{
    public SupaServerBuilder(SupaServerOptions options)
    {
        Services = new ServiceCollection();
    }

    public IServiceCollection Services { get; private set; }

    public IServerBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        //configureServices.Invoke(Services);
        configureServices(this.Services);
        return this;
    }

    public SupaServer Build()
    {
        Services.AddLogging(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });

        Services.AddSingleton<ICommandResolver, CommandResolver>();

        var sp = ((ServiceCollection)Services).BuildServiceProvider();

        return new SupaServer(sp, sp.GetService<ILogger<SupaServer>>());
    }
}

public class SupaServerOptions
{
    public string EnvironmentName { get; init; }

    public string ApplicationName { get; init; }
}

public class SupaServer
{
    readonly IServiceProvider _sp;

    readonly ILogger<SupaServer> _logger;

    readonly ConcurrentStack<IListenerClient> _sockets = new();

    readonly MemcachedServer _cache = new();

    readonly ICommandResolver _commandResolver;

    public SupaServer(IServiceProvider sp, ILogger<SupaServer> logger)
    {
        _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _commandResolver = _sp.GetRequiredService<ICommandResolver>();
    }

    public static SupaServerBuilder CreateBuilder(string[] args)
        => new(new()
        {
            ApplicationName = Process.GetCurrentProcess().ProcessName,
            EnvironmentName = Environment.GetEnvironmentVariable("SUPA_ENV") ?? "Development"
        });


    public async Task Start(CancellationToken cancellationToken)
    {
        var listener = _sp.GetRequiredService<IListener>();

        await foreach (var client in listener.GetListenerClient(cancellationToken))
        {
            _sockets.Push(client);

            _ = ProcessClient(client, cancellationToken);
        }
    }

    async Task ProcessClient(IListenerClient client, CancellationToken cancellationToken)
    {
        await foreach (var message in client.GetMessageIterator(cancellationToken))
        {
            var msg = Encoding.UTF8.GetString(message.ToArray());
            Console.WriteLine("Echo: {0}", msg);

            var cmds = _commandResolver.CommandArgs(msg.AsSpan());

            var response = _cache.ProcessMessage(cmds);
            var bufferResponse = Encoding.UTF8.GetBytes(response);
            await client.WriteBack(bufferResponse.AsMemory(), cancellationToken);
        }
        client.Dispose();
    }
}

public class MemcachedServer
{
    readonly ConcurrentDictionary<string, string> _state = new(Environment.ProcessorCount * 2, 20);
    //readonly ConcurrentDictionary<string, string> _state = new();

    public string ProcessMessage(string[] cmd)
    {
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