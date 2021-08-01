using System;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class EntityEventAttribute: Attribute
    {
        public int ClassType;

        public EntityEventAttribute(int classType)
        {
            ClassType = classType;
        }
    }
}