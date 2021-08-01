using System.Collections.Generic;

namespace ET
{
    /// <summary>
    /// 网络消息Id辅助工具类
    /// </summary>
    public static class MsgIdHelper
    {
        private static readonly HashSet<ushort> ignoreDebugLogMessageSet = new HashSet<ushort>
        {
            OuterOpcode.C2G_Ping, 
            OuterOpcode.G2C_Ping,
        };

        private static bool IsNeedLogMessage(ushort msgId)
        {
            if (ignoreDebugLogMessageSet.Contains(msgId))
            {
                return false;
            }

            return true;
        }

        public static bool IsOuterMessage(ushort msgId)
        {
            return msgId >= 20000;
        }

        public static bool IsInnerMessage(ushort msgId)
        {
            return msgId < 20000;
        }

        public static void LogMsg(int zone, ushort msgId, object message)
        {
            if (!IsNeedLogMessage(msgId))
            {
                return;
            }

            Game.ILog.Debug("zone: {0} {1}", zone, message);
        }

        public static void LogMsg(ushort msgId, long actorId, object message)
        {
            if (!IsNeedLogMessage(msgId))
            {
                return;
            }

            Game.ILog.Debug("actorId: {0} {1}", actorId, message);
        }
    }
}