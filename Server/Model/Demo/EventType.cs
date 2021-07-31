namespace ET
{
    namespace EventType
    {
        /// <summary>
        /// 启动事件
        /// </summary>
        public struct AppStart
        {
        }

        /// <summary>
        /// 调整位置事件
        /// </summary>
        public struct ChangePosition
        {
            public Unit Unit;
        }

        /// <summary>
        /// 调整方位事件
        /// </summary>
        public struct ChangeRotation
        {
            public Unit Unit;
        }

        /// <summary>
        /// 开始移动事件
        /// </summary>
        public struct MoveStart
        {
            public Unit Unit;
        }

        /// <summary>
        /// 停止移动事件
        /// </summary>
        public struct MoveStop
        {
            public Unit Unit;
        }
    }
}