namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 系统启用/禁用接口，实现此接口的系统可以在运行时动态启用或禁用
    /// 禁用后系统的 Update/FixedUpdate/LateUpdate 将不会被调用
    /// </summary>
    public interface ISystemEnabled : ISystemEvent
    {
        /// <summary>
        /// 系统是否启用
        /// </summary>
        bool Enabled { get; set; }
    }
}
