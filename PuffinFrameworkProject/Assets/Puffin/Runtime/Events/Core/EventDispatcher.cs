using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Puffin.Runtime.Events.Interfaces;
using Puffin.Runtime.Tools;

namespace Puffin.Runtime.Events.Core
{
    public enum InterceptorStateEnum
    {
        Next,
        Break,
        Return,
    }

    /// <summary>
    /// 注册绑定事件源
    /// </summary>
    public struct EventSource
    {
        public uint id { get; private set; }
        public Type eventType { get; private set; }
        public EventFunction function { get; private set; }
        public int priority { get; private set; }
        public bool isOnce { get; private set; }

        public EventSource(uint id, Type eventType, EventFunction function, int priority = 0, bool isOnce = false)
        {
            this.id = id;
            this.eventType = eventType;
            this.function = function;
            this.priority = priority;
            this.isOnce = isOnce;
        }
    }

    /// <summary>
    /// 事件注册结果，支持链式设置
    /// </summary>
    public struct EventResult
    {
        public uint id { get; private set; }
        private EventDispatcher _dispatcher;

        internal EventResult(uint id, EventDispatcher dispatcher)
        {
            this.id = id;
            _dispatcher = dispatcher;
        }

        /// <summary>设置优先级（数值越大越先执行）</summary>
        public EventResult Priority(int priority)
        {
            _dispatcher?.UpdatePriority(id, priority);
            return this;
        }

        /// <summary>设置为一次性事件（触发后自动注销）</summary>
        public EventResult Once()
        {
            _dispatcher?.SetOnce(id, true);
            return this;
        }

        /// <summary>立即调用一次处理器</summary>
        public EventResult InvokeNow()
        {
            _dispatcher?.InvokeHandler(id);
            return this;
        }

        /// <summary>添加到事件收集器</summary>
        public EventResult AddTo(EventCollector collector)
        {
            collector.Add(this);
            return this;
        }

        /// <summary>添加到实现 IEventCollector 的对象</summary>
        public EventResult AddTo(IEventCollector collector)
        {
            collector.GetEventCollector().Add(this);
            return this;
        }

        /// <summary>绑定到 GameObject 生命周期，GameObject 销毁时自动注销事件</summary>
        public EventResult AddTo(UnityEngine.GameObject gameObject)
        {
            if (gameObject == null)
            {
                Destroy();
                return this;
            }

            var destroyer = gameObject.GetComponent<EventResultDestroyer>();
            if (destroyer == null)
                destroyer = gameObject.AddComponent<EventResultDestroyer>();

            destroyer.Add(this);
            return this;
        }

        /// <summary>绑定到 Component 生命周期</summary>
        public EventResult AddTo(UnityEngine.Component component)
        {
            return AddTo(component?.gameObject);
        }

        /// <summary>销毁/注销事件</summary>
        public void Destroy()
        {
            if (id == 0) return;
            _dispatcher?.UnRegister(id);
            _dispatcher = null;
            id = 0;
        }
    }

    /// <summary>
    /// 拦截器返回结果
    /// </summary>
    public struct InterceptorResult
    {
        public uint id { get; private set; }
        public EventDispatcher dispatcher { get; private set; }

        public InterceptorResult(uint id, EventDispatcher dispatcher)
        {
            this.id = id;
            this.dispatcher = dispatcher;
        }

        public void Destroy() => dispatcher?.RemoveInterceptor(id);
    }

    /// <summary>
    /// 事件发送包
    /// </summary>
    public struct EventSendPackage
    {
        public object eventData { get; set; }
        public object sender { get; set; }
        public Type eventType { get; set; }

        public EventSendPackage(object eventData, object sender, Type eventType)
        {
            this.eventData = eventData;
            this.sender = sender;
            this.eventType = eventType;
        }
    }

    /// <summary>
    /// 事件拦截器
    /// </summary>
    public struct EventInterceptor
    {
        public EventInterceptorFunction interceptor { get; private set; }
        public EventDispatcher dispatcher { get; private set; }
        public string name { get; private set; }
        public Type type { get; set; }
        public uint id { get; set; }
        public int priority { get; set; }

        public EventInterceptor(EventInterceptorFunction interceptor, EventDispatcher dispatcher,
            string name, int priority, uint id, Type type)
        {
            this.interceptor = interceptor;
            this.dispatcher = dispatcher;
            this.name = name;
            this.priority = priority;
            this.id = id;
            this.type = type;
        }

        public void Destroy() => dispatcher?.RemoveInterceptor(id);
    }

    /// <summary>
    /// 事件处理器
    /// </summary>
    public class EventDispatcher
    {
        private uint _eventIdGen;
        private uint _interceptorIdGen;
        private bool _isOperating;

        private readonly Dictionary<Type, List<uint>> _eventRegisterMap = new();
        private readonly Dictionary<uint, EventSource> _eventRegistry = new();
        private readonly Dictionary<Type, List<uint>> _interceptorRegisterMap = new();
        private readonly Dictionary<uint, EventInterceptor> _interceptorRegistry = new();
        private readonly Dictionary<Type, object> _defaultEventCache = new();
        private readonly Queue<Operation> _operations = new(256);
        private readonly List<uint> _pendingRemove = new(16);

