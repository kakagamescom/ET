

namespace ET
{
	public class LoginFinish_CreateLobbyUI: BaseEvent<EventType.LoginFinish>
	{
		protected override async ETTask Run(EventType.LoginFinish args)
		{
			await UIHelper.Create(args.ZoneScene, UIType.UILobby);
		}
	}
}
