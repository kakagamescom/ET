using System;
using System.IO;
using MongoDB.Bson.IO;

namespace ET
{
    /// <summary>
    /// 网络消息序列化辅助工具类
    /// </summary>
    public static class MessageSerializeHelper
    {
        public const ushort PbMaxOpcode = 40000;
        
        public const ushort JsonMinOpcode = 51000;
        
        public static object Deserialize(ushort msgId, Type type, MemoryStream memoryStream)
        {
            if (msgId < PbMaxOpcode)
            {
                return ProtobufHelper.FromStream(type, memoryStream);
            }
            
            if (msgId >= JsonMinOpcode)
            {
                return JsonHelper.FromJson(type, memoryStream.GetBuffer().ToStr((int)memoryStream.Position, (int)(memoryStream.Length - memoryStream.Position)));
            }
#if NOT_CLIENT
            return MongoHelper.FromStream(type, memoryStream);
#else
            throw new Exception($"client no message: {msgId}");
#endif
        }

        public static void Serialize(ushort msgId, object obj, MemoryStream memoryStream)
        {
            if (msgId < PbMaxOpcode)
            {
                ProtobufHelper.ToStream(obj, memoryStream);
                return;
            }

            if (msgId >= JsonMinOpcode)
            {
                string s = JsonHelper.ToJson(obj);
                byte[] bytes = s.ToUtf8();
                memoryStream.Write(bytes, 0, bytes.Length);
                return;
            }
#if NOT_CLIENT
            MongoHelper.ToStream(obj, memoryStream);
#else
            throw new Exception($"client no message: {msgId}");
#endif
        }

        public static MemoryStream GetStream(int count = 0)
        {
            MemoryStream stream;
            if (count > 0)
            {
                stream = new MemoryStream(count);
            }
            else
            {
                stream = new MemoryStream();
            }

            return stream;
        }
        
        public static (ushort, MemoryStream) MessageToStream(object message, int count = 0)
        {
            MemoryStream stream = GetStream(Packet.MsgIdLength + count);

            ushort msgId = MsgIdTypeComponent.Instance.GetMsgId(message.GetType());
            
            stream.Seek(Packet.MsgIdLength, SeekOrigin.Begin);
            stream.SetLength(Packet.MsgIdLength);
            
            stream.GetBuffer().WriteTo(0, msgId);
            
            Serialize(msgId, message, stream);
            
            stream.Seek(0, SeekOrigin.Begin);
            return (msgId, stream);
        }
        
        public static (ushort, MemoryStream) MessageToStream(long actorId, object message, int count = 0)
        {
            int actorSize = sizeof (long);
            MemoryStream stream = GetStream(actorSize + Packet.MsgIdLength + count);

            ushort msgId = MsgIdTypeComponent.Instance.GetMsgId(message.GetType());
            
            stream.Seek(actorSize + Packet.MsgIdLength, SeekOrigin.Begin);
            stream.SetLength(actorSize + Packet.MsgIdLength);

            // 写入actorId
            stream.GetBuffer().WriteTo(0, actorId);
            stream.GetBuffer().WriteTo(actorSize, msgId);
            
            Serialize(msgId, message, stream);
            
            stream.Seek(0, SeekOrigin.Begin);
            return (msgId, stream);
        }
    }
}