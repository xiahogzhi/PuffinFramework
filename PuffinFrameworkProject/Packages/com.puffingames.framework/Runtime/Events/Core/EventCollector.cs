using System.Collections.Generic;

namespace Puffin.Runtime.Events.Core
{
    /// <summary>
    /// 事件收集器，用于统一管理和销毁多个事件注册
    /// 适用于需要批量注销事件的场景，如组件销毁时
    /// </summary>
    public class EventCollector
    {
        private List<EventResult> _destroyHandles;

        /// <summary>
        /// 添加事件注册结果到收集器
        /// </summary>
        /// <param name="handle">事件注册结果</param>
        public void Add(EventResult handle)
        {
            if (_destroyHandles == null)
            {
                _destroyHandles = new List<EventResult>();
            }

            _destroyHandles.Add(handle);
        }

        /// <summary>
        /// 销毁所有收集的事件注册
        /// </summary>
        public void Destroy()
        {
            if (_destroyHandles == null)
                return;

            for (var i = 0; i < _destroyHandles.Count; i++)
                _destroyHandles[i].Destroy();

            _destroyHandles.Clear();
        }
    }
}