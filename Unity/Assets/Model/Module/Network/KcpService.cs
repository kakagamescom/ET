using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ET
{
    public static class KcpProtocalType
    {
        public const byte SYN = 1;
        public const byte ACK = 2;
        public const byte FIN = 3;
        public const byte MSG = 4;
    }

    public enum ServiceType
    {
        Outer,
        Inner,
    }

    public sealed class KcpService: BaseService
    {
        // KService创建的时间
        private readonly long startTime;

        // 当前时间 - KService创建的时间, 线程安全
        public uint TimeNow
        {
            get
            {
                return (uint)(TimeHelper.ClientNow() - this.startTime);
            }
        }

        private Socket socket;

#region 回调方法

        static KcpService()
        {
            //Kcp.KcpSetLog(KcpLog);
            Kcp.KcpSetoutput(KcpOutput);
        }

        private static readonly byte[] logBuffer = new byte[1024];

#if ENABLE_IL2CPP
		[AOT.MonoPInvokeCallback(typeof(KcpOutput))]
#endif
        private static void KcpLog(IntPtr bytes, int len, IntPtr kcp, IntPtr user)
        {
            try
            {
                Marshal.Copy(bytes, logBuffer, 0, len);
                Log.Info(logBuffer.ToStr(0, len));
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

#if ENABLE_IL2CPP
		[AOT.MonoPInvokeCallback(typeof(KcpOutput))]
#endif
        private static int KcpOutput(IntPtr bytes, int len, IntPtr kcp, IntPtr user)
        {
            try
            {
                if (kcp == IntPtr.Zero)
                {
                    return 0;
                }

                if (!KcpChannel.KcpPtrChannels.TryGetValue(kcp, out KcpChannel kcpChannel))
                {
                    return 0;
                }

                kcpChannel.Output(bytes, len);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return len;
            }

            return len;
        }

#endregion

        public KcpService(ThreadSyncContext threadSyncContext, IPEndPoint ipEndPoint, ServiceType serviceType)
        {
            this.ServiceType = serviceType;
            this.ThreadSyncContext = threadSyncContext;
            this.startTime = TimeHelper.ClientNow();
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                this.socket.SendBufferSize = Kcp.OneM * 64;
                this.socket.ReceiveBufferSize = Kcp.OneM * 64;
            }

            this.socket.Bind(ipEndPoint);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const uint IOC_IN = 0x80000000;
                const uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                this.socket.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
            }
        }

        public KcpService(ThreadSyncContext threadSyncContext, ServiceType serviceType)
        {
            this.ServiceType = serviceType;
            this.ThreadSyncContext = threadSyncContext;
            this.startTime = TimeHelper.ClientNow();
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // 作为客户端不需要修改发送跟接收缓冲区大小
            this.socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const uint IOC_IN = 0x80000000;
                const uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                this.socket.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
            }
        }

        public void ChangeAddress(long id, IPEndPoint address)
        {
            KcpChannel kcpChannel = this.Get(id);
            if (kcpChannel == null)
            {
                return;
            }

            Log.Info($"channel change address: {id} {address}");
            kcpChannel.RemoteAddress = address;
        }

        // 保存所有的channel
        private readonly Dictionary<long, KcpChannel> idChannels = new Dictionary<long, KcpChannel>();
        private readonly Dictionary<long, KcpChannel> localConnChannels = new Dictionary<long, KcpChannel>();
        private readonly Dictionary<long, KcpChannel> waitConnectChannels = new Dictionary<long, KcpChannel>();

        private readonly byte[] cache = new byte[8192];
        private EndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 0);

        // 下帧要更新的channel
        private readonly HashSet<long> updateChannels = new HashSet<long>();

        // 下次时间更新的channel
        private readonly MultiMap<long, long> timeId = new MultiMap<long, long>();

        private readonly List<long> timeOutTime = new List<long>();

        // 记录最小时间，不用每次都去MultiMap取第一个值
        private long minTime;

        public override bool IsDispose()
        {
            return this.socket == null;
        }

        public override void Dispose()
        {
            foreach (long channelId in this.idChannels.Keys.ToArray())
            {
                this.Remove(channelId);
            }

            this.socket.Close();
            this.socket = null;
        }

        private IPEndPoint CloneAddress()
        {
            IPEndPoint ip = (IPEndPoint)this.ipEndPoint;
            return new IPEndPoint(ip.Address, ip.Port);
        }

        private void Recv()
        {
            if (this.socket == null)
            {
                return;
            }

            while (socket != null && this.socket.Available > 0)
            {
                int messageLength = this.socket.ReceiveFrom(this.cache, ref this.ipEndPoint);

                // 长度小于1，不是正常的消息
                if (messageLength < 1)
                {
                    continue;
                }

                // accept
                byte flag = this.cache[0];

                // conn从100开始，如果为1，2，3则是特殊包
                uint remoteConn = 0;
                uint localConn = 0;

                try
                {
                    KcpChannel kcpChannel = null;
                    switch (flag)
                    {
#if NOT_UNITY
                        case KcpProtocalType.SYN: // accept
                        {
                            // 长度!=5，不是SYN消息
                            if (messageLength < 9)
                            {
                                break;
                            }

                            string realAddress = null;
                            remoteConn = BitConverter.ToUInt32(this.cache, 1);
                            if (messageLength > 9)
                            {
                                realAddress = this.cache.ToStr(9, messageLength - 9);
                            }

                            remoteConn = BitConverter.ToUInt32(this.cache, 1);
                            localConn = BitConverter.ToUInt32(this.cache, 5);

                            this.waitConnectChannels.TryGetValue(remoteConn, out kcpChannel);
                            if (kcpChannel == null)
                            {
                                localConn = CreateRandomLocalConn();
                                // 已存在同样的localConn，则不处理，等待下次sync
                                if (this.localConnChannels.ContainsKey(localConn))
                                {
                                    break;
                                }
                                long id = this.CreateAcceptChannelId(localConn);
                                if (this.idChannels.ContainsKey(id))
                                {
                                    break;
                                }

                                kcpChannel = new KcpChannel(id, localConn, remoteConn, this.socket, this.CloneAddress(), this);
                                this.idChannels.Add(kcpChannel.Id, kcpChannel);
                                this.waitConnectChannels.Add(kcpChannel.RemoteConn, kcpChannel); // 连接上了或者超时后会删除
                                this.localConnChannels.Add(kcpChannel.LocalConn, kcpChannel);

                                kcpChannel.RealAddress = realAddress;

                                IPEndPoint realEndPoint = kcpChannel.RealAddress == null? kcpChannel.RemoteAddress : NetworkHelper.ToIPEndPoint(kcpChannel.RealAddress);
                                this.OnAccept(kcpChannel.Id, realEndPoint);
                            }
                            if (kcpChannel.RemoteConn != remoteConn)
                            {
                                break;
                            }

                            // 地址跟上次的不一致则跳过
                            if (kcpChannel.RealAddress != realAddress)
                            {
                                Log.Error($"KcpChannel syn address diff: {kcpChannel.Id} {kcpChannel.RealAddress} {realAddress}");
                                break;
                            }

                            try
                            {
                                byte[] buffer = this.cache;
                                buffer.WriteTo(0, KcpProtocalType.ACK);
                                buffer.WriteTo(1, kcpChannel.LocalConn);
                                buffer.WriteTo(5, kcpChannel.RemoteConn);
                                Log.Info($"KcpService syn: {kcpChannel.Id} {remoteConn} {localConn}");
                                this.socket.SendTo(buffer, 0, 9, SocketFlags.None, kcpChannel.RemoteAddress);
                            }
                            catch (Exception e)
                            {
                                Log.Error(e);
                                kcpChannel.OnError(ErrorCode.ERR_SocketCantSend);
                            }

                            break;
                        }
#endif
                        case KcpProtocalType.ACK: // connect返回
                            // 长度!=9，不是connect消息
                            if (messageLength != 9)
                            {
                                break;
                            }

                            remoteConn = BitConverter.ToUInt32(this.cache, 1);
                            localConn = BitConverter.ToUInt32(this.cache, 5);
                            kcpChannel = this.GetByLocalConn(localConn);
                            if (kcpChannel != null)
                            {
                                Log.Info($"KcpService ack: {kcpChannel.Id} {remoteConn} {localConn}");
                                kcpChannel.RemoteConn = remoteConn;
                                kcpChannel.HandleConnnect();
                            }

                            break;
                        case KcpProtocalType.FIN: // 断开
                            // 长度!=13，不是DisConnect消息
                            if (messageLength != 13)
                            {
                                break;
                            }

                            remoteConn = BitConverter.ToUInt32(this.cache, 1);
                            localConn = BitConverter.ToUInt32(this.cache, 5);
                            int error = BitConverter.ToInt32(this.cache, 9);

                            // 处理chanel
                            kcpChannel = this.GetByLocalConn(localConn);
                            if (kcpChannel == null)
                            {
                                break;
                            }

                            // 校验remoteConn，防止第三方攻击
                            if (kcpChannel.RemoteConn != remoteConn)
                            {
                                break;
                            }

                            Log.Info($"KcpService recv fin: {kcpChannel.Id} {localConn} {remoteConn} {error}");
                            kcpChannel.OnError(ErrorCode.ERR_PeerDisconnect);

                            break;
                        case KcpProtocalType.MSG: // 断开
                            // 长度<9，不是Msg消息
                            if (messageLength < 9)
                            {
                                break;
                            }

                            // 处理chanel
                            remoteConn = BitConverter.ToUInt32(this.cache, 1);
                            localConn = BitConverter.ToUInt32(this.cache, 5);

                            kcpChannel = this.GetByLocalConn(localConn);
                            if (kcpChannel == null)
                            {
                                // 通知对方断开
                                this.Disconnect(localConn, remoteConn, ErrorCode.ERR_KcpNotFoundChannel, (IPEndPoint)this.ipEndPoint, 1);
                                break;
                            }

                            // 校验remoteConn，防止第三方攻击
                            if (kcpChannel.RemoteConn != remoteConn)
                            {
                                break;
                            }

                            kcpChannel.HandleRecv(this.cache, 5, messageLength - 5);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"KcpService error: {flag} {remoteConn} {localConn}\n{e}");
                }
            }
        }

        public KcpChannel Get(long id)
        {
            KcpChannel channel;
            this.idChannels.TryGetValue(id, out channel);
            return channel;
        }

        private KcpChannel GetByLocalConn(uint localConn)
        {
            KcpChannel channel;
            this.localConnChannels.TryGetValue(localConn, out channel);
            return channel;
        }

        protected override void Get(long id, IPEndPoint address)
        {
            if (this.idChannels.TryGetValue(id, out KcpChannel kcpChannel))
            {
                return;
            }

            try
            {
                // 低32bit是localConn
                uint localConn = (uint)((ulong)id & uint.MaxValue);
                kcpChannel = new KcpChannel(id, localConn, this.socket, address, this);
                this.idChannels.Add(id, kcpChannel);
                this.localConnChannels.Add(kcpChannel.LocalConn, kcpChannel);
            }
            catch (Exception e)
            {
                Log.Error($"KcpService get error: {id}\n{e}");
            }
        }

        public override void Remove(long id)
        {
            if (!this.idChannels.TryGetValue(id, out KcpChannel kcpChannel))
            {
                return;
            }

            Log.Info($"KcpService remove channel: {id} {kcpChannel.LocalConn} {kcpChannel.RemoteConn}");
            this.idChannels.Remove(id);
            this.localConnChannels.Remove(kcpChannel.LocalConn);
            if (this.waitConnectChannels.TryGetValue(kcpChannel.RemoteConn, out KcpChannel waitChannel))
            {
                if (waitChannel.LocalConn == kcpChannel.LocalConn)
                {
                    this.waitConnectChannels.Remove(kcpChannel.RemoteConn);
                }
            }

            kcpChannel.Dispose();
        }

        private void Disconnect(uint localConn, uint remoteConn, int error, IPEndPoint address, int times)
        {
            try
            {
                if (this.socket == null)
                {
                    return;
                }

                byte[] buffer = this.cache;
                buffer.WriteTo(0, KcpProtocalType.FIN);
                buffer.WriteTo(1, localConn);
                buffer.WriteTo(5, remoteConn);
                buffer.WriteTo(9, (uint)error);
                for (int i = 0; i < times; ++i)
                {
                    this.socket.SendTo(buffer, 0, 13, SocketFlags.None, address);
                }
            }
            catch (Exception e)
            {
                Log.Error($"Disconnect error {localConn} {remoteConn} {error} {address} {e}");
            }

            Log.Info($"channel send fin: {localConn} {remoteConn} {address} {error}");
        }

        protected override void Send(long channelId, long actorId, MemoryStream stream)
        {
            KcpChannel channel = this.Get(channelId);
            if (channel == null)
            {
                return;
            }

            channel.Send(actorId, stream);
        }

        // 服务端需要看channel的update时间是否已到
        public void AddToUpdateNextTime(long time, long id)
        {
            if (time == 0)
            {
                this.updateChannels.Add(id);
                return;
            }

            if (time < this.minTime)
            {
                this.minTime = time;
            }

            this.timeId.Add(time, id);
        }

        public override void Update()
        {
            this.Recv();

            this.TimerOut();

            foreach (long id in updateChannels)
            {
                KcpChannel kcpChannel = this.Get(id);
                if (kcpChannel == null)
                {
                    continue;
                }

                if (kcpChannel.Id == 0)
                {
                    continue;
                }

                kcpChannel.Update();
            }

            this.updateChannels.Clear();

            this.RemoveConnectTimeoutChannels();
        }

        private void RemoveConnectTimeoutChannels()
        {
            using (ListComponent<long> waitRemoveChannels = ListComponent<long>.Create())
            {
                foreach (long channelId in this.waitConnectChannels.Keys)
                {
                    this.waitConnectChannels.TryGetValue(channelId, out KcpChannel kcpChannel);
                    if (kcpChannel == null)
                    {
                        Log.Error($"RemoveConnectTimeoutChannels not found KcpChannel: {channelId}");
                        continue;
                    }

                    // 连接上了要马上删除
                    if (kcpChannel.IsConnected)
                    {
                        waitRemoveChannels.List.Add(channelId);
                    }

                    // 10秒连接超时
                    if (this.TimeNow > kcpChannel.CreateTime + 10 * 1000)
                    {
                        waitRemoveChannels.List.Add(channelId);
                    }
                }

                foreach (long channelId in waitRemoveChannels.List)
                {
                    this.waitConnectChannels.Remove(channelId);
                }
            }
        }

        // 计算到期需要update的channel
        private void TimerOut()
        {
            if (this.timeId.Count == 0)
            {
                return;
            }

            uint timeNow = this.TimeNow;

            if (timeNow < this.minTime)
            {
                return;
            }

            this.timeOutTime.Clear();

            foreach (KeyValuePair<long, List<long>> kv in this.timeId)
            {
                long k = kv.Key;
                if (k > timeNow)
                {
                    minTime = k;
                    break;
                }

                this.timeOutTime.Add(k);
            }

            foreach (long k in this.timeOutTime)
            {
                foreach (long v in this.timeId[k])
                {
                    this.updateChannels.Add(v);
                }

                this.timeId.Remove(k);
            }
        }
    }
}