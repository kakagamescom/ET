using System;
using System.IO;
using System.Net;

namespace ET
{
    /// <summary>
    /// 通道类型
    /// </summary>
    public enum ChannelType
    {
        Transfer, // transfer channel
        Accept,  // accept channel
    }

    /// <summary>
    /// 网络数据包
    /// </summary>
    public struct Packet
    {
        public const int MinPacketSize = 2;
        public const int OpcodeIndex = 8;
        public const int KcpOpcodeIndex = 0;
        public const int OpcodeLength = 2;
        public const int ActorIdIndex = 0;
        public const int ActorIdLength = 8;
        public const int MessageIndex = 10;

        public ushort Opcode;
        public long ActorId;
        public MemoryStream MemoryStream;
    }

    /// <summary>
    /// 网络通道抽象基类
    /// </summary>
    public abstract class NetChannel: IDisposable
    {
        public long ChannelId;

        public ChannelType ChannelType { get; protected set; }

        public int Error { get; set; }

        public IPEndPoint RemoteAddress { get; set; }

        public bool IsDisposed
        {
            get
            {
                return ChannelId == 0;
            }
        }

        public abstract void Dispose();
    }
}