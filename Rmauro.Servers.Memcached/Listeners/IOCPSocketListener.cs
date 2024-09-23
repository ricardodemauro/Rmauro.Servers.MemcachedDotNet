using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Rmauro.Servers.Memcached.Listeners.Options;
using System.Buffers;
using System.Net.Sockets;
using System.Text;

//based on: https://medium.com/@Alikhalili/building-a-high-performance-tcp-server-from-scratch-a8ede35c4cc2
namespace Rmauro.Servers.Memcached.Listeners;

public class SocketAsyncEventArgsPolicy : IPooledObjectPolicy<SocketAsyncEventArgs>
{
    public SocketAsyncEventArgs Create() => new SocketAsyncEventArgs(unsafeSuppressExecutionContextFlow: true);

    public bool Return(SocketAsyncEventArgs obj)
    {
        obj.AcceptSocket = null;
        return true;
    }
}

public static class SocketAsyncEventArgsPool
{
    static readonly ObjectPool<SocketAsyncEventArgs> _provider;

    static SocketAsyncEventArgsPool()
    {
        var defaultProvider = new DefaultObjectPoolProvider
        {
            MaximumRetained = 4096
        };

        _provider = defaultProvider.Create(new SocketAsyncEventArgsPolicy());
    }

    public static SocketAsyncEventArgs Rent() => _provider.Get();

    public static void Return(SocketAsyncEventArgs obj) => _provider.Return(obj);
}

public class IOCPSocketListener(
    ILogger<TcpClientListener> logger,
    IOptions<ListenerOptions> options) : ISocketListener, IDisposable
{
    int connectedClients = 0;

    readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    List<IOCPSocketConnection> _connections = new();

    public void Dispose()
    {
        logger.LogInformation("Disposing {CountClients} clients", connectedClients);
        _connections.ForEach(x => x.Dispose());
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
            acceptSocket.NoDelay = true;

            var client = new IOCPSocketConnection(acceptSocket, connectedClients, process, logger);
            _connections.Add(client);
            client.Start();
        }

        return Task.CompletedTask;
    }
}

class IOCPSocketConnection(Socket socketClient, int connectionId, ProcessRequestDelegate processRequest, ILogger logger) : IDisposable
{
    readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(4096);

    readonly Socket _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));

    readonly int _connectionId = connectionId;

    readonly ProcessRequestDelegate _processRequestDelegate = processRequest ?? throw new ArgumentNullException(nameof(processRequest));

    readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public void Dispose()
    {
        _socketClient?.Close();
        _socketClient?.Dispose();
        ArrayPool<byte>.Shared.Return(_buffer);
    }

    internal void Start()
    {
        var receiveArgs = SocketAsyncEventArgsPool.Rent();
        receiveArgs.AcceptSocket = _socketClient;

        // var receiveArgs = new SocketAsyncEventArgs(unsafeSuppressExecutionContextFlow: true)
        // {
        //     AcceptSocket = _socketClient
        // };

        receiveArgs.SetBuffer(_buffer, 0, _buffer.Length);

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
                e.AcceptSocket.Close();
                return;
            }

            e.SetBuffer(_buffer.AsMemory());
            //SetBuffer(e);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Get Request {Request}", Encoding.UTF8.GetString(_buffer.AsSpan().Slice(0, e.BytesTransferred)));

            var response = _processRequestDelegate(_buffer.AsSpan().Slice(0, e.BytesTransferred));

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Sending back response {Response}", Encoding.UTF8.GetString(response.ToArray()));

            _socketClient.Send(response.Span);

        } while (!e.AcceptSocket!.ReceiveAsync(e));
    }
}
