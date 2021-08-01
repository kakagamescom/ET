namespace ET
{
    public class NetKcpComponent: Entity
    {
        public BaseService Service;

        public IMessageDispatcher MessageDispatcher { get; set; }
    }
}