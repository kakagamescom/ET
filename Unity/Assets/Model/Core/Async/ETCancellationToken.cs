using System;
using System.Collections.Generic;

namespace ET
{
    /// <summary>
    /// 
    /// </summary>
    public class ETCancellationToken
    {
        private HashSet<Action> _actions = new HashSet<Action>();

        public void Add(Action callback)
        {
            // 如果action是null，绝对不能添加,要抛异常，说明有协程泄漏
            _actions.Add(callback);
        }

        public void Remove(Action callback)
        {
            _actions?.Remove(callback);
        }

        public bool IsCancel()
        {
            return _actions == null;
        }

        public void Cancel()
        {
            if (_actions == null)
            {
                return;
            }

            Invoke();
        }

        private void Invoke()
        {
            HashSet<Action> runActions = _actions;
            _actions = null;
            try
            {
                foreach (Action action in runActions)
                {
                    action.Invoke();
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public async ETVoid CancelAfter(long afterTimeCancel)
        {
            if (_actions == null)
            {
                return;
            }

            await TimerComponent.Instance.WaitAsync(afterTimeCancel);

            Invoke();
        }
    }
}