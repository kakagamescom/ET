namespace ET
{
    public class LoadingFinishEvent_RemoveLoadingUI : BaseEvent<EventType.LoadingFinish>
    {
        protected override async ETTask Run(EventType.LoadingFinish args)
        {
            await UIHelper.Create(args.Scene, UIType.UILoading);
        }
    }
}
