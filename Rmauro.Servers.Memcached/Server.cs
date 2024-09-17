using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;
using System.Buffers;
using System.Net;

namespace Rmauro.Servers.Memcached;

public interface IServer
{
    Task Start(CancellationToken cancellationToken);
}

public class DefaultStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IHostingEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.Run((context) =>
        {
            return Task.CompletedTask;
        });
    }
}

class DefaultConnectionHandler : ConnectionHandler
{
    public override async Task OnConnectedAsync(ConnectionContext connection)
    {
        Log.Information(connection.ConnectionId + " connected");

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

        Log.Information(connection.ConnectionId + " disconnected");
    }
}

public class Server : IServer, Microsoft.AspNetCore.Hosting.IApplicationLifetime
{
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => CancellationToken.None;
    public CancellationToken ApplicationStopped => CancellationToken.None;

    public async Task Start(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddConsole();
        });

        var serviceProvider = services.BuildServiceProvider();

        var kestrelOptions = Options.Create(new KestrelServerOptions());
        var transportFactory = new SocketTransportFactory(
            Options.Create(new SocketTransportOptions()),
            this,
            NullLoggerFactory.Instance);

        var server = new KestrelServer(kestrelOptions, transportFactory, NullLoggerFactory.Instance);

        //server.StartAsync();
    }

    public void StopApplication()
    {

    }
}
