using System.Runtime.CompilerServices;
using System.Text;

namespace Rmauro.Servers.Memcached.Servers;

public static class BytesExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] AsBytes(this string raw)
        => Encoding.UTF8.GetBytes(raw);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte AsByte(this char c)
        => (byte)c;
}