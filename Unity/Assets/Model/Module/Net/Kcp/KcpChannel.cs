using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ET
{
    public struct KcpWaitPacket
    {
        public long ActorId;
        public MemoryStream MemoryStream;
    }

    /// <summary>
    /// KCP Channel
    /// </summary>
    public class KcpChannel: NetChannel
    {
        public static readonly Dictionary<IntPtr, KcpChannel> KcpPtrChannels = new Dictionary<IntPtr, KcpChannel>();

        public KcpService Service;

        private Socket _socket;

        public IntPtr kcp { get; private set; }

        private readonly Queue<KcpWaitPacket> _sendBuffer = new Queue<KcpWaitPacket>();

        private uint _lastRecvTime;

        public readonly uint CreateTime;

        public uint LocalConn { get; set; }
        
        public uint RemoteConn { get; set; }

        private readonly byte[] _sendCache = new byte[2 * 1024];

        public bool IsConnected { get; private set; }

        public string RealAddress { get; set; }

        private const int _maxPacketSize = 10000;

        private MemoryStream _ms = new MemoryStream(_maxPacketSize);

        private MemoryStream _readMemory;
        private int _needReadSplitCount;

        private void InitKcp()
        {
            KcpPtrChannels.Add(kcp, this);
            switch (Service.ServiceType)
            {
                case NetServiceType.Inner:
                    Kcp.KcpNodelay(kcp, 1, 10, 2, 1);
                    Kcp.KcpWndsize(kcp, ushort.MaxValue, ushort.MaxValue);
                    Kcp.KcpSetmtu(kcp, 1400); // 默认1400
                    Kcp.KcpSetminrto(kcp, 30);
                    break;
                case NetServiceType.Outer:
                    Kcp.KcpNodelay(kcp, 1, 10, 2, 1);
                    Kcp.KcpWndsize(kcp, 256, 256);
                    Kcp.KcpSetmtu(kcp, 470);
                    Kcp.KcpSetminrto(kcp, 30);
                    break;
            }
        }

        // connect
        public KcpChannel(long id, uint localConn, Socket socket, IPEndPoint remoteEndPoint, KcpService kService)
        {
            LocalConn = localConn;

            Id = id;
            ChannelType = ChannelType.Transfer;

            Log.Info($"channel create: {Id} {LocalConn} {remoteEndPoint} {ChannelType}");

            kcp = IntPtr.Zero;
            Service = kService;
            RemoteAddress = remoteEndPoint;
            _socket = socket;
            _lastRecvTime = kService.TimeNow;
            CreateTime = kService.TimeNow;

            Connect();
        }

        // accept
        public KcpChannel(long id, uint localConn, uint remoteConn, Socket socket, IPEndPoint remoteEndPoint, KcpService kService)
        {
            Id = id;
            ChannelType = ChannelType.Accept;

            Log.Info($"channel create: {Id} {localConn} {remoteConn} {remoteEndPoint} {ChannelType}");

            Service = kService;
            LocalConn = localConn;
            RemoteConn = remoteConn;
            RemoteAddress = remoteEndPoint;
            _socket = socket;
            kcp = Kcp.KcpCreate(RemoteConn, IntPtr.Zero);
            InitKcp();

            _lastRecvTime = kService.TimeNow;
            CreateTime = kService.TimeNow;
        }

        public override void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            uint localConn = LocalConn;
            uint remoteConn = RemoteConn;
            Log.Info($"channel dispose: {Id} {localConn} {remoteConn}");

            long id = Id;
            Id = 0;
            Service.Remove(id);

            try
            {
                //Service.Disconnect(localConn, remoteConn, Error, RemoteAddress, 3);
            }

            catch (Exception e)
            {
                Log.Error(e);
            }

            if (kcp != IntPtr.Zero)
            {
                KcpPtrChannels.Remove(kcp);
                Kcp.KcpRelease(kcp);
                kcp = IntPtr.Zero;
            }

            _socket = null;
        }

        public void HandleConnnect()
        {
            // 如果连接上了就不用处理了
            if (IsConnected)
            {
                return;
            }

            kcp = Kcp.KcpCreate(RemoteConn, IntPtr.Zero);
            InitKcp();

            Log.Info($"channel connected: {Id} {LocalConn} {RemoteConn} {RemoteAddress}");
            IsConnected = true;
            _lastRecvTime = Service.TimeNow;

            while (true)
            {
                if (_sendBuffer.Count <= 0)
                {
                    break;
                }

                KcpWaitPacket buffer = _sendBuffer.Dequeue();
                KcpSend(buffer);
            }
        }

        /// <summary>
        /// 发送请求连接消息
        /// </summary>
        private void Connect()
        {
            try
            {
                uint timeNow = Service.TimeNow;

                _lastRecvTime = timeNow;

                byte[] buffer = _sendCache;
                buffer.WriteTo(0, KcpProtocalType.SYN);
                buffer.WriteTo(1, LocalConn);
                buffer.WriteTo(5, RemoteConn);
                _socket.SendTo(buffer, 0, 9, SocketFlags.None, RemoteAddress);
                Log.Info($"KcpChannel connect {Id} {LocalConn} {RemoteConn} {RealAddress} {_socket.LocalEndPoint}");

                // 300毫秒后再次update发送connect请求
                Service.AddToUpdateNextTime(timeNow + 300, Id);
            }
            catch (Exception e)
            {
                Log.Error(e);
                OnError(ErrorCode.ERR_SocketCantSend);
            }
        }

        public void Update()
        {
            if (IsDisposed)
            {
                return;
            }

            uint timeNow = Service.TimeNow;

            // 如果还没连接上，发送连接请求
            if (!IsConnected)
            {
                // 10秒超时没连接上则报错
                if (timeNow - CreateTime > 10000)
                {
                    Log.Error($"KcpChannel connect timeout: {Id} {RemoteConn} {timeNow} {CreateTime} {ChannelType} {RemoteAddress}");
                    OnError(ErrorCode.ERR_KcpConnectTimeout);
                    return;
                }

                switch (ChannelType)
                {
                    case ChannelType.Transfer:
                        Connect();
                        break;
                }

                return;
            }

            if (kcp == IntPtr.Zero)
            {
                return;
            }

            try
            {
                Kcp.KcpUpdate(kcp, timeNow);
            }
            catch (Exception e)
            {
                Log.Error(e);
                OnError(ErrorCode.ERR_SocketError);
                return;
            }

            uint nextUpdateTime = Kcp.KcpCheck(kcp, timeNow);
            Service.AddToUpdateNextTime(nextUpdateTime, Id);
        }

        public void HandleRecv(byte[] date, int offset, int length)
        {
            if (IsDisposed)
            {
                return;
            }

            IsConnected = true;

            Kcp.KcpInput(kcp, date, offset, length);
            Service.AddToUpdateNextTime(0, Id);

            while (true)
            {
                if (IsDisposed)
                {
                    break;
                }

                int n = Kcp.KcpPeeksize(kcp);
                if (n < 0)
                {
                    break;
                }

                if (n == 0)
                {
                    OnError((int)SocketError.NetworkReset);
                    return;
                }

                if (_needReadSplitCount > 0) // 说明消息分片了
                {
                    byte[] buffer = _readMemory.GetBuffer();
                    int count = Kcp.KcpRecv(kcp, buffer, (int)_readMemory.Length - _needReadSplitCount, n);
                    _needReadSplitCount -= count;
                    if (n != count)
                    {
                        Log.Error($"KcpChannel read error1: {LocalConn} {RemoteConn}");
                        OnError(ErrorCode.ERR_KcpReadNotSame);
                        return;
                    }

                    if (_needReadSplitCount < 0)
                    {
                        Log.Error($"KcpChannel read error2: {LocalConn} {RemoteConn}");
                        OnError(ErrorCode.ERR_KcpSplitError);
                        return;
                    }

                    // 没有读完
                    if (_needReadSplitCount != 0)
                    {
                        continue;
                    }
                }
                else
                {
                    _readMemory = _ms;
                    _readMemory.SetLength(n);
                    _readMemory.Seek(0, SeekOrigin.Begin);

                    byte[] buffer = _readMemory.GetBuffer();
                    int count = Kcp.KcpRecv(kcp, buffer, 0, n);
                    if (n != count)
                    {
                        break;
                    }

                    // 判断是不是分片
                    if (n == 8)
                    {
                        int headInt = BitConverter.ToInt32(_readMemory.GetBuffer(), 0);
                        if (headInt == 0)
                        {
                            _needReadSplitCount = BitConverter.ToInt32(_readMemory.GetBuffer(), 4);
                            if (_needReadSplitCount <= _maxPacketSize)
                            {
                                Log.Error($"KcpChannel read error3: {_needReadSplitCount} {LocalConn} {RemoteConn}");
                                OnError(ErrorCode.ERR_KcpSplitCountError);
                                return;
                            }

                            _readMemory = new MemoryStream(_needReadSplitCount);
                            _readMemory.SetLength(_needReadSplitCount);
                            _readMemory.Seek(0, SeekOrigin.Begin);
                            continue;
                        }
                    }
                }

                switch (Service.ServiceType)
                {
                    case NetServiceType.Inner:
                        _readMemory.Seek(Packet.ActorIdLength + Packet.OpcodeLength, SeekOrigin.Begin);
                        break;
                    case NetServiceType.Outer:
                        _readMemory.Seek(Packet.OpcodeLength, SeekOrigin.Begin);
                        break;
                }

                _lastRecvTime = Service.TimeNow;
                MemoryStream mem = _readMemory;
                _readMemory = null;
                OnRead(mem);
            }
        }

        public void Output(IntPtr bytes, int count)
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                // 没连接上 kcp不往外发消息, 其实本来没连接上不会调用update，这里只是做一层保护
                if (!IsConnected)
                {
                    return;
                }

                if (count == 0)
                {
                    Log.Error($"output 0");
                    return;
                }

                byte[] buffer = _sendCache;
                buffer.WriteTo(0, KcpProtocalType.MSG);
                // 每个消息头部写下该channel的id;
                buffer.WriteTo(1, LocalConn);
                Marshal.Copy(bytes, buffer, 5, count);
                _socket.SendTo(buffer, 0, count + 5, SocketFlags.None, RemoteAddress);
            }
            catch (Exception e)
            {
                Log.Error(e);
                OnError(ErrorCode.ERR_SocketCantSend);
            }
        }

        private void KcpSend(KcpWaitPacket kcpWaitPacket)
        {
            if (IsDisposed)
            {
                return;
            }

            MemoryStream memoryStream = kcpWaitPacket.MemoryStream;
            int count = (int)(memoryStream.Length - memoryStream.Position);

            if (Service.ServiceType == NetServiceType.Inner)
            {
                memoryStream.GetBuffer().WriteTo(0, kcpWaitPacket.ActorId);
            }

            // 超出maxPacketSize需要分片
            if (count <= _maxPacketSize)
            {
                Kcp.KcpSend(kcp, memoryStream.GetBuffer(), (int)memoryStream.Position, count);
            }
            else
            {
                // 先发分片信息
                _sendCache.WriteTo(0, 0);
                _sendCache.WriteTo(4, count);
                Kcp.KcpSend(kcp, _sendCache, 0, 8);

                // 分片发送
                int alreadySendCount = 0;
                while (alreadySendCount < count)
                {
                    int leftCount = count - alreadySendCount;

                    int sendCount = leftCount < _maxPacketSize? leftCount : _maxPacketSize;

                    Kcp.KcpSend(kcp, memoryStream.GetBuffer(), (int)memoryStream.Position + alreadySendCount, sendCount);

                    alreadySendCount += sendCount;
                }
            }

            Service.AddToUpdateNextTime(0, Id);
        }

        public void Send(long actorId, MemoryStream stream)
        {
            if (kcp != IntPtr.Zero)
            {
                // 检查等待发送的消息，如果超出最大等待大小，应该断开连接
                int n = Kcp.KcpWaitsnd(kcp);

                int maxWaitSize = 0;
                switch (Service.ServiceType)
                {
                    case NetServiceType.Inner:
                        maxWaitSize = Kcp.InnerMaxWaitSize;
                        break;
                    case NetServiceType.Outer:
                        maxWaitSize = Kcp.OuterMaxWaitSize;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (n > maxWaitSize)
                {
                    Log.Error($"kcp wait snd too large: {n}: {Id} {LocalConn} {RemoteConn}");
                    OnError(ErrorCode.ERR_KcpWaitSendSizeTooLarge);
                    return;
                }
            }

            KcpWaitPacket kcpWaitPacket = new KcpWaitPacket() { ActorId = actorId, MemoryStream = stream };
            if (!IsConnected)
            {
                _sendBuffer.Enqueue(kcpWaitPacket);
                return;
            }

            KcpSend(kcpWaitPacket);
        }

        private void OnRead(MemoryStream memoryStream)
        {
            Service.OnRead(Id, memoryStream);
        }

        public void OnError(int error)
        {
            long channelId = Id;
            Service.Remove(channelId);
            Service.OnError(channelId, error);
        }
    }
}