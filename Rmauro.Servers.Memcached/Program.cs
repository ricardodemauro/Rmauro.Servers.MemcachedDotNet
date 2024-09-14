using Rmauro.Servers.Memcached;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Warning()
    .CreateLogger();

Console.WriteLine("Starting at 8888");

var server = new MemcachedServerImp(8888);
await server.StartAsync();