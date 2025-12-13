using UnityEngine;

namespace Puffin.Runtime.Events.Core
{
    /// <summary>
    /// 事件结果销毁器组件
    /// </summary>
    internal class EventResultDestroyer : MonoBehaviour
    {
        private readonly EventCollector _collector = new();

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
