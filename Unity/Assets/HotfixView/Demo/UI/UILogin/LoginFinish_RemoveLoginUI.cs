

namespace ET
{
	public class LoginFinish_RemoveLoginUI: BaseEvent<EventType.LoginFinish>
	{
		protected override async ETTask Run(EventType.LoginFinish args)
		{
			await UIHelper.Remove(args.ZoneScene, UIType.UILogin);
		}
	}
}
