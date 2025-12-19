namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 应用暂停事件接口，在应用暂停/恢复时调用
    /// 适用于暂停游戏逻辑、保存状态等操作
    /// </summary>
    public interface IApplicationPause : IGameSystemEvent
    {
        /// <summary>
        /// 应用暂停状态变化时回调
        /// </summary>
        /// <param name="pause">是否暂停</param>
        void OnApplicationPause(bool pause);
    }
}
