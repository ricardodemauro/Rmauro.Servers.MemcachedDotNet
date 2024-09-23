using Rmauro.Servers.Memcached.Listeners;
using Rmauro.Servers.Memcached.Servers;
using Serilog;
using Serilog.Sinks.FastConsole;

Log.Logger = new LoggerConfiguration()
    //.WriteTo.Console()
    .WriteTo.FastConsole()
    .MinimumLevel.Debug()
    .CreateLogger();

Console.WriteLine("Starting at 8888 with max connections of 1024");

var server = MemcachedServer.CreateBuilder(args)
    //.UseTcpClientListener(8888)
    .UseIOCPSocketListener(8888, 1024)
    .Build();

await server.Start(CancellationToken.None);