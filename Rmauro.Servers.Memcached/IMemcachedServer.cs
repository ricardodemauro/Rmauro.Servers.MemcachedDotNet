namespace Rmauro.Servers.Memcached;

public interface IMemcachedServer
{
    string ProcessMessage(ReadOnlySpan<char> message);
}
