using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Rmauro.Servers.Memcached.Connections;

public class PipeReaderConnectionResolver(int port, IMemcachedServer server) : IConnectionResolver
{
    readonly int _maxClients = 500;

    readonly int _port = port;

    readonly IMemcachedServer _server = server;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Log.Warning("Starting server PipeReaderConnectionResolver at {Port}", _port);

        await Listen(cancellationToken);
    }

    async Task Listen(CancellationToken cancellationToken)
    {
        var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.Any, _port));

        listenSocket.Listen(_maxClients);

        Log.Information("Starting to listen on port {Port}. Waiting for connections", _port);

        while (cancellationToken.IsCancellationRequested == false)
        {
            var client = await listenSocket.AcceptAsync(cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                break;

            Log.Information("Client has connected");

            _ = Task.Factory.StartNew(async () => await ProcessLinesAsync(client, cancellationToken), cancellationToken);
        }
    }

    async Task ProcessLinesAsync(Socket socket, CancellationToken cancellationToken)
    {
        Log.Debug($"[{socket.RemoteEndPoint}]: connected");

        // Create a PipeReader over the network stream
        var stream = new NetworkStream(socket);
        var reader = PipeReader.Create(stream);

        while (cancellationToken.IsCancellationRequested == false)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = result.Buffer;

            string msg = Encoding.UTF8.GetString(buffer);

            var response = _server.ProcessMessage(msg);


            var responseBytes = Encoding.UTF8.GetBytes(response);

            await socket.SendAsync(responseBytes, SocketFlags.None);

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
                break;
        }

        // Mark the PipeReader as complete.
        await reader.CompleteAsync();

        Log.Debug($"[{socket.RemoteEndPoint}]: disconnected");
    }
}
