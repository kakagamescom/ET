namespace ET
{
    public class NetKcpComponent: Entity
    {
        public NetService Service;

        public IMessageDispatcher MessageDispatcher { get; set; }
    }
}