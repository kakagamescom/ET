using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace ET
{
    /// <summary>
    /// 封装Socket, 将回调push到主线程处理
    /// </summary>
    public sealed class TcpChannel: NetChannel
    {
        private readonly TcpService _service;
        
        private Socket _socket;
        
        private SocketAsyncEventArgs _innArgs = new SocketAsyncEventArgs();
        
        private SocketAsyncEventArgs _outArgs = new SocketAsyncEventArgs();

        private readonly CircularBuffer _recvBuffer = new CircularBuffer();
        
        private readonly CircularBuffer _sendBuffer = new CircularBuffer();

        private bool _isSending;

        private bool _isConnected;

        private readonly PacketParser _packetParser;

        private readonly byte[] _sendCache = new byte[Packet.OpcodeLength + Packet.ActorIdLength];

        private void OnComplete(object sender, SocketAsyncEventArgs eventArgs)
        {
            switch (eventArgs.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    _service.ThreadSyncContext.Post(() => OnConnectComplete(eventArgs));
                    break;
                case SocketAsyncOperation.Receive:
                    _service.ThreadSyncContext.Post(() => OnRecvComplete(eventArgs));
                    break;
                case SocketAsyncOperation.Send:
                    _service.ThreadSyncContext.Post(() => OnSendComplete(eventArgs));
                    break;
                case SocketAsyncOperation.Disconnect:
                    _service.ThreadSyncContext.Post(() => OnDisconnectComplete(eventArgs));
                    break;
                default:
                    throw new Exception($"socket error: {eventArgs.LastOperation}");
            }
        }

#region net thread

        public TcpChannel(long channelId, IPEndPoint ipEndPoint, TcpService service)
        {
            ChannelType = ChannelType.Transfer;
            
            ChannelId = channelId;
            _service = service;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.NoDelay = true;
            _packetParser = new PacketParser(_recvBuffer, _service);
            _innArgs.Completed += OnComplete;
            _outArgs.Completed += OnComplete;

            RemoteAddress = ipEndPoint;
            _isConnected = false;
            _isSending = false;

            _service.ThreadSyncContext.PostNext(ConnectAsync);
        }

        public TcpChannel(long channelId, Socket acceptSocket, TcpService service)
        {
            ChannelType = ChannelType.Accept;
            
            ChannelId = channelId;
            _service = service;
            _socket = acceptSocket;
            _socket.NoDelay = true;
            _packetParser = new PacketParser(_recvBuffer, _service);
            _innArgs.Completed += OnComplete;
            _outArgs.Completed += OnComplete;

            RemoteAddress = (IPEndPoint)acceptSocket.RemoteEndPoint;
            _isConnected = true;
            _isSending = false;

            // 下一帧再开始读写
            _service.ThreadSyncContext.PostNext(() =>
            {
                StartRecv();
                StartSend();
            });
        }

        public override void Dispose()
        {
            if (IsDisposed)
                return;

            Log.Info($"channel dispose: {ChannelId} {RemoteAddress}");

            long channelId = ChannelId;
            ChannelId = 0;
            _service.Remove(channelId);
            _socket.Close();
            _innArgs.Dispose();
            _outArgs.Dispose();
            _innArgs = null;
            _outArgs = null;
            _socket = null;
        }

        public void Send(long actorId, MemoryStream stream)
        {
            if (IsDisposed)
            {
                throw new Exception("TChannel已经被Dispose, 不能发送消息");
            }

            switch (_service.ServiceType)
            {
                case NetServiceType.Inner:
                {
                    int messageSize = (int)(stream.Length - stream.Position);
                    if (messageSize > ushort.MaxValue * 16)
                    {
                        throw new Exception($"send packet too large: {stream.Length} {stream.Position}");
                    }

                    _sendCache.WriteTo(0, messageSize);
                    _sendBuffer.Write(_sendCache, 0, PacketParser.InnerPacketSizeLength);

                    // actorId
                    stream.GetBuffer().WriteTo(0, actorId);
                    _sendBuffer.Write(stream.GetBuffer(), (int)stream.Position, (int)(stream.Length - stream.Position));
                    break;
                }
                case NetServiceType.Outer:
                {
                    ushort messageSize = (ushort)(stream.Length - stream.Position);

                    _sendCache.WriteTo(0, messageSize);
                    _sendBuffer.Write(_sendCache, 0, PacketParser.OuterPacketSizeLength);

                    _sendBuffer.Write(stream.GetBuffer(), (int)stream.Position, (int)(stream.Length - stream.Position));
                    break;
                }
            }

            if (!_isSending)
            {
                //StartSend();
                _service.NeedStartSend.Add(ChannelId);
            }
        }

        private void ConnectAsync()
        {
            _outArgs.RemoteEndPoint = RemoteAddress;
            if (_socket.ConnectAsync(_outArgs))
                return;

            OnConnectComplete(_outArgs);
        }

        private void OnConnectComplete(object o)
        {
            if (_socket == null)
                return;

            SocketAsyncEventArgs eventArgs = (SocketAsyncEventArgs)o;
            if (eventArgs.SocketError != SocketError.Success)
            {
                OnError((int)eventArgs.SocketError);
                return;
            }

            eventArgs.RemoteEndPoint = null;
            _isConnected = true;
            StartRecv();
            StartSend();
        }

        private void OnDisconnectComplete(object o)
        {
            SocketAsyncEventArgs eventArgs = (SocketAsyncEventArgs)o;
            OnError((int)eventArgs.SocketError);
        }

        private void StartRecv()
        {
            while (true)
            {
                try
                {
                    if (_socket == null)
                        return;

                    int size = _recvBuffer.ChunkSize - _recvBuffer.LastIndex;
                    _innArgs.SetBuffer(_recvBuffer.Last, _recvBuffer.LastIndex, size);
                }
                catch (Exception ex)
                {
                    Log.Error($"tchannel error: {ChannelId}\n{ex}");
                    OnError(ErrorCode.ERR_TChannelRecvError);
                    return;
                }

                if (_socket.ReceiveAsync(_innArgs))
                {
                    return;
                }

                HandleRecv(_innArgs);
            }
        }

        private void OnRecvComplete(object o)
        {
            HandleRecv(o);

            if (_socket == null)
                return;

            StartRecv();
        }

        private void HandleRecv(object o)
        {
            if (_socket == null)
                return;

            SocketAsyncEventArgs eventArgs = (SocketAsyncEventArgs)o;

            if (eventArgs.SocketError != SocketError.Success)
            {
                OnError((int)eventArgs.SocketError);
                return;
            }

            if (eventArgs.BytesTransferred == 0)
            {
                OnError(ErrorCode.ERR_PeerDisconnect);
                return;
            }

            _recvBuffer.LastIndex += eventArgs.BytesTransferred;
            if (_recvBuffer.LastIndex == _recvBuffer.ChunkSize)
            {
                _recvBuffer.AddLast();
                _recvBuffer.LastIndex = 0;
            }

            // 收到消息回调
            while (true)
            {
                // 这里循环解析消息执行，有可能，执行消息的过程中断开了session
                if (_socket == null)
                    return;

                try
                {
                    bool ret = _packetParser.Parse();
                    if (!ret)
                        break;

                    OnRead(_packetParser.MemoryStream);
                }
                catch (Exception ex)
                {
                    Log.Error($"ip: {RemoteAddress} {ex}");
                    OnError(ErrorCode.ERR_SocketError);
                    return;
                }
            }
        }

        public void Update()
        {
            StartSend();
        }

        private void StartSend()
        {
            if (!_isConnected)
                return;

            while (true)
            {
                try
                {
                    if (_socket == null)
                        return;

                    // 没有数据需要发送
                    if (_sendBuffer.Length == 0)
                    {
                        _isSending = false;
                        return;
                    }

                    _isSending = true;

                    int sendSize = _sendBuffer.ChunkSize - _sendBuffer.FirstIndex;
                    if (sendSize > _sendBuffer.Length)
                    {
                        sendSize = (int)_sendBuffer.Length;
                    }

                    _outArgs.SetBuffer(_sendBuffer.First, _sendBuffer.FirstIndex, sendSize);

                    if (_socket.SendAsync(_outArgs))
                    {
                        return;
                    }

                    HandleSend(_outArgs);
                }
                catch (Exception e)
                {
                    throw new Exception($"socket set buffer error: {_sendBuffer.First.Length}, {_sendBuffer.FirstIndex}", e);
                }
            }
        }

        private void OnSendComplete(object o)
        {
            HandleSend(o);

            StartSend();
        }

        private void HandleSend(object o)
        {
            if (_socket == null)
            {
                return;
            }

            SocketAsyncEventArgs e = (SocketAsyncEventArgs)o;

            if (e.SocketError != SocketError.Success)
            {
                OnError((int)e.SocketError);
                return;
            }

            if (e.BytesTransferred == 0)
            {
                OnError(ErrorCode.ERR_PeerDisconnect);
                return;
            }

            _sendBuffer.FirstIndex += e.BytesTransferred;
            if (_sendBuffer.FirstIndex == _sendBuffer.ChunkSize)
            {
                _sendBuffer.FirstIndex = 0;
                _sendBuffer.RemoveFirst();
            }
        }

        private void OnRead(MemoryStream memoryStream)
        {
            try
            {
                long channelId = ChannelId;
                _service.OnRead(channelId, memoryStream);
            }
            catch (Exception ex)
            {
                Log.Error($"{RemoteAddress} {memoryStream.Length} {ex}");
                // 出现任何消息解析异常都要断开Session，防止客户端伪造消息
                OnError(ErrorCode.ERR_PacketParserError);
            }
        }

        private void OnError(int error)
        {
            Log.Info($"TChannel OnError: {error} {RemoteAddress}");

            long channelId = ChannelId;
            _service.Remove(channelId);
            _service.OnError(channelId, error);
        }

#endregion
    }
}