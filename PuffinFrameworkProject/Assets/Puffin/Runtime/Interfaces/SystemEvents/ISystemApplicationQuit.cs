namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 应用退出事件接口，在应用退出时调用
    /// 适用于保存数据、释放资源等清理操作
    /// </summary>
    public interface ISystemApplicationQuit : ISystemEvent
    {
        /// <summary>
        /// 应用退出时回调
        /// </summary>
        void OnApplicationQuit();
    }
}