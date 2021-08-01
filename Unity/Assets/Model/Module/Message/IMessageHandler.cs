using System;

namespace ET
{
    public interface IMessageHandler
    {
        void Handle(Session session, object message);
        
        Type GetMessageType();

        Type GetResponseType();
    }
}