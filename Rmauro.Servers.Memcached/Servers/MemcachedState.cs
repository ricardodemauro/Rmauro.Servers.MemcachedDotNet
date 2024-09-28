using System.Collections;
using System.Collections.Concurrent;

namespace Rmauro.Servers.Memcached.Servers;

//ref https://gist.github.com/manbeardgames/1d9b97278f71294b254e0b6672282dfd
public class ByteArrayComparer : IEqualityComparer<byte[]>
{
    private static ByteArrayComparer _default;

    public static ByteArrayComparer Default
    {
        get
        {
            _default ??= new ByteArrayComparer();
            return _default;
        }
    }

    public bool Equals(byte[] obj1, byte[] obj2)
    {
        return StructuralComparisons.StructuralEqualityComparer.Equals(obj1, obj2);
    }

    public int GetHashCode(byte[] obj)
    {
        return StructuralComparisons.StructuralEqualityComparer.GetHashCode(obj);
    }
}

public sealed class MemcachedState
{
    readonly NonBlocking.ConcurrentDictionary<byte[], byte[]> _state = new(Environment.ProcessorCount * 2, 20, ByteArrayComparer.Default);

    readonly byte[] messageStored = "STORED\r\n".AsBytes();

    readonly byte[] messageNotStored = "NOT_STORED\r\n".AsBytes();

    readonly byte[] messageOk = "OK\r\n".AsBytes();

    readonly byte[] messageEnd = "END\r\n".AsBytes();

    readonly byte[] messageEnd2 = "\nEND\r\n".AsBytes();


    public Memory<byte> Add(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        if (_state.TryAdd(key.ToArray(), data.ToArray()))
            return messageStored;

        return messageNotStored;
    }

    public Memory<byte> FlushAll()
    {
        _state.Clear();
        return messageOk;
    }

    public Memory<byte> Get(ReadOnlySpan<byte> key)
    {
        if (!_state.TryGetValue(key.ToArray(), out byte[] val))
            return messageEnd;

        ReadOnlySpan<byte> result = val.AsSpan().Merge(messageEnd2);
        return new Memory<byte>(result.ToArray());
    }

    public Memory<byte> Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        byte[] d = data.ToArray();
        _state.AddOrUpdate(key.ToArray(), _ => d, (_,_) => d);

        return messageStored;
    }
}
