using System;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    public interface IUpdateSystem
    {
        Type Type();
        void Run(object o);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [ObjectSystem]
    public abstract class UpdateSystem<T>: IUpdateSystem
    {
        public void Run(object o)
        {
            Update((T)o);
        }

        public Type Type()
        {
            return typeof(T);
        }

        public abstract void Update(T self);
    }
}