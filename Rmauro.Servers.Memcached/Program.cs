using Rmauro.Servers.Memcached;
using Rmauro.Servers.Memcached.Listeners;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Warning()
    .CreateLogger();

Console.WriteLine("Starting at 8888");

var server = SupaServer.CreateBuilder(args)
    .UseTcpClientListener(8888)
    .Build();

await server.Start(CancellationToken.None);