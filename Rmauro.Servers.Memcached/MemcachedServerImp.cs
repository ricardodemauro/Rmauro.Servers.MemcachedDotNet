using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Rmauro.Servers.Memcached;

public class MemcachedServerImp(int port = 8888) : IMemcachedServer
{
    readonly int _port = port;

    readonly ICommandResolver _commandResolver = new CommandResolver();

    readonly ConcurrentDictionary<string, string> _state = new();

    public async Task StartAsync()
    {
        Log.Information("Starting server at {Port}", _port);

        await Listen(CancellationToken.None);
    }

    async Task Listen(CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Any, _port);

        listener.Start();

        Log.Information("Starting to listen on port {Port}. Waiting for connections", _port);

        while (cancellationToken.IsCancellationRequested == false)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);

            Log.Information("Client has connected");

            await ProcessClient(client, cancellationToken);
        }
    }

    async Task ProcessClient(TcpClient client, CancellationToken cancellationToken)
    {
        using var networkStream = client.GetStream();

        var buffer = ArrayPool<byte>.Shared.Rent(1024);

        while (cancellationToken.IsCancellationRequested == false)
        {
            var bytesRead = await networkStream.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                Log.Information("Zero bytes read. Disconnecting client");
                break;
            }

            var msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            ArrayPool<byte>.Shared.Return(buffer);

            Log.Debug("Got message {Payload}", msg);

            var response = ProcessMessage(ref msg);
            
            if (string.IsNullOrEmpty(response))
            {
                Log.Information("Got no response to return. Ignoring message");
                continue;
            }

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);

            Log.Debug("Sending back {Payload}", response);

            await networkStream.WriteAsync(responseBytes, cancellationToken)
                .ConfigureAwait(false);

            await networkStream.FlushAsync(cancellationToken)
                .ConfigureAwait(false);

            Log.Debug("Completed response");
        }
    }

    string? ProcessMessage(ref string msg)
    {
        string[] cmd = _commandResolver.CommandArgs(ref msg);

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
