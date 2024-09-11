namespace Rmauro.Servers.Memcached;

internal static class CommandResolver
{
    const char cr = '\r';

    const char lf = '\n';

    const string crlf = "\r\n";

    internal static string Command(ref string rawCommand)
    {
        var span = rawCommand.AsSpan();
        var idxCmd = span.IndexOf(' ');

        string cmd = new(span[..(idxCmd)]);
        return cmd;
    }

    internal static string GetArgument(ref string rawCommand)
    {
        var span = rawCommand.AsSpan();
        var idxCmd = span.IndexOf(' ');

        string cmd = new(span[..(idxCmd - 1)]);
        return cmd;
    }

    internal static string[] Resolve(string rawCommand)
    {
        var span = rawCommand.AsSpan();

        var idx = span.IndexOf(cr);
        if (idx == -1) throw new FormatException("Invalid message format");

        if (idx == rawCommand.Length) return [rawCommand];

        string arg1 = new(span.Slice(0, idx - 1));
        string arg2 = new(span.Slice(idx, span.Length - 1));

        return [arg1, arg2];
    }
}
