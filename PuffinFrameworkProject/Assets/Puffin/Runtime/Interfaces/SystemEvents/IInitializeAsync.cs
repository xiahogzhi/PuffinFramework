using Cysharp.Threading.Tasks;

namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 异步初始化接口，系统注册后会调用
    /// </summary>
    public interface IInitializeAsync : IGameSystemEvent
    {
        UniTask OnInitializeAsync();
    }
}
