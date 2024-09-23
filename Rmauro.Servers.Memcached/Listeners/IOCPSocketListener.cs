using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rmauro.Servers.Memcached.Listeners.Options;
using Serilog;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

//based on: https://medium.com/@Alikhalili/building-a-high-performance-tcp-server-from-scratch-a8ede35c4cc2
namespace Rmauro.Servers.Memcached.Listeners;

public class IOCPSocketListener(
    ILogger<TcpClientListener> logger,
    IOptions<ListenerOptions> options) : ISocketListener, IDisposable
{
    int connectedClients = 0;

    readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    ConcurrentStack<IOCPSocketConnection> _connections = new ConcurrentStack<IOCPSocketConnection>();

    public void Dispose()
    {
        logger.LogInformation("Disposing {CountClients} clients", connectedClients);
    }

    public Task Start(ProcessRequestDelegate process, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting server TCPConnectionResolver at {Port}", options.Value.EndPoint.Port);

        _socket.Bind(options.Value.EndPoint);
        _socket.Listen(options.Value.MaxConnections);

        while (cancellationToken.IsCancellationRequested == false)
        {
            connectedClients = connectedClients + 1;
            var acceptSocket = _socket.Accept();

            var client = new IOCPSocketConnection(acceptSocket, connectedClients, process, logger);
            _connections.Push(client);
            client.Start();
        }

        return Task.CompletedTask;
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

                if (response.Length == 0)
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

class IOCPSocketConnection
{
    readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(4096);

    readonly Socket _socketClient;

    readonly int _connectionId;

    ProcessRequestDelegate _processRequestDelegate;

    Microsoft.Extensions.Logging.ILogger _logger;

    int _bytesRead = 0;

    public IOCPSocketConnection(
        Socket socketClient,
        int connectionId,
        ProcessRequestDelegate processRequest,
        Microsoft.Extensions.Logging.ILogger logger
        )
    {
        _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
        _connectionId = connectionId;
        _processRequestDelegate = processRequest ?? throw new ArgumentNullException(nameof(processRequest));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
    }

    internal void Start()
    {
        var receiveArgs = new SocketAsyncEventArgs(unsafeSuppressExecutionContextFlow: true);
        receiveArgs.AcceptSocket = _socketClient;

        receiveArgs.SetBuffer(_buffer, 0, _buffer.Length);

        receiveArgs.Completed += RecvEventArg_Completed;

        if (!_socketClient.ReceiveAsync(receiveArgs))
        {
            Task.Run(() => RecvEventArg_Completed(null, receiveArgs));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadDone(SocketAsyncEventArgs e)
    {
        ArrayPool<byte>.Shared.Return(_buffer);
        _socketClient.Close();
        _socketClient.Dispose();
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetBuffer(SocketAsyncEventArgs e) => e.SetBuffer(_buffer, _bytesRead, _buffer.Length - _bytesRead);


    private void RecvEventArg_Completed(object sender, SocketAsyncEventArgs e)
    {
        do
        {
            if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
            {
                ReadDone(e);

                return;
            }

            SetBuffer(e);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Get Request {Request}", Encoding.UTF8.GetString(_buffer.AsSpan().Slice(0, e.BytesTransferred)));

            var response = _processRequestDelegate(_buffer.AsSpan().Slice(0, e.BytesTransferred));

            if (_logger.IsEnabled(LogLevel.Debug))
                Log.Debug("Sending back response {Response}", Encoding.UTF8.GetString(response.ToArray()));

            _socketClient.Send(response.Span);

        } while (!e.AcceptSocket!.ReceiveAsync(e));
    }
}


