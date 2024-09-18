using NetUV.Core.Buffers;
using NetUV.Core.Handles;
using Serilog;
using System.Net;
using System.Text;

namespace Rmauro.Servers.Memcached.Connections
{
    public class LibuvConnectionResolver(int port, IMemcachedServer server) : IConnectionResolver
    {
        int _connectedClients = 0;

        readonly int _maxClients = 250;

        readonly int _port = port;

        readonly Loop _loop = new Loop();

        Tcp server;

        readonly IMemcachedServer _server = server;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Log.Warning("Starting server TCPConnectionResolver at {Port}", _port);

            await Listen(cancellationToken);
        }

        async Task Listen(CancellationToken cancellationToken)
        {
            Log.Information("Starting to listen on port {Port}. Waiting for connections", _port);

            server = _loop.CreateTcp()
                .SimultaneousAccepts(true)
                .Listen(new IPEndPoint(IPAddress.Any, _port), this.OnConnection);

            _loop.RunDefault();

            await Task.Delay(30 * 1000);
        }

        void OnConnection(Tcp client, Exception error)
        {
            if (error != null)
            {
                Console.WriteLine($"{nameof(LibuvConnectionResolver)} client connection failed {error}");
                client.CloseHandle(OnClosed);
                return;
            }
            client.OnRead(this.OnAccept, OnError);
        }

        void OnAccept(Tcp client, ReadableBuffer data)
        {
            string message = data.ReadString(Encoding.UTF8);
            if (string.IsNullOrEmpty(message))
            {

                return;
            }


            Log.Debug($"{nameof(LibuvConnectionResolver)} received : {message}");

            var response = _server.ProcessMessage(message.AsSpan());


            Log.Debug($"{nameof(LibuvConnectionResolver)} sending echo back.");

            WritableBuffer buffer = client.Allocate();
            buffer.WriteString(response, Encoding.UTF8);

            client.QueueWriteStream(buffer, (handle, exception) =>
            {
                buffer.Dispose();
                //handle.CloseHandle(OnClosed);
            });

        }

        static void OnError(Tcp handle, Exception error)
            => Console.WriteLine($"{nameof(LibuvConnectionResolver)} read error {error}");

        static void OnClosed(StreamHandle handle) => handle.Dispose();

        public void Dispose() => this.server.Dispose();

    }
}
