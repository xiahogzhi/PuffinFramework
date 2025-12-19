namespace Puffin.Runtime.Events.Core
{
    /// <summary>
    /// 事件收集器接口，实现此接口的类可以使用 AddTo 方法绑定事件生命周期
    /// </summary>
    public interface IEventCollector
    {
        /// <summary>
        /// 获取事件收集器实例
        /// </summary>
        /// <returns>事件收集器</returns>
        public EventCollector GetEventCollector();
    }
}