namespace Rmauro.Servers.Memcached.Servers.Commands;

public interface ICommandParser
{
    Command CommandArgs(ReadOnlySpan<byte> rawBytes);
}
