using Microsoft.AspNetCore.Connections;
using System.Net.Sockets;
using System.Net;

namespace Rmauro.Servers.Memcached.WebServer
{
    public class EchoHandler : ConnectionHandler
    {
        private readonly ILogger<EchoHandler> _logger;

        public EchoHandler(ILogger<EchoHandler> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            _logger.LogInformation(connection.ConnectionId + " connected");

            while (true)
            {
                var result = await connection.Transport.Input.ReadAsync();
                var buffer = result.Buffer;

                foreach (var segment in buffer)
                {
                    await connection.Transport.Output.WriteAsync(segment);
                }

                if (result.IsCompleted)
                {
                    break;
                }

                connection.Transport.Input.AdvanceTo(buffer.End);
            }

            _logger.LogInformation(connection.ConnectionId + " disconnected");
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost
                .ConfigureKestrel(x =>
                {
                    x.Listen(IPAddress.Any, 8888, builder => builder.UseConnectionLogging().UseConnectionHandler<EchoHandler>());

                    x.ConfigureEndpointDefaults(endpoints =>
                    {
                        endpoints.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.None;
                    });

                    //x.ListenHandle(10022, builder => builder.UseConnectionLogging().UseConnectionHandler<EchoHandler>());
                });
            builder.WebHost.UseKestrel();

            var app = builder.Build();

            app.Run();
        }
    }
}
