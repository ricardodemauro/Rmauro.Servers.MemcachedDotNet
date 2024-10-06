using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rmauro.Servers.Memcached.Servers.Commands
{
    public ref struct SpansCommandResolver
    {
        Span<byte> cr;

        Span<byte> lf;

        Span<byte> crlf;

        Span<byte> space;

        Span<byte> addCmd;

        Span<byte> setCmd;

        Span<byte> getCmd;

        Span<byte> flushCmd;

        public SpansCommandResolver()
        {
            cr = new Span<byte>([(byte)'\r']);

            lf = new Span<byte>(['\n'.AsByte()]);

            crlf = "\r\n".AsBytes();

            space = new Span<byte>([' '.AsByte()]);

            addCmd = "add".AsBytes();

            setCmd = "set".AsBytes();

            getCmd = "get".AsBytes();

            flushCmd = "flush_all".AsBytes();
        }

        public Command CommandArgs(ReadOnlySpan<byte> rawBytes)
        {
            var span = rawBytes;
            var lineIdx = span.IndexOf(lf);

            // Parse the command name
            var nextIdx = span.IndexOf(space);
            ReadOnlySpan<byte> commandName = nextIdx > -1 ? span[..nextIdx] : span[..lineIdx];

            if (addCmd.SequenceEqual(commandName))
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
            if (setCmd.SequenceEqual(commandName))
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
            if (getCmd.SequenceEqual(commandName))
            {
                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(lf);
                ReadOnlySpan<byte> key2 = span[..nextIdx];

                return new Command(CommandType.Get, key2, null);
            }
            if (flushCmd.SequenceEqual(commandName))
            {
                return new Command(CommandType.FlushAll, null, null);
            }
            return new Command(CommandType.Unknow, null, null);
        }
    }
}
