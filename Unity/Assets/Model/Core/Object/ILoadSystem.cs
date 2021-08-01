using System;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    public interface ILoadSystem
    {
        Type Type();
        
        void Run(object o);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [ObjectSystem]
    public abstract class LoadSystem<T>: ILoadSystem
    {
        public void Run(object o)
        {
            Load((T)o);
        }

        public Type Type()
        {
            return typeof(T);
        }

        public abstract void Load(T self);
    }
}