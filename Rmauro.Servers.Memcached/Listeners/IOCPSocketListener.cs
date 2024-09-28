﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rmauro.Servers.Memcached.Listeners.Options;
using Rmauro.Servers.Memcached.Pools;
using Serilog;
using System.Buffers;
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

    List<IOCPSocketConnection> _connections = new(1024);

    public void Dispose()
    {
        logger.LogInformation("Disposing {CountClients} clients", connectedClients);

        _socket.Dispose();

        GC.SuppressFinalize(this);
    }

    public Task Start(ProcessRequestDelegate process, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting server IOCPSocketListener at {Port}", options.Value.EndPoint.Port);

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

    class IOCPSocketConnection(
        Socket socketClient,
        int connectionId,
        ProcessRequestDelegate processRequest,
        Microsoft.Extensions.Logging.ILogger logger,
        bool useObjectPool)
    {
        readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(4096);

        readonly Socket _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));

        readonly int _connectionId = connectionId;

        ProcessRequestDelegate _processRequestDelegate = processRequest ?? throw new ArgumentNullException(nameof(processRequest));

        Microsoft.Extensions.Logging.ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        int _bytesRead = 0;

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        internal void Start()
        {
            var receiveArgs = useObjectPool ? SocketAsyncEventArgsPool.Get() : new SocketAsyncEventArgs(unsafeSuppressExecutionContextFlow: true);
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

            if (useObjectPool) SocketAsyncEventArgsPool.Return(e);
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
}