using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Rmauro.Servers.Memcached.Connections;

public class SemaphoneTCPConnectionResolver(int port, IMemcachedServer server) : IConnectionResolver
{

    const int MAX_CONNECTIONS = 250;

    SemaphoreSlim _maxConnectionsSemaphore = new(MAX_CONNECTIONS, MAX_CONNECTIONS);

    readonly int _port = port;

    readonly IMemcachedServer _server = server;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Log.Warning("Starting server SemaphoneTCPConnectionResolver at {Port}", _port);

        var _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
        _listenerSocket.Listen(100);

        _listenerSocket.NoDelay = true;

        Log.Information("Server started, listening for connections...");

        // Start accepting connections asynchronously
        while (cancellationToken.IsCancellationRequested == false)
        {
            // await AcceptConnectionAsync(_listenerSocket, cancellationToken);
            var socket = await _listenerSocket.AcceptAsync(cancellationToken);

            Log.Debug($"Connection accepted from {_listenerSocket.RemoteEndPoint}");

            // Acquire a semaphore slot to ensure we don't exceed the max connections
            await _maxConnectionsSemaphore.WaitAsync();

            // Handle the client connection asynchronously
            _ = HandleClientAsync(socket, cancellationToken);

        }
    }

    Task<Socket> AcceptConnectionAsync(Socket socket, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<Socket>();
        var acceptEventArgs = new SocketAsyncEventArgs();

        acceptEventArgs.Completed += (s, e) =>
        {
            if (e.SocketError == SocketError.Success)
            {
                tcs.SetResult(e.AcceptSocket);
            }
            else
            {
                tcs.SetException(new SocketException((int)e.SocketError));
            }
        };

        bool willRaiseEvent = socket.AcceptAsync(acceptEventArgs);
        if (!willRaiseEvent)
        {
            // If no pending I/O, complete the task immediately
            tcs.SetResult(acceptEventArgs.AcceptSocket);
        }

        return tcs.Task;
    }

    Task<int> ReceiveAsync(Socket socket, byte[] buffer)
    {
        var tcs = new TaskCompletionSource<int>();
        var receiveEventArgs = new SocketAsyncEventArgs();
        receiveEventArgs.SetBuffer(buffer, 0, buffer.Length);
        receiveEventArgs.UserToken = socket;

        receiveEventArgs.Completed += (s, e) =>
        {
            if (e.SocketError == SocketError.Success)
            {
                if (e.BytesTransferred > 0)
                {
                    tcs.SetResult(e.BytesTransferred);
                }
                else
                {
                    // Zero bytes received means the client has closed the connection
                    tcs.SetResult(0);
                }
            }
            else
            {
                tcs.SetException(new SocketException((int)e.SocketError));
            }
        };

        bool willRaiseEvent = socket.ReceiveAsync(receiveEventArgs);
        if (!willRaiseEvent)
        {
            tcs.SetResult(receiveEventArgs.BytesTransferred);
        }

        return tcs.Task;
    }

    Task<int> SendAsync(Socket socket, byte[] buffer)
    {
        var tcs = new TaskCompletionSource<int>();
        var sendEventArgs = new SocketAsyncEventArgs();
        sendEventArgs.SetBuffer(buffer, 0, buffer.Length);
        sendEventArgs.UserToken = socket;

        sendEventArgs.Completed += (s, e) =>
        {
            if (e.SocketError == SocketError.Success)
            {
                tcs.SetResult(e.BytesTransferred);
            }
            else
            {
                tcs.SetException(new SocketException((int)e.SocketError));
            }
        };
        sendEventArgs.AcceptSocket = socket;

        bool willRaiseEvent = socket.SendAsync(sendEventArgs);
        if (!willRaiseEvent)
        {
            tcs.SetResult(sendEventArgs.BytesTransferred);
        }

        return tcs.Task;
    }

    async Task HandleClientAsync(Socket clientSocket, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            while (cancellationToken.IsCancellationRequested == false)
            {

                // Receive data
                int bytesReceived = await ReceiveAsync(clientSocket, buffer);
                //int bytesReceived = await clientSocket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                if (bytesReceived == 0)
                {
                    // Connection closed
                    break;
                }

                string receivedText = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                Log.Debug($"Received: {receivedText}");

                var response = _server.ProcessMessage(receivedText);

                // Echo the data back to the client
                byte[] byteData = Encoding.UTF8.GetBytes(response);
                await SendAsync(clientSocket, byteData);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error handling client: {ex.Message}");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            //clientSocket.Close();
            Log.Debug("Client disconnected");
        }
    }

}
