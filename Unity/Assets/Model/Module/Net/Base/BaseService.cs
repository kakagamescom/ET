using System;
using System.IO;
using System.Net;

namespace ET
{
    /// <summary>
    /// 服务抽象基类
    /// </summary>
    public abstract class BaseService: IDisposable
    {
        public ServiceType ServiceType { get; protected set; }
        
        public ThreadSyncContext ThreadSyncContext;
        
        // localConn放在低32bit
        private long _connectIdGenerater = int.MaxValue;
        public long CreateConnectChannelId(uint localConn)
        {
            return (--_connectIdGenerater << 32) | localConn;
        }
        
        public uint CreateRandomLocalConn()
        {
            return (1u << 30) | RandomHelper.RandUInt32();
        }

        // localConn放在低32bit
        private long _acceptIdGenerater = 1;
        public long CreateAcceptChannelId(uint localConn)
        {
            return (++_acceptIdGenerater << 32) | localConn;
        }

        
        public abstract void Update();

        public abstract void Remove(long id);
        
        public abstract bool IsDispose();

        protected abstract void Get(long id, IPEndPoint address);

        public abstract void Dispose();

        protected abstract void Send(long channelId, long actorId, MemoryStream stream);
        
        protected void OnAccept(long channelId, IPEndPoint ipEndPoint)
        {
            AcceptCallback.Invoke(channelId, ipEndPoint);
        }

        public void OnRead(long channelId, MemoryStream memoryStream)
        {
            ReadCallback.Invoke(channelId, memoryStream);
        }

        public void OnError(long channelId, int e)
        {
            Remove(channelId);
            
            ErrorCallback?.Invoke(channelId, e);
        }

        
        public Action<long, IPEndPoint> AcceptCallback;
        public Action<long, int> ErrorCallback;
        public Action<long, MemoryStream> ReadCallback;

        public void Destroy()
        {
            Dispose();
        }

        public void RemoveChannel(long channelId)
        {
            Remove(channelId);
        }

        public void SendStream(long channelId, long actorId, MemoryStream stream)
        {
            Send(channelId, actorId, stream);
        }

        public void GetOrCreate(long id, IPEndPoint address)
        {
            Get(id, address);
        }
    }
}