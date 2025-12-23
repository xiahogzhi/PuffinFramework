namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 应用焦点变化事件接口，在应用获得/失去焦点时调用
    /// 适用于处理窗口切换、音频暂停等操作
    /// </summary>
    public interface ISystemApplicationFocusChanged : ISystemEvent
    {
        /// <summary>
        /// 应用焦点变化时回调
        /// </summary>
        /// <param name="hasFocus">是否拥有焦点</param>
        void OnApplicationFocus(bool hasFocus);
    }
}