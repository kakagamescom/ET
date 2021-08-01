using System.IO;

namespace ET
{
    /// <summary>
    /// 网络消息分发接口
    /// </summary>
    public interface IMessageDispatcher
    {
        /// <summary>
        /// 分发消息
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        void Dispatch(Session session, MemoryStream message);
    }
}