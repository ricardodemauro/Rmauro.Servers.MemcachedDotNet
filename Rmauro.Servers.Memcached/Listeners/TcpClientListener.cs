using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rmauro.Servers.Memcached.Listeners.Options;
using Serilog;
using System.Buffers;
using System.Net.Sockets;
using System.Text;

namespace Rmauro.Servers.Memcached.Listeners;

public class TcpClientListener(
    ILogger<TcpClientListener> logger,
    IOptions<ListenerOptions> options) : ISocketListener, IDisposable
{
    volatile int connectedClients = 0;

    List<TcpClient> _clients = new();

    public void Dispose()
    {
        logger.LogInformation("Disposing {CountClients} clients", _clients.Count);

        _clients.ForEach(x =>
        {
            x?.Dispose();
        });
    }

    public async Task Start(ProcessRequestDelegate process, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting server TCPConnectionResolver at {Port}", options.Value.EndPoint.Port);

        using var listener = new TcpListener(options.Value.EndPoint);

        listener.Start(options.Value.MaxConnections);

        while (cancellationToken.IsCancellationRequested == false)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);

            client.NoDelay = true;

            _clients.Add(client);

            connectedClients = connectedClients + 1;

            Log.Information("Client {RemoteEndPoint} has connected. Total clients connected is {ConnectedClients}",
                client.Client.RemoteEndPoint,
                connectedClients);


            _ = Task.Factory.StartNew(() =>
            {
                return ProcessClient(client, process, cancellationToken).ConfigureAwait(false);
            }, cancellationToken, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Current);
        }
    }


    async Task ProcessClient(TcpClient client, ProcessRequestDelegate process, CancellationToken cancellationToken)
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

                    client.Close();
                    client.Dispose();
                    break;
                }

#if DEBUG
                Log.Debug("Get Request {Request}", Encoding.UTF8.GetString(bufferMemory.ToArray()));
#endif

                var response = process.Invoke(bufferMemory.Span);

                if(response.Length == 0)
                {
                    Log.Debug("Got empty response. Ignoring message");
                    continue;
                }

#if DEBUG
                Log.Debug("Sending back response {Response}", Encoding.UTF8.GetString(response.ToArray()));
#endif

                await networkStream.WriteAsync(response, cancellationToken)
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
