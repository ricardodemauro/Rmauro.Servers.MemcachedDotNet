namespace Rmauro.Servers.Memcached;

public interface IMemcachedServer
{
    string? ProcessMessage(string message);
}
