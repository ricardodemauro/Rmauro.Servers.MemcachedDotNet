using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rmauro.Servers.Memcached.Listeners.Options;
using Rmauro.Servers.Memcached.Pools;
using System.Buffers;
using System.Net.Sockets;
using System.Text;

//based on: https://medium.com/@Alikhalili/building-a-high-performance-tcp-server-from-scratch-a8ede35c4cc2
//https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketasynceventargs?view=net-8.0
namespace Rmauro.Servers.Memcached.Listeners;

public class IOCP2SocketListener(
    ILogger<TcpClientListener> logger,
    IOptions<ListenerOptions> options) : ISocketListener, IDisposable
{
    int connectedClients = 0;

    readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    readonly List<IOCPSocketConnection> _connections = new(4096);

    #region Dispose

    bool _disposedValue;

    public void Dispose()
    {
        Dispose(true);

        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        logger.LogInformation("Disposing {CountClients} clients", connectedClients);

        if (!_disposedValue)
        {
            if (disposing)
            {
                logger.LogInformation("Disposing {CountClients} clients", connectedClients);
                _socket.Disconnect(false);
                _socket.Dispose();
            }

            _disposedValue = true;
        }
    }

    ~IOCP2SocketListener() => Dispose(false);

    #endregion Dispose

    public Task Start(ProcessRequestDelegate process, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting server IOCP2SocketListener at {Port}", options.Value.EndPoint.Port);

        _socket.Bind(options.Value.EndPoint);
        _socket.Listen(options.Value.MaxConnections);

        while (cancellationToken.IsCancellationRequested == false)
        {
            connectedClients = connectedClients + 1;
            var acceptSocket = _socket.Accept();
            acceptSocket.NoDelay = true;

            var client = new IOCPSocketConnection(acceptSocket, connectedClients, process, logger, options.Value.UseMemoryPool);
            _connections.Add(client);
            client.Start();
        }

        return Task.CompletedTask;
    }

    class IOCPSocketConnection(Socket socketClient, int connectionId, ProcessRequestDelegate processRequest, ILogger logger, bool useMemoryPool)
    {
        readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(4096);

        readonly Socket _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));

        readonly int _connectionId = connectionId;

        readonly ProcessRequestDelegate _processRequestDelegate = processRequest ?? throw new ArgumentNullException(nameof(processRequest));

        readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        internal void Start()
        {
            var receiveArgs = useMemoryPool ? SocketAsyncEventArgsPool.Get() : new SocketAsyncEventArgs(unsafeSuppressExecutionContextFlow: true);
            receiveArgs.AcceptSocket = _socketClient;

            receiveArgs.SetBuffer(_buffer.AsMemory());

            receiveArgs.Completed += RecvEventArg_Completed;

            if (!_socketClient.ReceiveAsync(receiveArgs))
            {
                Task.Run(() => RecvEventArg_Completed(null, receiveArgs));
            }
        }

        private void RecvEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            do
            {
                if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                    _socketClient.Shutdown(SocketShutdown.Both);
                    _socketClient.Close();

                    if (useMemoryPool) SocketAsyncEventArgsPool.Return(e);

                    GC.SuppressFinalize(true);

                    return;
                }

                //e.SetBuffer(_buffer, _bytesRead, _buffer.Length - _bytesRead);
                e.SetBuffer(_buffer.AsMemory());

                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("[{ConnectionId}]Get Request {Request}", _connectionId, Encoding.UTF8.GetString(_buffer.AsSpan().Slice(0, e.BytesTransferred)));

                var response = _processRequestDelegate(_buffer.AsSpan()[..e.BytesTransferred]);

                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("[{ConnectionId}]Sending back response {Response}", _connectionId, Encoding.UTF8.GetString(response.ToArray()));

                _socketClient.Send(response.Span);

            } while (!e.AcceptSocket!.ReceiveAsync(e));
        }
    }
}