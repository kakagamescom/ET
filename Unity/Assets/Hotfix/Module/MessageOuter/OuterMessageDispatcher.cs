using System;
using System.IO;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    public class OuterMessageDispatcher: IMessageDispatcher
    {
        // 查找卡死问题临时处理
        public long lastMessageTime = long.MaxValue;
        public object LastMessage;
        
        public void Dispatch(Session session, MemoryStream memoryStream)
        {
            ushort msgId = BitConverter.ToUInt16(memoryStream.GetBuffer(), Packet.KcpMsgIdIndex);
            Type type = MsgIdTypeComponent.Instance.GetType(msgId);
            object message = MessageSerializeHelper.Deserialize(msgId, type, memoryStream);

            if (TimeHelper.ClientFrameTime() - this.lastMessageTime > 3000)
            {
                Log.Info($"可能导致卡死的消息: {this.LastMessage}");
            }

            this.lastMessageTime = TimeHelper.ClientFrameTime();
            this.LastMessage = message;
            
            if (message is IResponse response)
            {
                session.OnRead(msgId, response);
                return;
            }

            MsgIdHelper.LogMsg(session.DomainZone(), msgId, message);
            // 普通消息或者是Rpc请求消息
            MessageDispatcherComponent.Instance.Handle(session, msgId, message);
        }
    }
}