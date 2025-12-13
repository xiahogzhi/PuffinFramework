namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 系统启用/禁用接口，实现此接口的系统可以被临时禁用
    /// </summary>
    public interface ISystemEnabled : IGameSystemEvent
    {
        bool Enabled { get; set; }
    }
}
