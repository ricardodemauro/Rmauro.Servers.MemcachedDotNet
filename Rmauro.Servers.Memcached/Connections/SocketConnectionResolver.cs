using Serilog;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Rmauro.Servers.Memcached.Connections
{
    public class SocketConnectionResolver(int port, IMemcachedServer server) : IConnectionResolver
    {
        int _connectedClients = 0;

        readonly int _maxClients = 250;

        readonly int _port = port;

        readonly IMemcachedServer _server = server;

        readonly ConcurrentBag<Socket> _bag = new ConcurrentBag<Socket>();

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Log.Warning("Starting server SocketConnectionResolver at {Port}", _port);

            await Listen(cancellationToken);
        }

        async Task Listen(CancellationToken cancellationToken)
        {
            Socket listener = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);


            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);

            if (listener.AddressFamily == AddressFamily.InterNetworkV6)
                listener.DualMode = true;


            //listener.DontFragment = true;
            //if(listener.ProtocolType)
            //listener.DualMode = true;
            //listener.ExclusiveAddressUse = true;
            listener.NoDelay = true;

            // Bind the socket to a local endpoint and listen for incoming connections
            listener.Bind(new IPEndPoint(IPAddress.Any, _port));
            listener.Listen();

            Log.Information("Starting to listen on port {Port}. Waiting for connections", _port);

            int acceptingConnections = 0;
            // Set up an event loop to handle connections
            while (acceptingConnections < _maxClients)
            {
                //var socketConnection = await listener.AcceptAsync(cancellationToken)
                //    .ConfigureAwait(false);
                var socketConnection = listener.Accept();

                socketConnection.NoDelay = true;
                socketConnection.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                _ = Task.Factory.StartNew(async () =>
                {
                    await ProcessClient(socketConnection, cancellationToken);

                }, cancellationToken, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Current);

                acceptingConnections++;
            }

            await Task.Delay(30 * 1000);
        }

        async Task ProcessClient(Socket client, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(4096);

            while (true)
            {
                var bytesRead = await client.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);

                if (bytesRead == 0)
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                    client.Dispose();
                    break;
                }

                string msg = Encoding.UTF8.GetString(buffer.AsSpan()[..bytesRead]);

                //Log.Debug("Got message {Payload}", msg);

                var response = _server.ProcessMessage(msg.AsSpan());

                var sequence = new ReadOnlySequence<char>(response.AsMemory());

                var responseBytes = Encoding.UTF8.GetBytes(sequence);
                //.AsMemory();

                await client.SendAsync(responseBytes.AsMemory(), cancellationToken);
            }
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
