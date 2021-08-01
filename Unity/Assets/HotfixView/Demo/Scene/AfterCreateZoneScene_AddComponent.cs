namespace ET
{
    public class AfterCreateZoneScene_AddComponent: BaseEvent<EventType.AfterCreateZoneScene>
    {
        protected override async ETTask Run(EventType.AfterCreateZoneScene args)
        {
            Scene zoneScene = args.ZoneScene;
            zoneScene.AddComponent<UIEventComponent>();
            zoneScene.AddComponent<UIComponent>();
            await ETTask.CompletedTask;
        }
    }
}