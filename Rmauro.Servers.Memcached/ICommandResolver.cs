using System;

namespace Rmauro.Servers.Memcached;

public interface ICommandResolver
{
    string[] CommandArgs(string command);
}
