using UnityEngine;

namespace ET
{
    public class LoadingBeginEvent_CreateLoadingUI : BaseEvent<EventType.LoadingBegin>
    {
        protected override async ETTask Run(EventType.LoadingBegin args)
        {
            await UIHelper.Create(args.Scene, UIType.UILoading);
        }
    }
}
