namespace ET
{
    /// <summary>
    /// 网络消息标签
    /// </summary>
    public class MessageAttribute: BaseAttribute
    {
        /// 消息Id
        public ushort MsgId { get; }

        public MessageAttribute(ushort msgId)
        {
            MsgId = msgId;
        }
    }
}