using Cysharp.Threading.Tasks;

namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 异步初始化接口，系统注册后会调用
    /// 适用于需要异步加载资源或进行网络请求的系统
    /// </summary>
    public interface IInitializeAsync : IGameSystemEvent
    {
        /// <summary>
        /// 异步初始化回调
        /// </summary>
        /// <returns>异步任务</returns>
        UniTask OnInitializeAsync();
    }
}
