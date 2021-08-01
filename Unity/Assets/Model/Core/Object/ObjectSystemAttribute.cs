using System;

namespace ET
{
    /// <summary>
    /// 对象标签
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ObjectSystemAttribute: BaseAttribute
    {
    }
}