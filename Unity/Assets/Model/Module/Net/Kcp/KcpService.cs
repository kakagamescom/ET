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
    /// <summary>
    /// KCP协议类型
    /// </summary>
    public static class KcpProtocalType
    {
        public const byte SYN = 1;
        public const byte ACK = 2;
        public const byte FIN = 3;
        public const byte MSG = 4;
    }

    public sealed class KcpService: NetService
    {
        // KService创建的时间
        private readonly long _startTime;

        // 当前时间 - KService创建的时间, 线程安全
        public uint TimeNow
        {
            get
            {
                return (uint)(TimeHelper.ClientNow() - this._startTime);
            }
        }

        private Socket _socket;

#region callback

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

        public KcpService(ThreadSyncContext threadSyncContext, IPEndPoint ipEndPoint, NetServiceType serviceType)
        {
            this.ServiceType = serviceType;
            this.ThreadSyncContext = threadSyncContext;
            this._startTime = TimeHelper.ClientNow();
            this._socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                this._socket.SendBufferSize = Kcp.OneM * 64;
                this._socket.ReceiveBufferSize = Kcp.OneM * 64;
            }

            this._socket.Bind(ipEndPoint);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const uint IOC_IN = 0x80000000;
                const uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                this._socket.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
            }
        }

        public KcpService(ThreadSyncContext threadSyncContext, NetServiceType serviceType)
        {
            this.ServiceType = serviceType;
            this.ThreadSyncContext = threadSyncContext;
            this._startTime = TimeHelper.ClientNow();
            this._socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // 作为客户端不需要修改发送跟接收缓冲区大小
            this._socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const uint IOC_IN = 0x80000000;
                const uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                this._socket.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
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
        private readonly Dictionary<long, KcpChannel> _idChannels = new Dictionary<long, KcpChannel>();
        private readonly Dictionary<long, KcpChannel> _localConnChannels = new Dictionary<long, KcpChannel>();
        private readonly Dictionary<long, KcpChannel> _waitConnectChannels = new Dictionary<long, KcpChannel>();

        private readonly byte[] _cache = new byte[8192];
        private EndPoint _ipEndPoint = new IPEndPoint(IPAddress.Any, 0);

        // 下帧要更新的channel
        private readonly HashSet<long> _updateChannels = new HashSet<long>();

        // 下次时间更新的channel
        private readonly MultiMap<long, long> _timeId = new MultiMap<long, long>();

        private readonly List<long> _timeoutTime = new List<long>();

        // 记录最小时间，不用每次都去MultiMap取第一个值
        private long _minTime;

        public override bool IsDispose()
        {
            return this._socket == null;
        }

        public override void Dispose()
        {
            foreach (long channelId in this._idChannels.Keys.ToArray())
            {
                this.Remove(channelId);
            }

            this._socket.Close();
            this._socket = null;
        }

        private IPEndPoint CloneAddress()
        {
            IPEndPoint ip = (IPEndPoint)this._ipEndPoint;
            return new IPEndPoint(ip.Address, ip.Port);
        }

        private void Recv()
        {
            if (this._socket == null)
            {
                return;
            }

            while (this._socket != null && this._socket.Available > 0)
            {
                int messageLength = this._socket.ReceiveFrom(this._cache, ref this._ipEndPoint);

                // 长度小于1，不是正常的消息
                if (messageLength < 1)
                {
                    continue;
                }

                // accept
                byte flag = this._cache[0];

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
                            remoteConn = BitConverter.ToUInt32(this._cache, 1);
                            if (messageLength > 9)
                            {
                                realAddress = this._cache.ToStr(9, messageLength - 9);
                            }

                            remoteConn = BitConverter.ToUInt32(this._cache, 1);
                            localConn = BitConverter.ToUInt32(this._cache, 5);

                            this._waitConnectChannels.TryGetValue(remoteConn, out kcpChannel);
                            if (kcpChannel == null)
                            {
                                localConn = CreateRandomLocalConn();
                                // 已存在同样的localConn，则不处理，等待下次sync
                                if (this._localConnChannels.ContainsKey(localConn))
                                {
                                    break;
                                }
                                long id = this.CreateAcceptChannelId(localConn);
                                if (this._idChannels.ContainsKey(id))
                                {
                                    break;
                                }

                                kcpChannel = new KcpChannel(id, localConn, remoteConn, this._socket, this.CloneAddress(), this);
                                this._idChannels.Add(kcpChannel.Id, kcpChannel);
                                this._waitConnectChannels.Add(kcpChannel.RemoteConn, kcpChannel); // 连接上了或者超时后会删除
                                this._localConnChannels.Add(kcpChannel.LocalConn, kcpChannel);

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
                                byte[] buffer = this._cache;
                                buffer.WriteTo(0, KcpProtocalType.ACK);
                                buffer.WriteTo(1, kcpChannel.LocalConn);
                                buffer.WriteTo(5, kcpChannel.RemoteConn);
                                Log.Info($"KcpService syn: {kcpChannel.Id} {remoteConn} {localConn}");
                                this._socket.SendTo(buffer, 0, 9, SocketFlags.None, kcpChannel.RemoteAddress);
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

                            remoteConn = BitConverter.ToUInt32(this._cache, 1);
                            localConn = BitConverter.ToUInt32(this._cache, 5);
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

                            remoteConn = BitConverter.ToUInt32(this._cache, 1);
                            localConn = BitConverter.ToUInt32(this._cache, 5);
                            int error = BitConverter.ToInt32(this._cache, 9);

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
                            remoteConn = BitConverter.ToUInt32(this._cache, 1);
                            localConn = BitConverter.ToUInt32(this._cache, 5);

                            kcpChannel = this.GetByLocalConn(localConn);
                            if (kcpChannel == null)
                            {
                                // 通知对方断开
                                this.Disconnect(localConn, remoteConn, ErrorCode.ERR_KcpNotFoundChannel, (IPEndPoint)this._ipEndPoint, 1);
                                break;
                            }

                            // 校验remoteConn，防止第三方攻击
                            if (kcpChannel.RemoteConn != remoteConn)
                            {
                                break;
                            }

                            kcpChannel.HandleRecv(this._cache, 5, messageLength - 5);
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
            this._idChannels.TryGetValue(id, out channel);
            return channel;
        }

        private KcpChannel GetByLocalConn(uint localConn)
        {
            KcpChannel channel;
            this._localConnChannels.TryGetValue(localConn, out channel);
            return channel;
        }

        protected override void Get(long id, IPEndPoint address)
        {
            if (this._idChannels.TryGetValue(id, out KcpChannel kcpChannel))
            {
                return;
            }

            try
            {
                // 低32bit是localConn
                uint localConn = (uint)((ulong)id & uint.MaxValue);
                kcpChannel = new KcpChannel(id, localConn, this._socket, address, this);
                this._idChannels.Add(id, kcpChannel);
                this._localConnChannels.Add(kcpChannel.LocalConn, kcpChannel);
            }
            catch (Exception e)
            {
                Log.Error($"KcpService get error: {id}\n{e}");
            }
        }

        public override void Remove(long id)
        {
            if (!this._idChannels.TryGetValue(id, out KcpChannel kcpChannel))
            {
                return;
            }

            Log.Info($"KcpService remove channel: {id} {kcpChannel.LocalConn} {kcpChannel.RemoteConn}");
            this._idChannels.Remove(id);
            this._localConnChannels.Remove(kcpChannel.LocalConn);
            if (this._waitConnectChannels.TryGetValue(kcpChannel.RemoteConn, out KcpChannel waitChannel))
            {
                if (waitChannel.LocalConn == kcpChannel.LocalConn)
                {
                    this._waitConnectChannels.Remove(kcpChannel.RemoteConn);
                }
            }

            kcpChannel.Dispose();
        }

        private void Disconnect(uint localConn, uint remoteConn, int error, IPEndPoint address, int times)
        {
            try
            {
                if (this._socket == null)
                {
                    return;
                }

                byte[] buffer = this._cache;
                buffer.WriteTo(0, KcpProtocalType.FIN);
                buffer.WriteTo(1, localConn);
                buffer.WriteTo(5, remoteConn);
                buffer.WriteTo(9, (uint)error);
                for (int i = 0; i < times; ++i)
                {
                    this._socket.SendTo(buffer, 0, 13, SocketFlags.None, address);
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
                this._updateChannels.Add(id);
                return;
            }

            if (time < this._minTime)
            {
                this._minTime = time;
            }

            this._timeId.Add(time, id);
        }

        public override void Update()
        {
            this.Recv();

            this.TimerOut();

            foreach (long id in this._updateChannels)
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

            this._updateChannels.Clear();

            this.RemoveConnectTimeoutChannels();
        }

        private void RemoveConnectTimeoutChannels()
        {
            using (ListComponent<long> waitRemoveChannels = ListComponent<long>.Create())
            {
                foreach (long channelId in this._waitConnectChannels.Keys)
                {
                    this._waitConnectChannels.TryGetValue(channelId, out KcpChannel kcpChannel);
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
                    this._waitConnectChannels.Remove(channelId);
                }
            }
        }

        // 计算到期需要update的channel
        private void TimerOut()
        {
            if (this._timeId.Count == 0)
            {
                return;
            }

            uint timeNow = this.TimeNow;

            if (timeNow < this._minTime)
            {
                return;
            }

            this._timeoutTime.Clear();

            foreach (KeyValuePair<long, List<long>> kv in this._timeId)
            {
                long k = kv.Key;
                if (k > timeNow)
                {
                    this._minTime = k;
                    break;
                }

                this._timeoutTime.Add(k);
            }

            foreach (long k in this._timeoutTime)
            {
                foreach (long v in this._timeId[k])
                {
                    this._updateChannels.Add(v);
                }

                this._timeId.Remove(k);
            }
        }
    }
}