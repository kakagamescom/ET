using System;

namespace ET
{
    public class ActorMessageAttribute: Attribute
    {
        public ushort MsgId { get; private set; }

        public ActorMessageAttribute(ushort msgId)
        {
            MsgId = msgId;
        }
    }
}