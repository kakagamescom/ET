using System;
using System.Runtime.InteropServices;

namespace ET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct IdStruct
    {
        public uint Time;    // 30bit
        public int Process;  // 18bit
        public ushort Value; // 16bit

        public long ToLong()
        {
            ulong result = 0;
            result |= Value;
            result |= (ulong)Process << 16;
            result |= (ulong)Time << 34;
            return (long)result;
        }

        public IdStruct(uint time, int process, ushort value)
        {
            Process = process;
            Time = time;
            Value = value;
        }

        public IdStruct(long id)
        {
            ulong result = (ulong)id;
            Value = (ushort)(result & ushort.MaxValue);
            result >>= 16;
            Process = (int)(result & IdGenerater.Mask18bit);
            result >>= 18;
            Time = (uint)result;
        }

        public override string ToString()
        {
            return $"process: {Process}, time: {Time}, value: {Value}";
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct InstanceIdStruct
    {
        public uint Time;   // 当年开始的tick 28bit
        public int Process; // 18bit
        public uint Value;  // 18bit

        public long ToLong()
        {
            ulong result = 0;
            result |= Value;
            result |= (ulong)Process << 18;
            result |= (ulong)Time << 36;
            return (long)result;
        }

        public InstanceIdStruct(long id)
        {
            ulong result = (ulong)id;
            Value = (uint)(result & IdGenerater.Mask18bit);
            result >>= 18;
            Process = (int)(result & IdGenerater.Mask18bit);
            result >>= 18;
            Time = (uint)result;
        }

        public InstanceIdStruct(uint time, int process, uint value)
        {
            Time = time;
            Process = process;
            Value = value;
        }

        // 给SceneId使用
        public InstanceIdStruct(int process, uint value)
        {
            Time = 0;
            Process = process;
            Value = value;
        }

        public override string ToString()
        {
            return $"process: {Process}, value: {Value} time: {Time}";
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UnitIdStruct
    {
        public uint Time;        // 30bit 34年
        public ushort Zone;      // 10bit 1024个区
        public byte ProcessMode; // 8bit  Process % 256  一个区最多256个进程
        public ushort Value;     // 16bit 每秒每个进程最大16K个Unit

        public long ToLong()
        {
            ulong result = 0;
            result |= Value;
            result |= (uint)ProcessMode << 16;
            result |= (ulong)Zone << 24;
            result |= (ulong)Time << 34;
            return (long)result;
        }

        public UnitIdStruct(int zone, int process, uint time, ushort value)
        {
            Time = time;
            ProcessMode = (byte)(process % 256);
            Value = value;
            Zone = (ushort)zone;
        }

        public UnitIdStruct(long id)
        {
            ulong result = (ulong)id;
            Value = (ushort)(result & ushort.MaxValue);
            result >>= 16;
            ProcessMode = (byte)(result & byte.MaxValue);
            result >>= 8;
            Zone = (ushort)(result & 0x03ff);
            result >>= 10;
            Time = (uint)result;
        }

        public override string ToString()
        {
            return $"ProcessMode: {ProcessMode}, value: {Value} time: {Time}";
        }

        public static int GetUnitZone(long unitId)
        {
            int v = (int)((unitId >> 24) & 0x03ff); // 取出10bit
            return v;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class IdGenerater: IDisposable
    {
        public const int Mask18bit = 0x03ffff;
        public static IdGenerater Instance = new IdGenerater();

        public const int MaxZone = 1024;

        private long _epoch2020;
        private ushort _value;
        private uint _lastIdTime;

        private long _epochThisYear;
        private uint _instanceIdValue;
        private uint _lastInstanceIdTime;

        private ushort _unitIdValue;
        private uint _lastUnitIdTime;

        public IdGenerater()
        {
            long epoch1970tick = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks / 10000;
            _epoch2020 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks / 10000 - epoch1970tick;
            _epochThisYear = new DateTime(DateTime.Now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks / 10000 - epoch1970tick;

            _lastInstanceIdTime = TimeSinceThisYear();
            if (_lastInstanceIdTime <= 0)
            {
                Log.Warning($"lastInstanceIdTime less than 0: {_lastInstanceIdTime}");
                _lastInstanceIdTime = 1;
            }

            _lastIdTime = TimeSince2020();
            if (_lastIdTime <= 0)
            {
                Log.Warning($"lastIdTime less than 0: {_lastIdTime}");
                _lastIdTime = 1;
            }

            _lastUnitIdTime = TimeSince2020();
            if (_lastUnitIdTime <= 0)
            {
                Log.Warning($"lastUnitIdTime less than 0: {_lastUnitIdTime}");
                _lastUnitIdTime = 1;
            }
        }

        public void Dispose()
        {
            _epoch2020 = 0;
            _epochThisYear = 0;
            _value = 0;
        }

        private uint TimeSince2020()
        {
            uint a = (uint)((Game.TimeInfo.FrameTime - _epoch2020) / 1000);
            return a;
        }

        private uint TimeSinceThisYear()
        {
            uint a = (uint)((Game.TimeInfo.FrameTime - _epochThisYear) / 1000);
            return a;
        }

        public long GenerateInstanceId()
        {
            uint time = TimeSinceThisYear();

            if (time > _lastInstanceIdTime)
            {
                _lastInstanceIdTime = time;
                _instanceIdValue = 0;
            }
            else
            {
                ++_instanceIdValue;

                if (_instanceIdValue > IdGenerater.Mask18bit - 1) // 18bit
                {
                    ++_lastInstanceIdTime; // 借用下一秒
                    _instanceIdValue = 0;
#if NOT_UNITY
                    Log.Error($"instanceid count per sec overflow: {time} {_lastInstanceIdTime}");
#endif
                }
            }

            InstanceIdStruct instanceIdStruct = new InstanceIdStruct(_lastInstanceIdTime, Game.Options.Process, _instanceIdValue);
            return instanceIdStruct.ToLong();
        }

        public long GenerateId()
        {
            uint time = TimeSince2020();

            if (time > _lastIdTime)
            {
                _lastIdTime = time;
                _value = 0;
            }
            else
            {
                ++_value;

                if (_value > ushort.MaxValue - 1)
                {
                    _value = 0;
                    ++_lastIdTime; // 借用下一秒
                    Log.Error($"id count per sec overflow: {time} {_lastIdTime}");
                }
            }

            IdStruct idStruct = new IdStruct(_lastIdTime, Game.Options.Process, _value);
            return idStruct.ToLong();
        }

        public long GenerateUnitId(int zone)
        {
            if (zone > MaxZone)
            {
                throw new Exception($"zone > MaxZone: {zone}");
            }

            uint time = TimeSince2020();

            if (time > _lastUnitIdTime)
            {
                _lastUnitIdTime = time;
                _unitIdValue = 0;
            }
            else
            {
                ++_unitIdValue;

                if (_unitIdValue > ushort.MaxValue - 1)
                {
                    _unitIdValue = 0;
                    ++_lastUnitIdTime; // 借用下一秒
                    Log.Error($"unitid count per sec overflow: {time} {_lastUnitIdTime}");
                }
            }

            UnitIdStruct unitIdStruct = new UnitIdStruct(zone, Game.Options.Process, _lastUnitIdTime, _unitIdValue);
            return unitIdStruct.ToLong();
        }
    }
}