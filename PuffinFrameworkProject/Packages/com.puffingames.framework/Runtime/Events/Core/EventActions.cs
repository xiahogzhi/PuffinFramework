using Cysharp.Threading.Tasks;

namespace Puffin.Runtime.Events.Core
{
    /// <summary>
    /// 事件系统委托定义
    /// </summary>

    /// <summary>
    /// 核心事件处理函数委托（内部使用）
    /// </summary>
    /// <param name="evt">事件数据</param>
    /// <param name="sender">事件发送者</param>
    /// <param name="result">事件注册结果，可用于注销事件</param>
    public delegate void EventFunction(object evt, object sender, EventResult result);

    /// <summary>
    /// 事件拦截器函数委托
    /// </summary>
    /// <param name="e">事件发送包（引用传递，可修改）</param>
    /// <returns>拦截器状态：Next 继续、Break 中断拦截器链、Return 终止事件分发</returns>
    public delegate InterceptorStateEnum EventInterceptorFunction(ref EventSendPackage e);

    /// <summary>
    /// 同步事件处理器委托
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <param name="evt">事件数据</param>
    public delegate void EventHandler<in T>(T evt);

    /// <summary>
    /// 异步事件处理器委托
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <param name="evt">事件数据</param>
    /// <returns>异步任务</returns>
    public delegate UniTask AsyncEventHandler<in T>(T evt);

    /// <summary>
    /// 事件初始化委托，用于初始化 struct 类型的事件
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <param name="evt">事件数据（引用传递）</param>
    public delegate void EventInitializer<T>(ref T evt);
}
