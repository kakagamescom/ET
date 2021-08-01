namespace ET
{
    /// <summary>
    /// 网络消息标签
    /// </summary>
    public class MessageAttribute: BaseAttribute
    {
        public ushort Opcode { get; }

        public MessageAttribute(ushort opcode)
        {
            Opcode = opcode;
        }
    }
}