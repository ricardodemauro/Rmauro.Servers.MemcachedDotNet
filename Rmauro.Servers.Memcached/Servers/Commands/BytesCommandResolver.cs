using System;
using System.Text;

namespace Rmauro.Servers.Memcached.Servers.Commands;

public class BytesCommandResolver : ICommandParser
{
    readonly byte cr = '\r'.AsByte();

    readonly byte lf = '\n'.AsByte();

    readonly byte[] crlf = "\r\n".AsBytes();

    readonly byte space = ' '.AsByte();

    readonly byte[] addCmd = "add".AsBytes();

    readonly byte[] setCmd = "set".AsBytes();

    readonly byte[] getCmd = "get".AsBytes();

    readonly byte[] flushCmd = "flush_all".AsBytes();

    public Command CommandArgs(ReadOnlySpan<byte> rawBytes)
    {
        var span = rawBytes;
        var lineIdx = span.IndexOf(lf);

        // Parse the command name
        var nextIdx = span.IndexOf(space);
        ReadOnlySpan<byte> commandName = nextIdx > -1 ? span[..nextIdx] : span[..lineIdx];

        if (addCmd.AsSpan().SequenceEqual(commandName))
        {
            span = span[(nextIdx + 1)..];
            nextIdx = span.IndexOf(space);
            ReadOnlySpan<byte> key = span[..nextIdx];

            span = span[(nextIdx + 1)..];
            nextIdx = span.IndexOf(space);
            //ReadOnlySpan<byte> flags = span[..nextIdx];

            span = span[(nextIdx + 1)..];
            nextIdx = span.IndexOf(space);
            //ReadOnlySpan<byte> expiration = span[..nextIdx];

            span = span[(nextIdx + 1)..];
            nextIdx = span.IndexOf(cr);
            //ReadOnlySpan<byte> bytes = span[..nextIdx];

            span = span[(nextIdx + 2)..];
            nextIdx = span.IndexOf(cr);
            ReadOnlySpan<byte> data = span[..(nextIdx > -1 ? nextIdx : span.Length)];

            return new Command(CommandType.Add, key, data);
        }
        if (setCmd.AsSpan().SequenceEqual(commandName))
        {
            span = span[(nextIdx + 1)..];
            nextIdx = span.IndexOf(space);
            ReadOnlySpan<byte> key = span[..nextIdx];

            span = span[(nextIdx + 1)..];
            nextIdx = span.IndexOf(space);
            //ReadOnlySpan<byte> flags = span[..nextIdx];

            span = span[(nextIdx + 1)..];
            nextIdx = span.IndexOf(space);
            //ReadOnlySpan<byte> expiration = span[..nextIdx];

            span = span[(nextIdx + 1)..];
            nextIdx = span.IndexOf(cr);
            //ReadOnlySpan<byte> bytes = span[..nextIdx];

            span = span[(nextIdx + 2)..];
            nextIdx = span.IndexOf(cr);
            ReadOnlySpan<byte> data = span[..(nextIdx > -1 ? nextIdx : span.Length)];

            return new Command(CommandType.Set, key, data);
        }
        if (getCmd.AsSpan().SequenceEqual(commandName))
        {
            span = span[(nextIdx + 1)..];
            nextIdx = span.IndexOf(lf);
            ReadOnlySpan<byte> key2 = span[..nextIdx];

            return new Command(CommandType.Get, key2, null);
        }
        if (flushCmd.AsSpan().SequenceEqual(commandName))
        {
            return new Command(CommandType.FlushAll, null, null);
        }
        return new Command(CommandType.Unknow, null, null);
    }
}
