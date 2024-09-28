using Rmauro.Servers.Memcached.Listeners;
using Rmauro.Servers.Memcached.Servers;
using Serilog;
using Serilog.Events;

var logLevel = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "Warning";
var maxConnections = Environment.GetEnvironmentVariable("SOCKET_MAX_CONNECTIONS") ?? "4096";
var useObjectPool = Environment.GetEnvironmentVariable("USE_OBJECT_POOL") ?? "false";
var socketListener = Environment.GetEnvironmentVariable("SOCKET_LISTENER") ?? "IOCPSocketListener";
var port = Environment.GetEnvironmentVariable("SOCKET_PORT") ?? "8888";

Console.WriteLine("LogLevel {0}", logLevel);
Console.WriteLine("Max Connections {0}", maxConnections);
Console.WriteLine("Use Object Pool {0}", useObjectPool);
Console.WriteLine("Socket Listener {0}", socketListener);
Console.WriteLine("Port {0}", port);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    //.WriteTo.FastConsole()
    .MinimumLevel.Is((LogEventLevel)Enum.Parse(typeof(LogEventLevel), logLevel))
    .CreateLogger();

Console.WriteLine("Starting at {0}", port);

var server = MemcachedServer.CreateBuilder(args)
    //.UseTcpClientListener(8888)
    .UseListener(int.Parse(port), int.Parse(maxConnections))
    .Build();

await server.Start(CancellationToken.None);