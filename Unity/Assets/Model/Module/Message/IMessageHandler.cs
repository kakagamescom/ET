using System;

namespace ET
{
    /// <summary>
    /// 消息处理器接口
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        /// 处理消息
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        void Handle(Session session, object message);
        
        /// <summary>
        /// 消息类型
        /// </summary>
        /// <returns></returns>
        Type GetMessageType();

        /// <summary>
        /// 消息响应类型
        /// </summary>
        /// <returns></returns>
        Type GetResponseType();
    }
}