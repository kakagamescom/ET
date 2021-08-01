using System;

namespace ET
{
    /// <summary>
    /// 事件接口
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// 获取事件类型
        /// </summary>
        /// <returns></returns>
        Type GetEventType();
    }

    /// <summary>
    /// 事件抽象基类
    /// </summary>
    /// <typeparam name="A"></typeparam>
    [Event]
    public abstract class BaseEvent<A>: IEvent where A : struct
    {
        public Type GetEventType()
        {
            return typeof (A);
        }

        /// <summary>
        /// 执行事件
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        protected abstract ETTask Run(A a);

        /// <summary>
        /// 处理事件
        /// </summary>
        /// <param name="a"></param>
        public async ETTask Handle(A a)
        {
            try
            {
                await Run(a);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}