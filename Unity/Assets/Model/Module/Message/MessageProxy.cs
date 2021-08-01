using System;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    public class MessageProxy: IMessageHandler
    {
        private readonly Type _type;
        
        private readonly Action<Session, object> _action;

        public MessageProxy(Type type, Action<Session, object> action)
        {
            _type = type;
            _action = action;
        }

        public void Handle(Session session, object message)
        {
            _action.Invoke(session, message);
        }

        public Type GetMessageType()
        {
            return _type;
        }

        public Type GetResponseType()
        {
            return null;
        }
    }
}