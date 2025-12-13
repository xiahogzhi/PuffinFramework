using System.Collections.Generic;

namespace Puffin.Runtime.Events.Core
{
    public class EventCollector
    {
        private List<EventResult> _destroyHandles;

        public void Add(EventResult handle)
        {
            if (_destroyHandles == null)
            {
                _destroyHandles = new List<EventResult>();
            }

            _destroyHandles.Add(handle);
        }

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