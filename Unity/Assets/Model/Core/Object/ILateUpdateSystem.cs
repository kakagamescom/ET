using System;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    public interface ILateUpdateSystem
    {
        Type Type();
        
        void Run(object o);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [ObjectSystem]
    public abstract class LateUpdateSystem<T>: ILateUpdateSystem
    {
        public void Run(object o)
        {
            LateUpdate((T)o);
        }

        public Type Type()
        {
            return typeof(T);
        }

        public abstract void LateUpdate(T self);
    }
}