using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace Rmauro.Servers.Memcached.Listeners;

public static class PureSocketListenerExtensions
{
    public static IServerBuilder UseTcpClientListener(
        this IServerBuilder builder,
        Action<TcpClientListenerOptions> configure)
    {
        builder.ConfigureServices(s =>
        {
            s.Configure<TcpClientListenerOptions>(configure);
            s.AddSingleton<IListener, TcpClientListener>();
        });
        return builder;
    }

    public static IServerBuilder UseTcpClientListener(
        this IServerBuilder builder,
        int port)
    {
        builder.ConfigureServices(s =>
        {
            s.Configure<TcpClientListenerOptions>(c =>
            {
                c.EndPoint = new IPEndPoint(IPAddress.Any, port);
            });

            s.AddSingleton<IListener, TcpClientListener>();
        });
        return builder;
    }
}

public class TcpClientListenerOptions
{
    public IPEndPoint EndPoint { get; set; }
}

public class TcpClientStreamClient(TcpClient client) : IListenerClient
{
    readonly TcpClient _client = client ?? throw new ArgumentNullException(nameof(client));

    readonly NetworkStream _networkStream = client.GetStream();

    byte[] _buffer = ArrayPool<byte>.Shared.Rent(1024);

    public void Dispose()
    {
        _client?.Dispose();
        _networkStream?.Dispose();

        ArrayPool<byte>.Shared.Return(_buffer);
    }

    public async IAsyncEnumerable<Memory<byte>> GetMessageIterator(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var bufferMemory = _buffer.AsMemory();

        while (cancellationToken.IsCancellationRequested == false)
        {
            var bytesRead = await _networkStream.ReadAsync(bufferMemory, cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            yield return bufferMemory[..bytesRead];
        }
    }

    public async Task WriteBack(Memory<byte> message, CancellationToken cancellationToken)
    {
        await _networkStream.WriteAsync(message, cancellationToken);
        await _networkStream.FlushAsync(cancellationToken);
    }
}

public class TcpClientListener(
    ILogger<TcpClientListener> logger,
    IOptions<TcpClientListenerOptions> options) : IListener
{
    volatile int connectedClients = 0;

    ProcessMessage listener;

    public async IAsyncEnumerable<IListenerClient> GetListenerClient(
         [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(options.Value.EndPoint);
        listener.Start();

        while (cancellationToken.IsCancellationRequested == false)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);

            //Interlocked.Add(ref _connectedClients, 1);
            Log.Information("Client {RemoteEndPoint} has connected. Total clients connected is {ConnectedClients}",
                client.Client.RemoteEndPoint,
                connectedClients);

            yield return new TcpClientStreamClient(client);
        }
    }

    public async IAsyncEnumerable<(Memory<byte>, IListener)> GetMessageIterator(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(options.Value.EndPoint);
        listener.Start();

        while (cancellationToken.IsCancellationRequested == false)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);

            //Interlocked.Add(ref _connectedClients, 1);
            Log.Information("Client {RemoteEndPoint} has connected. Total clients connected is {ConnectedClients}",
                client.Client.RemoteEndPoint,
                connectedClients);


            using var networkStream = client.GetStream();

            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            var bufferMemory = buffer.AsMemory();

            while (cancellationToken.IsCancellationRequested == false)
            {

                var bytesRead = await networkStream.ReadAsync(bufferMemory, cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    connectedClients = connectedClients - 1;

                    Log.Information("Client {RemoteEndPoint} disconnected. Total clients connected is {ConnectedClients}",
                        client.Client.RemoteEndPoint,
                        connectedClients);

                    client.Dispose();
                    break;
                }

                 yield return (bufferMemory, this);
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Register(ProcessMessage processMessage)
    {
        listener = processMessage;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting server TCPConnectionResolver at {Port}", options.Value.EndPoint.Port);

        return Listen(cancellationToken);
    }

    public Task WriteBack(Memory<byte> message)
    {
        throw new NotImplementedException();
    }

    async Task Listen(CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(options.Value.EndPoint);

        listener.Start();

        while (cancellationToken.IsCancellationRequested == false)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);

            //Interlocked.Add(ref _connectedClients, 1);
            Log.Information("Client {RemoteEndPoint} has connected. Total clients connected is {ConnectedClients}",
                client.Client.RemoteEndPoint,
                connectedClients);


            _ = ProcessClient(client, cancellationToken);
        }
    }

    async Task ProcessClient(TcpClient client, CancellationToken cancellationToken)
    {
        using var networkStream = client.GetStream();

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        var bufferMemory = buffer.AsMemory();
        try
        {
            while (cancellationToken.IsCancellationRequested == false)
            {

                var bytesRead = await networkStream.ReadAsync(bufferMemory, cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    connectedClients = connectedClients - 1;

                    Log.Information("Client {RemoteEndPoint} disconnected. Total clients connected is {ConnectedClients}",
                        client.Client.RemoteEndPoint,
                        connectedClients);

                    client.Dispose();
                    break;
                }

                var response = listener.Invoke(bufferMemory);

                if (string.IsNullOrEmpty(response))
                {
                    Log.Debug("Got no response to return. Ignoring message");
                    continue;
                }

                byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                Log.Debug("Sending back {Payload}", response);

                await networkStream.WriteAsync(responseBytes, cancellationToken)
                    .ConfigureAwait(false);

                await networkStream.FlushAsync(cancellationToken)
                    .ConfigureAwait(false);

                Log.Debug("Flushed Stream");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error when processing message");
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
