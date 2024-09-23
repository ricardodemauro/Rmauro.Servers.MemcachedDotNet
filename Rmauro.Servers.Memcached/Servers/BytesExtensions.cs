using System.Text;

namespace Rmauro.Servers.Memcached.Servers;

public static class BytesExtensions
{
    public static byte[] AsBytes(this string raw)
        => Encoding.UTF8.GetBytes(raw);

    public static byte AsByte(this char c)
        => (byte)c;
}