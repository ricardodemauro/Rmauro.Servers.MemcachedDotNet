namespace Rmauro.Servers.Memcached.Listeners;

public delegate Memory<byte> ProcessRequestDelegate(in ReadOnlySpan<byte> data);

public interface ISocketListener
{
    Task Start(ProcessRequestDelegate process, CancellationToken cancellationToken);
}