        private enum OperationType
        {
            Register,
            UnRegister,
            AddInterceptor,
            RemoveInterceptor,
            Send,
        }

        private struct Operation
        {
            public OperationType type;
            public uint id;
            public EventSendPackage sendPackage;
            public EventSource eventSource;
            public EventInterceptor interceptor;
        }

        #region Register

        /// <summary>
        /// 注册同步事件
        /// <code>
        /// dispatcher.Register&lt;MyEvent&gt;(e => Handle(e))
        ///     .Priority(100)
        ///     .Once();
        /// </code>
        /// </summary>
        public EventResult Register<T>(EventHandler<T> handler) where T : IEventDefine
        {
            return RegisterInternal(typeof(T), (evt, _, _) => handler((T)evt), 0, false);
        }

        /// <summary>
        /// 注册异步事件
        /// <code>
        /// dispatcher.Register&lt;MyEvent&gt;(async e => await HandleAsync(e))
        ///     .Priority(100)
        ///     .Once();
        /// </code>
        /// </summary>
        public EventResult Register<T>(AsyncEventHandler<T> handler) where T : IEventDefine
        {
            return RegisterInternal(typeof(T), (evt, _, _) => handler((T)evt).Forget(), 0, false);
        }

        internal void UpdatePriority(uint id, int priority)
        {
            if (!_eventRegistry.TryGetValue(id, out var source)) return;
            var newSource = new EventSource(source.id, source.eventType, source.function, priority, source.isOnce);
            _eventRegistry[id] = newSource;
            ResortHandlers(source.eventType);
        }

        internal void SetOnce(uint id, bool once)
        {
            if (!_eventRegistry.TryGetValue(id, out var source)) return;
            var newSource = new EventSource(source.id, source.eventType, source.function, source.priority, once);
            _eventRegistry[id] = newSource;
        }

        internal void InvokeHandler(uint id)
        {
            if (!_eventRegistry.TryGetValue(id, out var source)) return;
            var evt = GetOrCreateDefaultEvent(source.eventType);
            try
            {
                source.function(evt, null, new EventResult(id, this));
                if (source.isOnce)
                    DoUnRegister(id);
            }
            catch (Exception e)
            {
                Log.Exception(e);
            }
        }

        private void ResortHandlers(Type eventType)
        {
            if (!_eventRegisterMap.TryGetValue(eventType, out var list)) return;
            list.Sort((a, b) =>
            {
                var pa = _eventRegistry.TryGetValue(a, out var sa) ? sa.priority : 0;
                var pb = _eventRegistry.TryGetValue(b, out var sb) ? sb.priority : 0;
                return pb.CompareTo(pa);
            });
        }

        private EventResult RegisterInternal(Type eventType, EventFunction handler, int priority, bool isOnce)
        {
            if (handler == null || eventType == null)
                throw new ArgumentNullException($"注册事件失败 => {eventType?.Name}");

            var id = ++_eventIdGen;
            var source = new EventSource(id, eventType, handler, priority, isOnce);
            PutOperation(new Operation { type = OperationType.Register, eventSource = source });
            return new EventResult(id, this);
        }

        #endregion

        #region Send

        /// <summary>
        /// 发送事件
        /// </summary>
        public void Send<T>(T evt, object sender = null) where T : IEventDefine
        {
            SendInternal(typeof(T), evt, sender);
        }

        /// <summary>
        /// 发送事件（带初始化器，支持 struct）
        /// </summary>
        public void Send<T>(EventInitializer<T> initializer, object sender = null) where T : IEventDefine, new()
        {
            var evt = new T();
            initializer?.Invoke(ref evt);
            SendInternal(typeof(T), evt, sender);
        }

        /// <summary>
        /// 发送默认事件（无参数）
        /// </summary>
        public void SendDefault<T>(object sender = null) where T : IEventDefine
        {
            SendInternal(typeof(T), null, sender);
        }

        /// <summary>
        /// 发送事件（非泛型）
        /// </summary>
        public void Send(IEventDefine evt, object sender = null)
        {
            if (evt == null)
            {
                Log.Warning("发送空事件");
                return;
            }

            SendInternal(evt.GetType(), evt, sender);
        }

        private void SendInternal(Type eventType, object evt, object sender)
        {
            if (eventType == null) return;
            evt ??= GetOrCreateDefaultEvent(eventType);
            PutOperation(new Operation
            {
                type = OperationType.Send,
                sendPackage = new EventSendPackage(evt, sender, eventType)
            });
        }

        #endregion

        #region Interceptor

        /// <summary>
        /// 添加事件拦截器
        /// </summary>
        public uint AddInterceptor<T>(EventInterceptorFunction interceptor, string name = "拦截器", int priority = 0)
            where T : IEventDefine
        {
            var id = ++_interceptorIdGen;
            var ei = new EventInterceptor(interceptor, this, name, priority, id, typeof(T));
            PutOperation(new Operation { type = OperationType.AddInterceptor, interceptor = ei });
            return id;
        }

