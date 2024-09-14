using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Rmauro.Servers.Memcached.Connections;

public class PipelinesConnectionResolver(int port, IMemcachedServer server) : IConnectionResolver
{
    int connectedClients = 0;

    readonly int _port = port;

    readonly IMemcachedServer _server = server;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Log.Warning("Starting server PipelinesConnectionResolver at {Port}", _port);

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

            Log.Information("Client has connected");

            _ = ProcessClient(client, cancellationToken);
        }
    }

    async Task ProcessClient(TcpClient client, CancellationToken cancellationToken)
    {
        //Interlocked.Increment(ref connectedClients);

        Log.Debug("Connected with {RemoteEndPoint}. Total clients connected is {ConnectedClients}",
                        client.Client.RemoteEndPoint,
                        connectedClients);

        var pipe = new Pipe();

        var reading = FillPipeAsync(client.Client, pipe.Writer, cancellationToken);
        var writing = ReadPipeAsync(client.Client, pipe.Reader, cancellationToken);

        await Task.WhenAll(reading, writing);

        //Interlocked.Decrement(ref connectedClients);

        Log.Debug("Disconnecting client. Total clients connected is {ConnectedClients}",
                    connectedClients);

        client.Dispose();
    }

    async Task FillPipeAsync(Socket socket, PipeWriter writer, CancellationToken cancellationToken)
    {
        const int minimumBufferSize = 512;

        while (cancellationToken.IsCancellationRequested == false)
        {
            try
            {
                var memory = writer.GetMemory(minimumBufferSize);
                int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, cancellationToken);
                if (bytesRead == 0)
                {
                    Log.Debug("Disconnecting client.");
                    break;
                }

                writer.Advance(bytesRead);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while reading from stream");
                break;
            }

            var result = await writer.FlushAsync(cancellationToken);

            if (result.IsCompleted)
            {
                break;
            }
        }

        await writer.CompleteAsync();
    }

    async Task ReadPipeAsync(Socket socket, PipeReader reader, CancellationToken cancellationToken)
    {
        while (cancellationToken.IsCancellationRequested == false)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = result.Buffer;

            if (cancellationToken.IsCancellationRequested)
                break;

            if (buffer.Length > 0)
            {
                var message = Encoding.UTF8.GetString(buffer);
                Log.Debug($"Received: {message}");

                var response = _server.ProcessMessage(message) ?? string.Empty;

                // Echo the message back to the client
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                await socket.SendAsync(responseBytes, SocketFlags.None, cancellationToken);
            }

            // Tell the PipeReader how much of the buffer has been consumed.
            reader.AdvanceTo(buffer.Start, buffer.End);

            // Stop reading if there's no more data coming.
            if (result.IsCompleted)
            {
                Log.Information("Disconecting client");
                break;
            }
        }

        // Mark the PipeReader as complete.
        await reader.CompleteAsync();
    }
}
