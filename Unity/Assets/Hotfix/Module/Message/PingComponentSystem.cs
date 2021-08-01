using System;

namespace ET
{
    [ObjectSystem]
    public class PingComponentAwakeSystem: AwakeSystem<PingComponent>
    {
        public override void Awake(PingComponent self)
        {
            PingAsync(self).Coroutine();
        }

        private static async ETVoid PingAsync(PingComponent self)
        {
            Session session = self.GetParent<Session>();
            long instanceId = self.InstanceId;
            
            while (true)
            {
                if (self.InstanceId != instanceId)
                {
                    return;
                }

                long time1 = TimeHelper.ClientNow();
                try
                {
                    G2C_Ping response = await session.Call(self.C2G_Ping) as G2C_Ping;

                    if (self.InstanceId != instanceId)
                    {
                        return;
                    }

                    long time2 = TimeHelper.ClientNow();
                    self.Ping = time2 - time1;
                    
                    Game.TimeInfo.ServerMinusClientTime = response.Time + (time2 - time1) / 2 - time2;

                    await TimerComponent.Instance.WaitAsync(2000);
                }
                catch (RpcException ex)
                {
                    // session断开导致ping rpc报错，记录一下即可，不需要打成error
                    Log.Info($"ping error: {self.Id} {ex.Error}");
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error($"ping error: \n{ex}");
                }
            }
        }
    }

    [ObjectSystem]
    public class PingComponentDestroySystem: DestroySystem<PingComponent>
    {
        public override void Destroy(PingComponent self)
        {
            self.Ping = default;
        }
    }
}