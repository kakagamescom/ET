using System;
using System.IO;

namespace ET
{
    /// <summary>
    /// 解析状态
    /// </summary>
    public enum ParserState
    {
        PacketSize,
        PacketBody
    }

    /// <summary>
    /// 数据包解析器
    /// </summary>
    public class PacketParser
    {
        public const int InnerPacketSizeLength = 4;
        
        public const int OuterPacketSizeLength = 2;
        
        private readonly CircularBuffer _buffer;

        private int _packetSize;

        private ParserState _state;

        private NetService _netService;

        private readonly byte[] _cache = new byte[8];

        public MemoryStream MemoryStream;

        public PacketParser(CircularBuffer buffer, NetService netService)
        {
            _buffer = buffer;
            _netService = netService;
        }

        public bool Parse()
        {
            while (true)
            {
                switch (_state)
                {
                    case ParserState.PacketSize:
                    {
                        if (!ParseSize())
                            return false;
                        break;
                    }
                    case ParserState.PacketBody:
                        return ParseBody();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private bool ParseSize()
        {
            if (this._netService.ServiceType == NetServiceType.Inner)
            {
                if (_buffer.Length < InnerPacketSizeLength)
                {
                    return false;
                }

                _buffer.Read(_cache, 0, InnerPacketSizeLength);

                _packetSize = BitConverter.ToInt32(_cache, 0);
                if (_packetSize > ushort.MaxValue * 16 || _packetSize < Packet.MinPacketSize)
                {
                    throw new Exception($"recv packet size error: {_packetSize}");
                }
            }
            else
            {
                if (_buffer.Length < OuterPacketSizeLength)
                {
                    return false;
                }

                _buffer.Read(_cache, 0, OuterPacketSizeLength);

                _packetSize = BitConverter.ToUInt16(_cache, 0);
                if (_packetSize < Packet.MinPacketSize)
                {
                    throw new Exception($"recv packet size error: {_packetSize}");
                }
            }

            _state = ParserState.PacketBody;
            
            return true;
        }

        private bool ParseBody()
        {
            if (_buffer.Length < _packetSize)
            {
                return false;
            }

            MemoryStream memoryStream = MessageSerializeHelper.GetStream(_packetSize);
            _buffer.Read(memoryStream, _packetSize);
            //memoryStream.SetLength(packetSize - Packet.MessageIndex);
            MemoryStream = memoryStream;

            if (this._netService.ServiceType == NetServiceType.Inner)
            {
                memoryStream.Seek(Packet.MessageIndex, SeekOrigin.Begin);
            }
            else
            {
                memoryStream.Seek(Packet.MsgIdLength, SeekOrigin.Begin);
            }

            _state = ParserState.PacketSize;
            
            return true;
        }
    }
}