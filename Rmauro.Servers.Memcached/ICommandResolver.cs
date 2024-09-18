using System;

namespace Rmauro.Servers.Memcached;

public interface ICommandResolver
{
    (string, string, string) CommandArgs(string command);

    (string, string, string) CommandArgs(ReadOnlySpan<char> command);
}
