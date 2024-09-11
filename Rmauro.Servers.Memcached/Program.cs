// See https://aka.ms/new-console-template for more information
using System.Collections.Concurrent;
using System.Net.Sockets;
using Rmauro.Servers.Memcached;
using Serilog;


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Hello, World!");

var server = new MemcachedServerImp(8888);
await server.StartAsync();