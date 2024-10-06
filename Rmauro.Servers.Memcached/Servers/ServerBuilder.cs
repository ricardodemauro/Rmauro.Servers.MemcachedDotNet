using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rmauro.Servers.Memcached.Listeners;
using Rmauro.Servers.Memcached.Servers.Commands;
using Serilog;

namespace Rmauro.Servers.Memcached.Servers;

public interface IServerBuilder
{
    IServerBuilder ConfigureServices(Action<IServiceCollection> configureServices);

    MemcachedServer Build();
}

public class ServerBuilder : IServerBuilder
{
    public ServerBuilder()
    {
        Services = new ServiceCollection();
    }

    public IServiceCollection Services { get; private set; }

    public IServerBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        //configureServices.Invoke(Services);
        configureServices(Services);
        return this;
    }

    public MemcachedServer Build()
    {
        Services.AddLogging().AddSerilog(Log.Logger, true);

        Services.AddSingleton<ICommandParser, BytesCommandResolver>();

        var sp = ((ServiceCollection)Services).BuildServiceProvider();

        return new MemcachedServer(
            sp.GetRequiredService<ISocketListener>(),
            sp.GetRequiredService<ICommandParser>(),
            sp.GetService<ILogger<MemcachedServer>>());
    }
}