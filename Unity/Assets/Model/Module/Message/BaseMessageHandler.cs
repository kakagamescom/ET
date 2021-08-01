using System;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="Message"></typeparam>
    [MessageHandler]
    public abstract class BaseMessageHandler<Message>: IMessageHandler where Message : class
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        protected abstract ETVoid Run(Session session, Message message);

        public void Handle(Session session, object msg)
        {
            Message message = msg as Message;
            if (message == null)
            {
                Log.Error($"message convert failed: {msg.GetType().Name} to {typeof(Message).Name}");
                return;
            }

            if (session.IsDisposed)
            {
                Log.Error($"session disconnect {msg}");
                return;
            }

            Run(session, message).Coroutine();
        }

        public Type GetMessageType()
        {
            return typeof(Message);
        }

        public Type GetResponseType()
        {
            return null;
        }
    }
}