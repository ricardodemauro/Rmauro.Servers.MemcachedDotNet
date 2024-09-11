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
            if(string.IsNullOrEmpty(response))
            {
                Log.Information("Got no response to return. Disconnecting client");
                break;
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
        string cmd = CommandResolver.Command(ref msg);

        return cmd switch
        {
            "" => string.Empty,
            Commands.Get => ProcessGet(CommandResolver.GetArgument(ref msg)),
            Commands.FlushAll => ProcessFlushAll(CommandResolver.GetArgument(ref msg)),
            Commands.Set => ProcessSet(CommandResolver.GetArgument(ref msg)),
            _ => throw new ArgumentOutOfRangeException("command"),
        };
    }

    private string ProcessFlushAll(string args)
    {
        return string.Empty;
    }

    string? ProcessGet(string args) => _state.GetValueOrDefault(args);

    string ProcessSet(string args)
    {
        return string.Empty;
    }
}
