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
        private readonly CircularBuffer _buffer;

        private int _packetSize;

        private ParserState _state;

        public BaseService service;

        private readonly byte[] _cache = new byte[8];

        public const int InnerPacketSizeLength = 4;

        public const int OuterPacketSizeLength = 2;

        public MemoryStream MemoryStream;

        public PacketParser(CircularBuffer buffer, BaseService service)
        {
            _buffer = buffer;
            this.service = service;
        }

        public bool Parse()
        {
            while (true)
            {
                switch (_state)
                {
                    case ParserState.PacketSize:
                    {
                        if (this.service.ServiceType == ServiceType.Inner)
                        {
                            if (_buffer.Length < InnerPacketSizeLength)
                            {
                                return false;
                            }

                            _buffer.Read(_cache, 0, InnerPacketSizeLength);

                            _packetSize = BitConverter.ToInt32(_cache, 0);
                            if (_packetSize > ushort.MaxValue * 16 || _packetSize < Packet.MinPacketSize)
                            {
                                throw new Exception($"recv packet size error, 可能是外网探测端口: {_packetSize}");
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
                                throw new Exception($"recv packet size error, 可能是外网探测端口: {_packetSize}");
                            }
                        }

                        _state = ParserState.PacketBody;
                        break;
                    }
                    case ParserState.PacketBody:
                    {
                        if (_buffer.Length < _packetSize)
                        {
                            return false;
                        }

                        MemoryStream memoryStream = MessageSerializeHelper.GetStream(_packetSize);
                        _buffer.Read(memoryStream, _packetSize);
                        //memoryStream.SetLength(packetSize - Packet.MessageIndex);
                        MemoryStream = memoryStream;

                        if (this.service.ServiceType == ServiceType.Inner)
                        {
                            memoryStream.Seek(Packet.MessageIndex, SeekOrigin.Begin);
                        }
                        else
                        {
                            memoryStream.Seek(Packet.OpcodeLength, SeekOrigin.Begin);
                        }

                        _state = ParserState.PacketSize;
                        return true;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}