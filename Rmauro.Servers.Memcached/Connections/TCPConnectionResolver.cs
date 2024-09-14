using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Rmauro.Servers.Memcached.Connections;

public class TCPConnectionResolver(int port, IMemcachedServer server) : IConnectionResolver
{
    int _connectedClients = 0;

    readonly int _maxClients = 100;

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

        listener.Start();

        Log.Information("Starting to listen on port {Port}. Waiting for connections", _port);

        while (cancellationToken.IsCancellationRequested == false)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);

            //Interlocked.Add(ref _connectedClients, 1);
            Log.Information("Client {RemoteEndPoint} has connected. Total clients connected is {ConnectedClients}",
                client.Client.RemoteEndPoint,
                _connectedClients);


            _ = ProcessClient(client, cancellationToken);
        }
    }

    async Task ProcessClient(TcpClient client, CancellationToken cancellationToken)
    {
        using var networkStream = client.GetStream();

        while (cancellationToken.IsCancellationRequested == false)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(1024);

            var bytesRead = await networkStream.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                Interlocked.Decrement(ref _connectedClients);
                Log.Information("Client {RemoteEndPoint} disconnected. Total clients connected is {ConnectedClients}",
                    client.Client.RemoteEndPoint,
                    _connectedClients);

                client.Dispose();
                break;
            }

            var msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            ArrayPool<byte>.Shared.Return(buffer);

            Log.Debug("Got message {Payload}", msg);

            var response = _server.ProcessMessage(msg);

            if (string.IsNullOrEmpty(response))
            {
                Log.Information("Got no response to return. Ignoring message");
                continue;
            }

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);

            Log.Debug("Sending back {Payload}", response);

            // await networkStream.WriteAsync(responseBytes, cancellationToken)
            //     .ConfigureAwait(false);

            // await networkStream.FlushAsync(cancellationToken)
            //     .ConfigureAwait(false);

            networkStream.Write(responseBytes);

            networkStream.Flush();

            Log.Debug("Flushed Stream");

            //break;
        }
    }
}
