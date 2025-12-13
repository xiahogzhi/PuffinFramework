using Cysharp.Threading.Tasks;

namespace Puffin.Runtime.Events.Core
{
    // 核心委托（内部使用）
    public delegate void EventFunction(object evt, object sender, EventResult result);
    public delegate InterceptorStateEnum EventInterceptorFunction(ref EventSendPackage e);

    // 简化的公开委托
    public delegate void EventHandler<in T>(T evt);

    // 异步委托
    public delegate UniTask AsyncEventHandler<in T>(T evt);

    // 事件初始化委托（支持 struct）
    public delegate void EventInitializer<T>(ref T evt);
}
