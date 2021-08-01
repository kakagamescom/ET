namespace ET
{
    /// <summary>
    /// 消息接口
    /// </summary>
    public interface IMessage
    {
    }

    /// <summary>
    /// 请求消息接口
    /// </summary>
    public interface IRequest: IMessage
    {
        int RpcId { get; set; }
    }

    /// <summary>
    /// 响应消息接口
    /// </summary>
    public interface IResponse: IMessage
    {
        int Error { get; set; }

        string Message { get; set; }

        int RpcId { get; set; }
    }

    /// <summary>
    /// 错误响应消息接口
    /// </summary>
    public class ErrorResponse: IResponse
    {
        public int Error { get; set; }

        public string Message { get; set; }

        public int RpcId { get; set; }
    }
}