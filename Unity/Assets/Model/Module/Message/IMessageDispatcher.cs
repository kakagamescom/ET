using System.IO;

namespace ET
{
    /// <summary>
    /// 网络消息分发接口
    /// </summary>
    public interface IMessageDispatcher
    {
        void Dispatch(Session session, MemoryStream message);
    }
}