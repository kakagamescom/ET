using System.Collections.Generic;

namespace ET
{
    /// <summary>
    /// 消息分发组件
    /// </summary>
    public class MessageDispatcherComponent: Entity
    {
        public static MessageDispatcherComponent Instance { get; set; }

        public readonly Dictionary<ushort, List<IMessageHandler>> Handlers = new Dictionary<ushort, List<IMessageHandler>>();
    }
}