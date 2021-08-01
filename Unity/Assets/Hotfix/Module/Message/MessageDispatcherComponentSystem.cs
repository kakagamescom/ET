using System;
using System.Collections.Generic;

namespace ET
{
    [ObjectSystem]
    public class MessageDispatcherComponentAwakeSystem: AwakeSystem<MessageDispatcherComponent>
    {
        public override void Awake(MessageDispatcherComponent self)
        {
            MessageDispatcherComponent.Instance = self;
            self.Load();
        }
    }

    [ObjectSystem]
    public class MessageDispatcherComponentLoadSystem: LoadSystem<MessageDispatcherComponent>
    {
        public override void Load(MessageDispatcherComponent self)
        {
            self.Load();
        }
    }

    [ObjectSystem]
    public class MessageDispatcherComponentDestroySystem: DestroySystem<MessageDispatcherComponent>
    {
        public override void Destroy(MessageDispatcherComponent self)
        {
            MessageDispatcherComponent.Instance = null;
            self.Handlers.Clear();
        }
    }

    /// <summary>
    /// 消息分发组件扩展
    /// </summary>
    public static class MessageDispatcherComponentExtension
    {
        public static void Load(this MessageDispatcherComponent self)
        {
            self.Handlers.Clear();

            // 为所有使用MessageHandler标签标记的类型创建处理器
            HashSet<Type> types = Game.EventSystem.GetTypes(typeof(MessageHandlerAttribute));
            foreach (Type type in types)
            {
                // 创建消息处理器
                IMessageHandler messageHandler = Activator.CreateInstance(type) as IMessageHandler;
                if (messageHandler == null)
                {
                    Log.Error($"message handle {type.Name} must inhert from IMessageHandler");
                    continue;
                }

                // 获取消息Id
                Type messageType = messageHandler.GetMessageType();
                ushort msgId = MsgIdTypeComponent.Instance.GetMsgId(messageType);
                if (msgId == 0)
                {
                    Log.Error($"msg id cannot be 0: {messageType.Name}");
                    continue;
                }

                // 注册消息处理器
                self.RegisterHandler(msgId, messageHandler);
            }
        }

        /// <summary>
        /// 注册消息处理器
        /// </summary>
        /// <param name="self"></param>
        /// <param name="msgId"></param>
        /// <param name="handler"></param>
        public static void RegisterHandler(this MessageDispatcherComponent self, ushort msgId, IMessageHandler handler)
        {
            if (!self.Handlers.ContainsKey(msgId))
            {
                self.Handlers.Add(msgId, new List<IMessageHandler>());
            }

            self.Handlers[msgId].Add(handler);
        }

        /// <summary>
        /// 处理消息
        /// </summary>
        /// <param name="self"></param>
        /// <param name="session"></param>
        /// <param name="msgId"></param>
        /// <param name="message"></param>
        public static void Handle(this MessageDispatcherComponent self, Session session, ushort msgId, object message)
        {
            // 获取处理器
            if (!self.Handlers.TryGetValue(msgId, out List<IMessageHandler> actions))
            {
                Log.Error($"message not handle: {msgId} {message}");
                return;
            }

            // 处理消息
            foreach (IMessageHandler messageHandler in actions)
            {
                try
                {
                    messageHandler.Handle(session, message);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }
    }
}