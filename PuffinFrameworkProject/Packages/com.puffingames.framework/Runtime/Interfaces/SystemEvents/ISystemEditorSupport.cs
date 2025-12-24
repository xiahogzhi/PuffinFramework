namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 编辑器支持接口，实现此接口的系统会在编辑器模式下初始化
    /// </summary>
    public interface ISystemEditorSupport : ISystemEvent
    {
        /// <summary>
        /// 编辑器初始化
        /// </summary>
        void OnEditorInitialize();
    }
}
