using Microsoft.Extensions.ObjectPool;
using System.Net.Sockets;

namespace Rmauro.Servers.Memcached.Pools;

class SocketPoolPolicy : IPooledObjectPolicy<SocketAsyncEventArgs>
{
    public SocketAsyncEventArgs Create()
    {
        return new SocketAsyncEventArgs()
        {
            DisconnectReuseSocket = true
        };
    }

    public bool Return(SocketAsyncEventArgs obj)
    {
        obj.ConnectSocket?.Disconnect(true);
        return true;
    }
}

public static class SocketAsyncEventArgsPool
{
    static readonly ObjectPool<SocketAsyncEventArgs> _pool = new DefaultObjectPool<SocketAsyncEventArgs>(new SocketPoolPolicy(), 1024);


    public static SocketAsyncEventArgs Get() => _pool.Get();

    public static void Return(SocketAsyncEventArgs obj) => _pool.Return(obj);
}
