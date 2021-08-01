using System;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    public interface IDestroySystem
    {
        Type Type();
        
        void Run(object o);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [ObjectSystem]
    public abstract class DestroySystem<T>: IDestroySystem
    {
        public void Run(object o)
        {
            Destroy((T)o);
        }

        public Type Type()
        {
            return typeof(T);
        }

        public abstract void Destroy(T self);
    }
}