using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class TcpService: NetService
    {
        private readonly Dictionary<long, TcpChannel> _idChannels = new Dictionary<long, TcpChannel>();

        private readonly SocketAsyncEventArgs _innArgs = new SocketAsyncEventArgs();

        private Socket _acceptor;

        public HashSet<long> NeedStartSend = new HashSet<long>();

        public TcpService(ThreadSyncContext threadSyncContext, NetServiceType serviceType)
        {
            ServiceType = serviceType;
            ThreadSyncContext = threadSyncContext;
        }

        public TcpService(ThreadSyncContext threadSyncContext, IPEndPoint ipEndPoint, NetServiceType serviceType)
        {
            ServiceType = serviceType;
            ThreadSyncContext = threadSyncContext;

            _acceptor = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _acceptor.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _innArgs.Completed += OnComplete;
            _acceptor.Bind(ipEndPoint);
            _acceptor.Listen(1000);

            ThreadSyncContext.PostNext(AcceptAsync);
        }

        private void OnComplete(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    SocketError socketError = e.SocketError;
                    Socket acceptSocket = e.AcceptSocket;
                    ThreadSyncContext.Post(() => { OnAcceptComplete(socketError, acceptSocket); });
                    break;
                default:
                    throw new Exception($"socket error: {e.LastOperation}");
            }
        }

#region 网络线程

        private void OnAcceptComplete(SocketError socketError, Socket acceptSocket)
        {
            if (_acceptor == null)
            {
                return;
            }

            // 开始新的accept
            AcceptAsync();

            if (socketError != SocketError.Success)
            {
                Log.Error($"accept error {socketError}");
                return;
            }

            try
            {
                long acceptChannelId = CreateAcceptChannelId(0);
                TcpChannel channel = new TcpChannel(acceptChannelId, acceptSocket, this);
                _idChannels.Add(channel.ChannelId, channel);
                long channelId = channel.ChannelId;

                OnAccept(channelId, channel.RemoteAddress);
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }

        private void AcceptAsync()
        {
            _innArgs.AcceptSocket = null;
            if (_acceptor.AcceptAsync(_innArgs))
            {
                return;
            }

            OnAcceptComplete(_innArgs.SocketError, _innArgs.AcceptSocket);
        }

        private TcpChannel Create(IPEndPoint ipEndPoint, long id)
        {
            TcpChannel channel = new TcpChannel(id, ipEndPoint, this);
            _idChannels.Add(channel.ChannelId, channel);
            return channel;
        }

        protected override void Get(long channelId, IPEndPoint address)
        {
            if (_idChannels.TryGetValue(channelId, out TcpChannel _))
            {
                return;
            }

            Create(address, channelId);
        }

        private TcpChannel Get(long channelId)
        {
            _idChannels.TryGetValue(channelId, out TcpChannel channel);
            return channel;
        }

        public override void Dispose()
        {
            _acceptor?.Close();
            _acceptor = null;
            _innArgs.Dispose();
            ThreadSyncContext = null;

            foreach (long channelId in _idChannels.Keys.ToArray())
            {
                TcpChannel channel = _idChannels[channelId];
                channel.Dispose();
            }

            _idChannels.Clear();
        }

        public override void Remove(long channelId)
        {
            if (_idChannels.TryGetValue(channelId, out TcpChannel channel))
            {
                channel.Dispose();
            }

            _idChannels.Remove(channelId);
        }

        protected override void Send(long channelId, long actorId, MemoryStream stream)
        {
            try
            {
                TcpChannel channel = Get(channelId);
                if (channel == null)
                {
                    OnError(channelId, ErrorCode.ERR_SendMessageNotFoundTChannel);
                    return;
                }

                channel.Send(actorId, stream);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Update()
        {
            foreach (long channelId in NeedStartSend)
            {
                TcpChannel tcpChannel = Get(channelId);
                tcpChannel?.Update();
            }

            NeedStartSend.Clear();
        }

        public override bool IsDispose()
        {
            return ThreadSyncContext == null;
        }

#endregion
    }
}