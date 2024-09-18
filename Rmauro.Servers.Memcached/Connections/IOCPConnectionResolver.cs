using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Rmauro.Servers.Memcached.Connections;

public class IOCPConnectionResolver(int port, IMemcachedServer server) : IConnectionResolver
{
    volatile int _connectedClients = 0;

    readonly int _port = port;

    readonly IMemcachedServer _server = server;

    readonly int _maxClients = 500;


    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Log.Warning("Starting server IOCPConnectionResolver at {Port}", _port);

        await Listen(cancellationToken);
    }

    Task Listen(CancellationToken cancellationToken)
    {
        Socket listener = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp);

        // Bind the socket to a local endpoint and listen for incoming connections
        listener.Bind(new IPEndPoint(IPAddress.Any, _port));
        listener.Listen(_maxClients);

        Log.Information("Starting to listen on port {Port}. Waiting for connections", _port);

        // Set up an event loop to handle connections
        for (int i = 0; i < _maxClients; i++)
        {
            var eventArgs = new SocketAsyncEventArgs();
            eventArgs.Completed += OnAcceptCompleted;
            listener.AcceptAsync(eventArgs); // Start accepting connections asynchronously
        }

        cancellationToken.WaitHandle.WaitOne();

        return Task.CompletedTask;
    }

    void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success)
        {
            _connectedClients = _connectedClients + 1;

            Log.Debug($"Client connected. Total clients: {_connectedClients}");

            // Set up the buffer for receiving data
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
            e.SetBuffer(buffer, 0, buffer.Length);

            // Set up an event to handle incoming data from the client
            e.Completed += OnReceiveCompleted;
            e?.AcceptSocket?.ReceiveAsync(e); // Begin receiving data from the client

            ArrayPool<byte>.Shared.Return(buffer);
        }
        else
        {
            Log.Error("Accept failed.");
            e.Dispose();
        }
    }

    void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
        {
            // Handle the received data
            string message = Encoding.UTF8.GetString(e?.Buffer, 0, e.BytesTransferred);

            Log.Debug("Received: {message}", message);

            var response = _server.ProcessMessage(message);

            var byteResponse = Encoding.UTF8.GetBytes(response);

            e.SetBuffer(byteResponse, 0, byteResponse.Length);
            e.AcceptSocket?.SendAsync(e); // Send response back asynchronously
        }
        else
        {
            Log.Error("Receive failed.");
            e.AcceptSocket?.Shutdown(SocketShutdown.Both);
            e.AcceptSocket?.Close();
            e.Dispose();
        }
    }
}
