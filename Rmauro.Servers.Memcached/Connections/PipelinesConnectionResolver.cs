using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Rmauro.Servers.Memcached.Connections;

public class PipelinesConnectionResolver(int port, IMemcachedServer server) : IConnectionResolver
{
    readonly int _port = port;

    readonly IMemcachedServer _server = server;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Log.Information("Starting server at {Port}", _port);

        await Listen(cancellationToken);
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
        var pipe = new Pipe();

        var reading = FillPipeAsync(client.Client, pipe.Writer);
        var writing = ReadPipeAsync(pipe.Reader, client.Client);

        await Task.WhenAll(reading, writing);
    }

    async Task FillPipeAsync(Socket socket, PipeWriter writer)
    {
        const int minimumBufferSize = 512;

        while (true)
        {
            try
            {
                var memory = writer.GetMemory(minimumBufferSize);
                int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
                if (bytesRead == 0)
                {
                    break;
                }

                writer.Advance(bytesRead);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while reading from stream");
                break;
            }

            var result = await writer.FlushAsync();

            if (result.IsCompleted)
            {
                break;
            }
        }

        await writer.CompleteAsync();
    }

    async Task ReadPipeAsync(PipeReader reader, Socket socket)
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
            {
                // Process the line.
                ProcessLine(line, socket);
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
    }

    bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        // Look for a EOL in the buffer.
        SequencePosition? position = buffer.PositionOf((byte)'\n');

        if (position == null)
        {
            line = default;
            return false;
        }

        // Skip the line + the \n.
        line = buffer.Slice(0, position.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }

    static void ProcessLine(ReadOnlySequence<byte> line, Socket socket)
    {
        var message = Encoding.UTF8.GetString(line.ToArray());
        Console.WriteLine($"Received: {message}");

        // Echo the message back to the client
        byte[] responseBytes = Encoding.UTF8.GetBytes(message);

        socket.Send(responseBytes, SocketFlags.None);
    }
}
