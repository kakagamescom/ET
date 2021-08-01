using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ET
{
    /// <summary>
    /// 线程上下文, 线程间通信
    /// </summary>
    public class ThreadSyncContext: SynchronizationContext
    {
        public static ThreadSyncContext Instance { get; } = new ThreadSyncContext(Thread.CurrentThread.ManagedThreadId);

        private readonly int _threadId;

        // 线程同步队列,发送接收socket回调都放到该队列,由poll线程统一执行
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        private Action _action;

        public ThreadSyncContext(int threadId)
        {
            _threadId = threadId;
        }

        public void Update()
        {
            while (true)
            {
                if (!_queue.TryDequeue(out _action))
                {
                    return;
                }

                try
                {
                    _action();
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            Post(() => callback(state));
        }

        public void Post(Action action)
        {
            if (Thread.CurrentThread.ManagedThreadId == _threadId)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }

                return;
            }

            _queue.Enqueue(action);
        }

        public void PostNext(Action action)
        {
            _queue.Enqueue(action);
        }
    }
}