using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

namespace ET
{
    [ObjectSystem]
    public class NetInnerComponentAwakeSystem: AwakeSystem<NetInnerComponent>
    {
        public override void Awake(NetInnerComponent self)
        {
            NetInnerComponent.Instance = self;
            self.MessageDispatcher = new InnerMessageDispatcher();
            
            self.NetService = new TcpService(NetThreadComponent.Instance.ThreadSyncContext, NetServiceType.Inner);
            self.NetService.ErrorCallback += self.OnError;
            self.NetService.ReadCallback += self.OnRead;

            NetThreadComponent.Instance.Add(self.NetService);
        }
    }

    [ObjectSystem]
    public class NetInnerComponentAwake1System: AwakeSystem<NetInnerComponent, IPEndPoint>
    {
        public override void Awake(NetInnerComponent self, IPEndPoint address)
        {
            NetInnerComponent.Instance = self;
            self.MessageDispatcher = new InnerMessageDispatcher();

            self.NetService = new TcpService(NetThreadComponent.Instance.ThreadSyncContext, address, NetServiceType.Inner);
            self.NetService.ErrorCallback += self.OnError;
            self.NetService.ReadCallback += self.OnRead;
            self.NetService.AcceptCallback += self.OnAccept;

            NetThreadComponent.Instance.Add(self.NetService);
        }
    }

    [ObjectSystem]
    public class NetInnerComponentLoadSystem: LoadSystem<NetInnerComponent>
    {
        public override void Load(NetInnerComponent self)
        {
            self.MessageDispatcher = new InnerMessageDispatcher();
        }
    }

    [ObjectSystem]
    public class NetInnerComponentDestroySystem: DestroySystem<NetInnerComponent>
    {
        public override void Destroy(NetInnerComponent self)
        {
            NetThreadComponent.Instance.Remove(self.NetService);
            self.NetService.Destroy();
        }
    }

    public static class NetInnerComponentSystem
    {
        public static void OnRead(this NetInnerComponent self, long channelId, MemoryStream memoryStream)
        {
            Session session = self.GetChild<Session>(channelId);
            if (session == null)
            {
                return;
            }

            session.LastRecvTime = TimeHelper.ClientNow();
            self.MessageDispatcher.Dispatch(session, memoryStream);
        }

        public static void OnError(this NetInnerComponent self, long channelId, int error)
        {
            Session session = self.GetChild<Session>(channelId);
            if (session == null)
            {
                return;
            }

            session.Error = error;
            session.Dispose();
        }

        // 这个channelId是由CreateAcceptChannelId生成的
        public static void OnAccept(this NetInnerComponent self, long channelId, IPEndPoint ipEndPoint)
        {
            Session session = EntityFactory.CreateWithParentAndId<Session, NetService>(self, channelId, self.NetService);
            session.RemoteAddress = ipEndPoint;
            //session.AddComponent<SessionIdleCheckerComponent, int, int, int>(NetThreadComponent.checkInteral, NetThreadComponent.recvMaxIdleTime, NetThreadComponent.sendMaxIdleTime);
        }

        // 这个channelId是由CreateConnectChannelId生成的
        public static Session Create(this NetInnerComponent self, IPEndPoint ipEndPoint)
        {
            uint localConn = self.NetService.CreateRandomLocalConn();
            long channelId = self.NetService.CreateConnectChannelId(localConn);
            Session session = self.CreateInner(channelId, ipEndPoint);
            return session;
        }

        private static Session CreateInner(this NetInnerComponent self, long channelId, IPEndPoint ipEndPoint)
        {
            Session session = EntityFactory.CreateWithParentAndId<Session, NetService>(self, channelId, self.NetService);

            session.RemoteAddress = ipEndPoint;

            self.NetService.GetOrCreate(channelId, ipEndPoint);

            //session.AddComponent<InnerPingComponent>();
            //session.AddComponent<SessionIdleCheckerComponent, int, int, int>(NetThreadComponent.checkInteral, NetThreadComponent.recvMaxIdleTime, NetThreadComponent.sendMaxIdleTime);

            return session;
        }

        // 内网actor session，channelId是进程号
        public static Session Get(this NetInnerComponent self, long channelId)
        {
            Session session = self.GetChild<Session>(channelId);
            if (session == null)
            {
                IPEndPoint ipEndPoint = StartProcessConfigCategory.Instance.Get((int) channelId).InnerIPPort;
                session = self.CreateInner(channelId, ipEndPoint);
            }

            return session;
        }
    }
}