        public void RemoveInterceptor(uint id)
        {
            PutOperation(new Operation { type = OperationType.RemoveInterceptor, id = id });
        }

        #endregion

        #region UnRegister

        public void UnRegister(uint id)
        {
            PutOperation(new Operation { type = OperationType.UnRegister, id = id });
        }

        public void Reset()
        {
            _eventRegisterMap.Clear();
            _eventRegistry.Clear();
            _interceptorRegistry.Clear();
            _interceptorRegisterMap.Clear();
            _eventIdGen = 0;
            _interceptorIdGen = 0;
            _isOperating = false;
            _operations.Clear();
            _defaultEventCache.Clear();
        }

        #endregion

        #region Internal

        private void PutOperation(Operation op)
        {
            if (!_isOperating)
                ExecuteOperation(op);
            else
                _operations.Enqueue(op);
        }

        private void ExecuteOperation(Operation op)
        {
            if (_isOperating) return;
            _isOperating = true;

            try
            {
                switch (op.type)
                {
                    case OperationType.Register:
                        DoRegister(op.eventSource);
                        break;
                    case OperationType.UnRegister:
                        DoUnRegister(op.id);
                        break;
                    case OperationType.AddInterceptor:
                        DoAddInterceptor(op.interceptor);
                        break;
                    case OperationType.RemoveInterceptor:
                        DoRemoveInterceptor(op.id);
                        break;
                    case OperationType.Send:
                        DoSend(op.sendPackage);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Exception(e);
            }

            _isOperating = false;

            if (_operations.Count > 0)
                ExecuteOperation(_operations.Dequeue());
        }

        private void DoRegister(EventSource source)
        {
            var eventType = source.eventType;
            if (!_eventRegisterMap.TryGetValue(eventType, out var list))
            {
                list = new List<uint>();
                _eventRegisterMap[eventType] = list;
            }

            list.Add(source.id);
            _eventRegistry[source.id] = source;

            // 按优先级排序（高优先级在前）
            list.Sort((a, b) =>
            {
                var pa = _eventRegistry.TryGetValue(a, out var sa) ? sa.priority : 0;
                var pb = _eventRegistry.TryGetValue(b, out var sb) ? sb.priority : 0;
                return pb.CompareTo(pa);
            });
        }

        private void DoUnRegister(uint id)
        {
            if (_eventRegistry.TryGetValue(id, out var source))
            {
                _eventRegisterMap.GetValueOrDefault(source.eventType)?.Remove(id);
                _eventRegistry.Remove(id);
            }
        }

        private void DoAddInterceptor(EventInterceptor ei)
        {
            if (!_interceptorRegisterMap.TryGetValue(ei.type, out var list))
            {
                list = new List<uint>();
                _interceptorRegisterMap[ei.type] = list;
            }

            list.Add(ei.id);
            _interceptorRegistry[ei.id] = ei;

            list.Sort((a, b) =>
            {
                var pa = _interceptorRegistry.TryGetValue(a, out var ia) ? ia.priority : 0;
                var pb = _interceptorRegistry.TryGetValue(b, out var ib) ? ib.priority : 0;
                return pb.CompareTo(pa);
            });
        }

        private void DoRemoveInterceptor(uint id)
        {
            if (_interceptorRegistry.TryGetValue(id, out var ei))
            {
                _interceptorRegisterMap.GetValueOrDefault(ei.type)?.Remove(id);
                _interceptorRegistry.Remove(id);
            }
        }

        private void DoSend(EventSendPackage package)
        {
            // 拦截器处理
            if (_interceptorRegisterMap.TryGetValue(package.eventType, out var interceptors))
            {
                foreach (var id in interceptors)
                {
                    if (!_interceptorRegistry.TryGetValue(id, out var ei)) continue;
                    try
                    {
                        var state = ei.interceptor(ref package);
                        if (state == InterceptorStateEnum.Break) break;
                        if (state == InterceptorStateEnum.Return) return;
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e);
                    }
                }
            }

            // 事件处理
            if (!_eventRegisterMap.TryGetValue(package.eventType, out var handlers)) return;

            _pendingRemove.Clear();
            foreach (var id in handlers)
            {
                if (!_eventRegistry.TryGetValue(id, out var source)) continue;
                try
                {
                    source.function(package.eventData, package.sender, new EventResult(id, this));
                    if (source.isOnce)
                        _pendingRemove.Add(id);
                }
                catch (Exception e)
                {
                    Log.Exception(e);
                }
            }

            // 移除一次性事件
            foreach (var id in _pendingRemove)
                DoUnRegister(id);
        }

        private object GetOrCreateDefaultEvent(Type eventType)
        {
            if (_defaultEventCache.TryGetValue(eventType, out var evt))
                return evt;
            evt = Activator.CreateInstance(eventType);
            _defaultEventCache[eventType] = evt;
            return evt;
        }

        #endregion
    }
}