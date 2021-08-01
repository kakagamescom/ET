using System.Collections.Generic;

namespace ET
{
    /// <summary>
    /// 网络线程组件
    /// </summary>
    public class NetThreadComponent: Entity
    {
        public static NetThreadComponent Instance;

        public const int checkInteral = 2000;
        public const int recvMaxIdleTime = 60000;
        public const int sendMaxIdleTime = 60000;

        public ThreadSyncContext ThreadSyncContext;

        public HashSet<NetService> Services = new HashSet<NetService>();
    }
}