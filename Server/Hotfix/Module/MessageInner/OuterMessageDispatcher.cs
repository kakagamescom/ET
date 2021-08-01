using System;
using System.IO;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    public class OuterMessageDispatcher: IMessageDispatcher
    {
        public void Dispatch(Session session, MemoryStream memoryStream)
        {
            ushort msgId = BitConverter.ToUInt16(memoryStream.GetBuffer(), Packet.KcpMsgIdIndex);
            Type type = MsgIdTypeComponent.Instance.GetType(msgId);
            object message = MessageSerializeHelper.Deserialize(msgId, type, memoryStream);

            if (message is IResponse response)
            {
                session.OnRead(msgId, response);
                return;
            }

            MsgIdHelper.LogMsg(session.DomainZone(), msgId, message);

            DispatchAsync(session, msgId, message).Coroutine();
        }

        public async ETVoid DispatchAsync(Session session, ushort msgId, object message)
        {
            // 根据消息接口判断是不是Actor消息，不同的接口做不同的处理
            switch (message)
            {
                case IActorLocationRequest actorLocationRequest: // gate session收到actor rpc消息，先向actor 发送rpc请求，再将请求结果返回客户端
                {
                    long unitId = session.GetComponent<SessionPlayerComponent>().Player.UnitId;
                    int rpcId = actorLocationRequest.RpcId; // 这里要保存客户端的rpcId
                    long instanceId = session.InstanceId;
                    IResponse response = await ActorLocationSenderComponent.Instance.Call(unitId, actorLocationRequest);
                    response.RpcId = rpcId;
                    // session可能已经断开了，所以这里需要判断
                    if (session.InstanceId == instanceId)
                    {
                        session.Reply(response);
                    }

                    break;
                }
                case IActorLocationMessage actorLocationMessage:
                {
                    long unitId = session.GetComponent<SessionPlayerComponent>().Player.UnitId;
                    ActorLocationSenderComponent.Instance.Send(unitId, actorLocationMessage);
                    break;
                }
                case IActorRequest actorRequest: // 分发IActorRequest消息，目前没有用到，需要的自己添加
                {
                    break;
                }
                case IActorMessage actorMessage: // 分发IActorMessage消息，目前没有用到，需要的自己添加
                {
                    break;
                }

                default:
                {
                    // 非Actor消息
                    MessageDispatcherComponent.Instance.Handle(session, msgId, message);
                    break;
                }
            }
        }
    }
}