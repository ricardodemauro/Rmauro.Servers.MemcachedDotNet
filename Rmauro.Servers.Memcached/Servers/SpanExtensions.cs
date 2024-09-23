using System.Diagnostics;

namespace Rmauro.Servers.Memcached.Servers;

public static class SpanExtensions
{
    //https://stackoverflow.com/a/52123265
    //https://stackoverflow.com/a/51562522
    //https://github.com/dotnet/corefx/blob/master/src/Common/src/CoreLib/System/IO/Path.cs#L542-L562
    public static ReadOnlySpan<byte> Merge(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        Debug.Assert(first.Length > 0 && second.Length > 0, "should have dealt with empty paths");

        byte[] buffer = new byte[first.Length + second.Length];

        var span = new Span<byte>(buffer);

        first.CopyTo(span.Slice(0, first.Length));

        second.CopyTo(span.Slice(first.Length, second.Length));

        return buffer;
    }

    //https://stackoverflow.com/a/52123265
    //https://stackoverflow.com/a/51562522
    //https://github.com/dotnet/corefx/blob/master/src/Common/src/CoreLib/System/IO/Path.cs#L542-L562
    public static ReadOnlySpan<byte> Merge(this Span<byte> first, ReadOnlySpan<byte> second)
    {
        Debug.Assert(first.Length > 0 && second.Length > 0, "should have dealt with empty paths");

        byte[] buffer = new byte[first.Length + second.Length];

        var span = new Span<byte>(buffer);

        first.CopyTo(span.Slice(0, first.Length));

        second.CopyTo(span.Slice(first.Length, second.Length));

        return buffer;
    }
}
