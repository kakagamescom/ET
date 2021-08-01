using System;

namespace ET
{
    public class TimeInfo: IDisposable
    {
        public static TimeInfo Instance = new TimeInfo();
        
        private readonly DateTime _dt1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private DateTime _dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private int _timeZone;

        public int TimeZone
        {
            get
            {
                return _timeZone;
            }
            set
            {
                _timeZone = value;
                _dt = _dt1970.AddHours(TimeZone);
            }
        }

        public long ServerMinusClientTime { private get; set; }

        public long FrameTime;

        private TimeInfo()
        {
            FrameTime = ClientNow();
        }

        public void Update()
        {
            FrameTime = ClientNow();
        }

        /// <summary> 
        /// 根据时间戳获取时间 
        /// </summary>  
        public DateTime ToDateTime(long timeStamp)
        {
            return _dt.AddTicks(timeStamp * 10000);
        }

        // 线程安全
        public long ClientNow()
        {
            return (DateTime.Now.Ticks - _dt1970.Ticks) / 10000;
        }

        public long ServerNow()
        {
            return ClientNow() + Game.TimeInfo.ServerMinusClientTime;
        }

        public long ClientFrameTime()
        {
            return FrameTime;
        }

        public long ServerFrameTime()
        {
            return FrameTime + Game.TimeInfo.ServerMinusClientTime;
        }

        public long Transition(DateTime d)
        {
            return (d.Ticks - _dt.Ticks) / 10000;
        }

        public void Dispose()
        {
            Instance = null;
        }
    }
}