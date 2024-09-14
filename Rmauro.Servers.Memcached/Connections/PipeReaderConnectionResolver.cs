using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Rmauro.Servers.Memcached.Connections;

public class PipeReaderConnectionResolver(int port, IMemcachedServer server) : IConnectionResolver
{
    int _connectedClients = 0;

    readonly int _maxClients = 100;

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

        listenSocket.Listen(120);

        Log.Information("Starting to listen on port {Port}. Waiting for connections", _port);

        while (cancellationToken.IsCancellationRequested == false)
        {
            var client = await listenSocket.AcceptAsync(cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                break;

            Log.Information("Client has connected");

            _ = ProcessLinesAsync(client, cancellationToken);
        }
    }

    async Task ProcessLinesAsync(Socket socket, CancellationToken cancellationToken)
    {
        Log.Debug($"[{socket.RemoteEndPoint}]: connected");

        // Create a PipeReader over the network stream
        var stream = new NetworkStream(socket);
        var reader = PipeReader.Create(stream);

        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            //var msg = buffer.ToString();
            //var msg = buffer.Slice(0, buffer.Length).ToString();
            var msg = Encoding.UTF8.GetString(buffer);

            //while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
            {
                // var response = _server.ProcessMessage(line.ToString());
                var response = _server.ProcessMessage(msg);

                await socket.SendAsync(Encoding.UTF8.GetBytes(response), SocketFlags.None);
                // Process the line.
                //ProcessLine(line);
            }

            // Tell the PipeReader how much of the buffer has been consumed.
            reader.AdvanceTo(buffer.Start, buffer.End);

            // Stop reading if there's no more data coming.
            if (result.IsCompleted)
            {
                break;
            }
        }

        // Mark the PipeReader as complete.
        await reader.CompleteAsync();

        Log.Debug($"[{socket.RemoteEndPoint}]: disconnected");
    }
}
