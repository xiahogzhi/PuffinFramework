using UnityEngine;

namespace Puffin.Runtime.Events.Core
{
    /// <summary>
    /// 事件结果销毁器组件，自动挂载到 GameObject 上
    /// 当 GameObject 销毁时自动注销所有绑定的事件
    /// </summary>
    internal class EventResultDestroyer : MonoBehaviour
    {
        private readonly EventCollector _collector = new();

        /// <summary>
        /// 添加事件注册结果，将在 GameObject 销毁时自动注销
        /// </summary>
        /// <param name="handle">事件注册结果</param>
        public void Add(EventResult handle)
        {
            _collector.Add(handle);
        }

        private void OnDestroy()
        {
            _collector.Destroy();
        }
    }
}
