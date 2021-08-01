using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ET
{
    /// <summary>
    /// 
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