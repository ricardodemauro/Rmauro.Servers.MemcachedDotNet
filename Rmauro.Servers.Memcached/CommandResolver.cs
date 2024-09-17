namespace Rmauro.Servers.Memcached;

public class CommandResolver : ICommandResolver
{
    const char cr = '\r';

    const char lf = '\n';

    const string crlf = "\r\n";

    const char space = ' ';

    public string[] CommandArgs(string command)
    {
        ReadOnlySpan<char> span = command.AsSpan();

        var lineIdx = span.IndexOf(lf);

        // Parse the command name
        var nextIdx = span.IndexOf(space);
        ReadOnlySpan<char> commandName = nextIdx > -1 ? span[..nextIdx] : span[..lineIdx];

        switch (commandName.ToString())
        {
            case Commands.Add:
            case Commands.Set:
                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(space);
                ReadOnlySpan<char> key = span[..nextIdx];

                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(space);
                ReadOnlySpan<char> flags = span[..nextIdx];

                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(space);
                ReadOnlySpan<char> expiration = span[..nextIdx];

                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(cr);
                ReadOnlySpan<char> bytes = span[..nextIdx];

                span = span[(nextIdx + 2)..];
                nextIdx = span.IndexOf(cr);
                ReadOnlySpan<char> data = span[..(nextIdx > -1 ? nextIdx : span.Length)];

                return
                [
                    commandName.ToString(),
                    key.ToString(),
                    flags.ToString(),
                    expiration.ToString(),
                    bytes.ToString(),
                    data.ToString()
                ];

            case Commands.Get:
                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(lf);
                ReadOnlySpan<char> key2 = span[..nextIdx];

                return [commandName.ToString(), key2.ToString()];

            case Commands.FlushAll:
                return [commandName.ToString()];


        }
        return [];
    }

    public string[] CommandArgs(ReadOnlySpan<char> message)
    {
        ReadOnlySpan<char> span = message;

        var lineIdx = span.IndexOf(lf);

        // Parse the command name
        var nextIdx = span.IndexOf(space);
        ReadOnlySpan<char> commandName = nextIdx > -1 ? span[..nextIdx] : span[..lineIdx];

        switch (commandName.ToString())
        {
            case Commands.Add:
            case Commands.Set:
                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(space);
                ReadOnlySpan<char> key = span[..nextIdx];

                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(space);
                ReadOnlySpan<char> flags = span[..nextIdx];

                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(space);
                ReadOnlySpan<char> expiration = span[..nextIdx];

                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(cr);
                ReadOnlySpan<char> bytes = span[..nextIdx];

                span = span[(nextIdx + 2)..];
                nextIdx = span.IndexOf(cr);
                ReadOnlySpan<char> data = span[..(nextIdx > -1 ? nextIdx : span.Length)];

                return
                [
                    commandName.ToString(),
                    key.ToString(),
                    flags.ToString(),
                    expiration.ToString(),
                    bytes.ToString(),
                    data.ToString()
                ];

            case Commands.Get:
                span = span[(nextIdx + 1)..];
                nextIdx = span.IndexOf(lf);
                ReadOnlySpan<char> key2 = span[..nextIdx];

                return [commandName.ToString(), key2.ToString()];

            case Commands.FlushAll:
                return [commandName.ToString()];


        }
        return [];
    }
}
