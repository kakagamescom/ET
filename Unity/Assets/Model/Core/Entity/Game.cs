using System;
using System.Collections.Generic;

namespace ET
{
    public static class Game
    {
        public static ThreadSynchronizationContext ThreadSynchronizationContext => ThreadSynchronizationContext.Instance;

        public static TimeInfo TimeInfo => TimeInfo.Instance;

        public static EventSystem EventSystem => EventSystem.Instance;

        private static Scene _scene;

        public static Scene Scene
        {
            get
            {
                if (_scene != null)
                {
                    return _scene;
                }

                _scene = EntitySceneFactory.CreateScene(IdGenerater.Instance.GenerateInstanceId(), 0, SceneType.Process, "Process");
                return _scene;
            }
        }

        public static ObjectPool ObjectPool => ObjectPool.Instance;

        public static IdGenerater IdGenerater => IdGenerater.Instance;

        public static Options Options;

        public static ILog ILog;

        public static List<Action> FrameFinishCallback = new List<Action>();

        public static void Update()
        {
            ThreadSynchronizationContext.Update();
            TimeInfo.Update();
            EventSystem.Update();
        }

        public static void LateUpdate()
        {
            EventSystem.LateUpdate();
        }

        public static void FrameFinish()
        {
            foreach (Action action in FrameFinishCallback)
            {
                action.Invoke();
            }

            FrameFinishCallback.Clear();
        }

        public static void Close()
        {
            _scene?.Dispose();
            _scene = null;
            ObjectPool.Instance.Dispose();
            EventSystem.Instance.Dispose();
            IdGenerater.Instance.Dispose();
        }
    }
}