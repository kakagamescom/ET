using System;
using System.IO;
using System.Net;

namespace ET
{
    /// <summary>
    /// 网络消息分发器
    /// </summary>
    public class InnerMessageDispatcher: IMessageDispatcher
    {
        public void Dispatch(Session session, MemoryStream memoryStream)
        {
            ushort msgId = 0;
            try
            {
                long actorId = BitConverter.ToInt64(memoryStream.GetBuffer(), Packet.ActorIdIndex);
                msgId = BitConverter.ToUInt16(memoryStream.GetBuffer(), Packet.MsgIdIndex);
                Type type = null;
                object message = null;
#if SERVER
                // 内网收到外网消息，有可能是gateUnit消息，还有可能是gate广播消息
                if (MsgIdTypeComponent.Instance.IsOuterActorMessage(msgId))
                {
                    InstanceIdStruct instanceIdStruct = new InstanceIdStruct(actorId);
                    instanceIdStruct.Process = Game.Options.Process;
                    long realActorId = instanceIdStruct.ToLong();

                    Entity entity = Game.EventSystem.Get(realActorId);
                    if (entity == null)
                    {
                        type = MsgIdTypeComponent.Instance.GetType(msgId);
                        message = MessageSerializeHelper.Deserialize(msgId, type, memoryStream);
                        Log.Error($"not found actor: {session.DomainScene().Name}  {msgId} {realActorId} {message}");
                        return;
                    }

                    if (entity is Session gateSession)
                    {
                        // 发送给客户端
                        memoryStream.Seek(Packet.MsgIdIndex, SeekOrigin.Begin);
                        gateSession.Send(0, memoryStream);
                        return;
                    }
                }
#endif

                type = MsgIdTypeComponent.Instance.GetType(msgId);
                message = MessageSerializeHelper.Deserialize(msgId, type, memoryStream);

                if (message is IResponse iResponse && !(message is IActorResponse))
                {
                    session.OnRead(msgId, iResponse);
                    return;
                }

                MsgIdHelper.LogMsg(session.DomainZone(), msgId, message);

                // 收到actor消息,放入actor队列
                switch (message)
                {
                    case IActorRequest iActorRequest:
                    {
                        InstanceIdStruct instanceIdStruct = new InstanceIdStruct(actorId);
                        int fromProcess = instanceIdStruct.Process;
                        instanceIdStruct.Process = Game.Options.Process;
                        long realActorId = instanceIdStruct.ToLong();

                        void Reply(IActorResponse response)
                        {
                            Session replySession = NetInnerComponent.Instance.Get(fromProcess);
                            // 发回真实的actorId 做查问题使用
                            replySession.Send(realActorId, response);
                        }

                        InnerMessageDispatcherHelper.HandleIActorRequest(msgId, realActorId, iActorRequest, Reply);
                        return;
                    }
                    case IActorResponse iActorResponse:
                    {
                        InstanceIdStruct instanceIdStruct = new InstanceIdStruct(actorId);
                        instanceIdStruct.Process = Game.Options.Process;
                        long realActorId = instanceIdStruct.ToLong();
                        InnerMessageDispatcherHelper.HandleIActorResponse(msgId, realActorId, iActorResponse);
                        return;
                    }
                    case IActorMessage iactorMessage:
                    {
                        InstanceIdStruct instanceIdStruct = new InstanceIdStruct(actorId);
                        instanceIdStruct.Process = Game.Options.Process;
                        long realActorId = instanceIdStruct.ToLong();
                        InnerMessageDispatcherHelper.HandleIActorMessage(msgId, realActorId, iactorMessage);
                        return;
                    }
                    default:
                    {
                        MessageDispatcherComponent.Instance.Handle(session, msgId, message);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"InnerMessageDispatcher error: {msgId}\n{e}");
            }
        }
    }
}