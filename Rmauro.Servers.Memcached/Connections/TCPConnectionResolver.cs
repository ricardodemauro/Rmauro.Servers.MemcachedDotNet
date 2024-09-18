using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Rmauro.Servers.Memcached.Connections;

public class TCPConnectionResolver(int port, IMemcachedServer server) : IConnectionResolver
{
    int _connectedClients = 0;

    readonly int _maxClients = 250;

    readonly int _port = port;

    readonly IMemcachedServer _server = server;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Log.Warning("Starting server TCPConnectionResolver at {Port}", _port);

        await Listen(cancellationToken);
    }

    async Task Listen(CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Any, _port);

        listener.Start(_maxClients);

        Log.Information("Starting to listen on port {Port}. Waiting for connections", _port);

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);

            //Interlocked.Add(ref _connectedClients, 1);
            Log.Information("Client {RemoteEndPoint} has connected. Total clients connected is {ConnectedClients}",
                client.Client.RemoteEndPoint,
                _connectedClients);


            _ = Task.Factory.StartNew(async () => await ProcessClient(client, cancellationToken), cancellationToken);
        }
    }

     async Task ProcessClient(TcpClient client, CancellationToken cancellationToken)
    {
        using var networkStream = client.GetStream();

        var buffer = ArrayPool<byte>.Shared.Rent(4096);

        while (true)
        {
            var bytesRead = await networkStream.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                //Interlocked.Decrement(ref _connectedClients);
                Log.Information("Client {RemoteEndPoint} disconnected. Total clients connected is {ConnectedClients}",
                    client.Client.RemoteEndPoint,
                    _connectedClients);

                client.Dispose();
                break;
            }

            string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            //Log.Debug("Got message {Payload}", msg);

            var response = _server.ProcessMessage(msg.AsSpan());

            if (string.IsNullOrEmpty(response))
            {
                Log.Information("Got no response to return. Ignoring message");
                continue;
            }

            var sequence = new ReadOnlySequence<char>(response.AsMemory());

            var responseBytes = Encoding.UTF8.GetBytes(sequence);
                //.AsMemory();

            await networkStream.WriteAsync(responseBytes, cancellationToken)
                .ConfigureAwait(false);

            //await networkStream.FlushAsync(cancellationToken)
            //    .ConfigureAwait(false);

            //networkStream.Write(responseBytes.AsSpan());
        }
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
