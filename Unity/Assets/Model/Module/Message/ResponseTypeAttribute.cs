using System;

namespace ET
{
    /// <summary>
    /// 消息响应类型标签
    /// </summary>
    public class ResponseTypeAttribute: BaseAttribute
    {
        public Type Type { get; }

        public ResponseTypeAttribute(Type type)
        {
            Type = type;
        }
    }
}