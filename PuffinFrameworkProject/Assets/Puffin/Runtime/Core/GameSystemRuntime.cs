using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Interfaces.SystemEvents;
using Puffin.Runtime.Settings;
using Puffin.Runtime.Tools;

namespace Puffin.Runtime.Core
{
    /// <summary>
    /// 系统状态信息
    /// </summary>
    public class SystemStatus
    {
        public string Name { get; set; }
        public string Alias { get; set; }
        public Type Type { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsInitialized { get; set; }
        public int Priority { get; set; }
        public double LastUpdateMs { get; set; }
        public double AverageUpdateMs { get; set; }
        public bool CanToggle { get; set; }
        public string ModuleId { get; set; }
    }

    /// <summary>
    /// 游戏系统运行时 - 支持两种访问模式：
    /// 1. 模块化模式：通过接口访问 GetSystem&lt;IXxxSystem&gt;()
    /// 2. 集成模式：通过类直接访问 GetSystem&lt;XxxManager&gt;()
    /// </summary>
    public class GameSystemRuntime
    {
        private readonly IPuffinLogger _logger;

        // 所有已注册的系统实例
        private readonly List<ISystem> _systems = new();

        // 类型 -> 实例 (支持接口和具体类两种方式访问)
        private readonly Dictionary<Type, ISystem> _typeToInstance = new();

        // 已初始化的系统
        private readonly HashSet<ISystem> _initializedSystems = new();

        // 生命周期事件列表
        private readonly List<ISystemUpdate> _updateList = new();
        private readonly List<ISystemFixedUpdate> _fixedUpdateList = new();
        private readonly List<ISystemLateUpdate> _lateUpdateList = new();
        private readonly List<ISystemApplicationQuit> _quitList = new();
        private readonly List<ISystemApplicationFocusChanged> _focusList = new();
        private readonly List<ISystemApplicationPause> _pauseList = new();
        private readonly List<ISystemRegister> _registerList = new();
        private readonly List<ISystemInitialize> _initAsyncList = new();

        // 性能统计
        private readonly Dictionary<ISystem, PerformanceData> _performanceData = new();
        private readonly Stopwatch _stopwatch = new();

        // 条件符号集合
        private readonly HashSet<string> _definedSymbols = new();

        // 系统别名映射
        private readonly Dictionary<string, ISystem> _aliasToInstance = new();

        // Update 间隔控制
        private readonly Dictionary<ISystem, UpdateIntervalData> _updateIntervals = new();
        private int _frameCount;

        // 系统元数据缓存（避免重复反射）
        private readonly Dictionary<Type, SystemMetadata> _metadataCache = new();

        // 注入信息缓存
        private readonly Dictionary<Type, InjectionInfo> _injectionCache = new();

        /// <summary>
        /// Runtime 是否暂停
        /// </summary>
        public bool IsPaused { get; private set; }

        // 系统事件
        public event Action<ISystem> OnSystemRegistered;
        public event Action<ISystem> OnSystemUnregistered;
        public event Action<ISystem, bool> OnSystemEnabledChanged;

        /// <summary>
        /// 是否启用性能统计
        /// </summary>
        public bool EnableProfiling { get; set; }

        /// <summary>
        /// 是否为编辑器模式（跳过运行时生命周期）
        /// </summary>
        public bool IsEditorMode { get; set; }

        public GameSystemRuntime(IPuffinLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InitializeDefinedSymbols();
        }

        /// <summary>
        /// 初始化预定义的条件符号（UNITY_EDITOR、DEBUG、DEVELOPMENT_BUILD）
        /// </summary>
        private void InitializeDefinedSymbols()
        {
#if UNITY_EDITOR
            _definedSymbols.Add("UNITY_EDITOR");
#endif
#if DEBUG
            _definedSymbols.Add("DEBUG");
#endif
#if DEVELOPMENT_BUILD
            _definedSymbols.Add("DEVELOPMENT_BUILD");
#endif
        }

        /// <summary>
        /// 添加条件符号
        /// </summary>
        public void AddSymbol(string symbol) => _definedSymbols.Add(symbol);

        /// <summary>
        /// 移除条件符号
        /// </summary>
        public void RemoveSymbol(string symbol) => _definedSymbols.Remove(symbol);

        /// <summary>
        /// 获取系统名称（从缓存或 SystemAliasAttribute）
        /// </summary>
        public string GetSystemName(ISystem system) => GetSystemName(system.GetType());

        public string GetSystemName(Type type)
        {
            var meta = GetOrCreateMetadata(type);
            return meta.Alias ?? meta.Name;
        }

        /// <summary>
        /// 获取系统优先级（从缓存或 SystemPriorityAttribute）
        /// </summary>
        public int GetSystemPriority(ISystem system) => GetSystemPriority(system.GetType());

        public int GetSystemPriority(Type type)
        {
            return GetOrCreateMetadata(type).Priority;
        }


        #region 系统访问

        /// <summary>
        /// 获取系统 - 支持接口或具体类
        /// </summary>
        public T GetSystem<T>() where T : class, ISystem
        {
            return _typeToInstance.TryGetValue(typeof(T), out var system) ? system as T : null;
        }

        /// <summary>
        /// 检查系统是否存在
        /// </summary>
        public bool HasSystem<T>() where T : class, ISystem
        {
            return _typeToInstance.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 获取所有已注册的系统（调试用）
        /// </summary>
        public IReadOnlyList<ISystem> GetAllSystems() => _systems;

        /// <summary>
        /// 通过别名获取系统
        /// </summary>
        public ISystem GetSystemByAlias(string alias)
        {
            return _aliasToInstance.TryGetValue(alias, out var system) ? system : null;
        }

        /// <summary>
        /// 通过别名获取系统（泛型版本）
        /// </summary>
        public T GetSystemByAlias<T>(string alias) where T : class, ISystem
        {
            return GetSystemByAlias(alias) as T;
        }

        /// <summary>
        /// 获取系统状态信息
        /// </summary>
        public SystemStatus GetSystemStatus<T>() where T : class, ISystem
        {
            var system = GetSystem<T>();
            if (system == null) return null;

            var perf = _performanceData.GetValueOrDefault(system);
            var type = system.GetType();
            return new SystemStatus
            {
                Name = type.Name,
                Alias = type.GetCustomAttribute<SystemAliasAttribute>()?.Alias,
                Type = type,
                IsEnabled = system is not ISystemEnabled {Enabled: false},
                IsInitialized = _initializedSystems.Contains(system),
                Priority = GetSystemPriority(type),
                LastUpdateMs = perf?.LastMs ?? 0,
                AverageUpdateMs = perf?.AverageMs ?? 0
            };
        }

        /// <summary>
        /// 获取所有系统状态
        /// </summary>
        public List<SystemStatus> GetAllSystemStatus()
        {
            var moduleRegistry = ModuleRegistrySettings.Instance;
            return _systems.Select(sys =>
            {
                var perf = _performanceData.GetValueOrDefault(sys);
                var type = sys.GetType();
                var meta = GetOrCreateMetadata(type);
                return new SystemStatus
                {
                    Name = meta.Name,
                    Alias = meta.Alias,
                    Type = type,
                    IsEnabled = sys is not ISystemEnabled {Enabled: false},
                    IsInitialized = _initializedSystems.Contains(sys),
                    Priority = meta.Priority,
                    LastUpdateMs = perf?.LastMs ?? 0,
                    AverageUpdateMs = perf?.AverageMs ?? 0,
                    CanToggle = sys is ISystemEnabled,
                    ModuleId = ExtractModuleId(type.Assembly.GetName().Name, moduleRegistry)
                };
            }).ToList();
        }

        /// <summary>
        /// 从程序集名称提取模块ID
        /// </summary>
        private static string ExtractModuleId(string assemblyName, ModuleRegistrySettings registry)
        {
            if (registry == null) return null;
            foreach (var module in registry.modules)
            {
                if (assemblyName.Contains(module.moduleId))
                    return module.moduleId;
            }

            return null;
        }

        #endregion

        #region Runtime 控制

        /// <summary>
        /// 暂停 Runtime（停止所有 Update/FixedUpdate/LateUpdate）
        /// </summary>
        public void Pause() => IsPaused = true;

        /// <summary>
        /// 恢复 Runtime
        /// </summary>
        public void Resume() => IsPaused = false;

        #endregion

        #region 系统注册

        /// <summary>
        /// 从类型数组创建并注册系统
        /// </summary>
        public async UniTask CreateSystemFromTypesAsync(Type[] systemTypes)
        {
            // 过滤条件注册
            var filteredTypes = FilterConditionalTypes(systemTypes);

            // 分离默认系统和非默认系统
            var (defaultTypes, nonDefaultTypes) = SeparateDefaultTypes(filteredTypes);

            // 拓扑排序处理依赖（先处理非默认系统）
            var sortedNonDefaultTypes = TopologicalSort(nonDefaultTypes);
            var sortedDefaultTypes = TopologicalSort(defaultTypes);

            // 先注册非默认系统
            foreach (var type in sortedNonDefaultTypes)
            {
                try
                {
                    Register(type);
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }
            }

            // 再注册默认系统（只注册没有其他实现的接口）
            RegisterDefaultSystems(sortedDefaultTypes);

            // 依赖注入
            InjectDependencies();

            SortByPriority();
            InvokeRegister();
            await InvokeInitializeAsync();
        }

        /// <summary>
        /// 同步版本（不等待异步初始化）
        /// </summary>
        public void CreateSystemFromTypes(Type[] systemTypes)
        {
            CreateSystemFromTypesAsync(systemTypes).Forget();
        }

        /// <summary>
        /// 动态注册单个系统（运行时）
        /// </summary>
        public async UniTask RegisterSystemAsync<T>() where T : class, ISystem, new()
        {
            var type = typeof(T);

            // 已注册则跳过
            if (_typeToInstance.ContainsKey(type))
            {
                _logger.Info($"系统 {type.Name} 已注册，跳过");
                return;
            }

            Register(type);
            InjectDependencies(_typeToInstance[type]);
            SortByPriority();

            if (_typeToInstance[type] is ISystemRegister reg)
            {
                try
                {
                    reg.OnRegister();
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }
            }

            if (_typeToInstance[type] is ISystemInitialize init)
            {
                try
                {
                    await init.OnInitializeAsync();
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }
            }

            _initializedSystems.Add(_typeToInstance[type]);
        }

        /// <summary>
        /// 动态注册单个系统（同步版本）
        /// </summary>
        public void RegisterSystem<T>() where T : class, ISystem, new()
        {
            RegisterSystemAsync<T>().Forget();
        }

        /// <summary>
        /// 动态注册单个系统（通过类型）
        /// </summary>
        public async UniTask RegisterSystemAsync(Type type)
        {
            if (_typeToInstance.ContainsKey(type))
            {
                _logger.Info($"系统 {type.Name} 已注册，跳过");
                return;
            }

            Register(type);
            InjectDependencies(_typeToInstance[type]);
            SortByPriority();

            if (_typeToInstance[type] is ISystemRegister reg)
            {
                try
                {
                    reg.OnRegister();
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }
            }

            if (_typeToInstance[type] is ISystemInitialize init)
            {
                try
                {
                    await init.OnInitializeAsync();
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }
            }

            _initializedSystems.Add(_typeToInstance[type]);
        }

        /// <summary>
        /// 注册系统
        /// </summary>
        private void Register(Type type)
        {
            if (_typeToInstance.ContainsKey(type))
                return;

            var system = Activator.CreateInstance(type) as ISystem
                         ?? throw new Exception($"创建系统实例失败: {type}");

            _systems.Add(system);
            _performanceData[system] = new PerformanceData();

            // 注册具体类类型
            _typeToInstance[type] = system;

            // 注册所有实现的 IGameSystem 派生接口
            var settings = SystemRegistrySettings.Instance;
            foreach (var iface in type.GetInterfaces())
            {
                if (iface != typeof(ISystem) && typeof(ISystem).IsAssignableFrom(iface))
                {
                    if (_typeToInstance.ContainsKey(iface))
                    {
                        _logger.Info($"接口 {iface.Name} 已被注册，跳过: {type.Name}");
                        continue;
                    }

                    // 检查是否有指定的实现
                    var selectedImpl = settings?.GetSelectedImplementation(iface.FullName);
                    if (selectedImpl != null && selectedImpl != type.FullName)
                    {
                        _logger.Info($"接口 {iface.Name} 指定使用 {selectedImpl}，跳过: {type.Name}");
                        continue;
                    }

                    _typeToInstance[iface] = system;
                }
            }

            // 注册生命周期事件
            TryAddEvent(system, _updateList);
            TryAddEvent(system, _fixedUpdateList);
            TryAddEvent(system, _lateUpdateList);
            TryAddEvent(system, _quitList);
            TryAddEvent(system, _focusList);
            TryAddEvent(system, _pauseList);
            TryAddEvent(system, _registerList);
            TryAddEvent(system, _initAsyncList);

            // 使用缓存的元数据注册别名和 Update 间隔
            var meta = GetOrCreateMetadata(type);
            if (!string.IsNullOrEmpty(meta.Alias))
                _aliasToInstance[meta.Alias] = system;

            if (meta.UpdateInterval > 1)
                _updateIntervals[system] = new UpdateIntervalData(meta.UpdateInterval);

            if (!IsEditorMode)
                _logger.Info($"注册系统: {GetSystemName(system)}");
            OnSystemRegistered?.Invoke(system);
        }

        /// <summary>
        /// 注销系统
        /// </summary>
        public void UnRegister<T>() where T : class, ISystem
        {
            UnRegister(typeof(T));
        }

        /// <summary>
        /// 注销系统（通过类型）
        /// </summary>
        public void UnRegister(Type type)
        {
            if (!_typeToInstance.TryGetValue(type, out var system))
                return;

            // 触发注销事件
            if (system is ISystemRegister reg)
            {
                try
                {
                    reg.OnUnRegister();
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }
            }

            _systems.Remove(system);
            _initializedSystems.Remove(system);
            _performanceData.Remove(system);

            // 移除所有指向该实例的类型映射
            var keysToRemove = _typeToInstance.Where(kv => kv.Value == system).Select(kv => kv.Key).ToList();
            foreach (var key in keysToRemove)
                _typeToInstance.Remove(key);

            // 移除生命周期事件
            TryRemoveEvent(system, _updateList);
            TryRemoveEvent(system, _fixedUpdateList);
            TryRemoveEvent(system, _lateUpdateList);
            TryRemoveEvent(system, _quitList);
            TryRemoveEvent(system, _focusList);
            TryRemoveEvent(system, _pauseList);
            TryRemoveEvent(system, _registerList);
            TryRemoveEvent(system, _initAsyncList);

            // 移除别名
            var aliasToRemove = _aliasToInstance.Where(kv => kv.Value == system).Select(kv => kv.Key).ToList();
            foreach (var alias in aliasToRemove)
                _aliasToInstance.Remove(alias);

            // 移除 Update 间隔
            _updateIntervals.Remove(system);

            if (!IsEditorMode)
                _logger.Info($"注销系统: {GetSystemName(system)}");
            OnSystemUnregistered?.Invoke(system);
        }

        /// <summary>
        /// 启用/禁用指定系统
        /// </summary>
        public void SetSystemEnabled<T>(bool enabled) where T : class, ISystem
        {
            var system = GetSystem<T>();
            SetSystemEnabledInternal(system, enabled);
        }

        /// <summary>
        /// 启用/禁用指定系统（通过类型）
        /// </summary>
        public void SetSystemEnabled(Type type, bool enabled)
        {
            if (_typeToInstance.TryGetValue(type, out var system))
                SetSystemEnabledInternal(system, enabled);
        }

        private void SetSystemEnabledInternal(ISystem system, bool enabled)
        {
            if (system is ISystemEnabled sys)
            {
                var oldEnabled = sys.Enabled;
                sys.Enabled = enabled;
                if (oldEnabled != enabled)
                    OnSystemEnabledChanged?.Invoke(system, enabled);
            }
        }

        #endregion

        #region 生命周期调用

        /// <summary>
        /// 每帧更新所有实现 IUpdate 的系统
        /// </summary>
        public void Update(float deltaTime)
        {
            if (IsPaused) return;

            _frameCount++;

            foreach (var sys in _updateList)
            {
                if (sys is ISystemEnabled {Enabled: false}) continue;

                // 检查 Update 间隔
                var gameSystem = (ISystem) sys;
                if (_updateIntervals.TryGetValue(gameSystem, out var intervalData))
                {
                    if (!intervalData.ShouldUpdate(_frameCount))
                        continue;
                }

                try
                {
                    if (EnableProfiling)
                    {
                        _stopwatch.Restart();
                        sys.OnUpdate(deltaTime);
                        _stopwatch.Stop();
                        RecordPerformance(gameSystem, _stopwatch.Elapsed.TotalMilliseconds);
                    }
                    else
                    {
                        sys.OnUpdate(deltaTime);
                    }
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }
            }
        }

        /// <summary>
        /// 固定时间步更新所有实现 IFixedUpdate 的系统
        /// </summary>
        public void FixedUpdate(float deltaTime)
        {
            if (IsPaused) return;
            InvokeLifecycle(_fixedUpdateList, sys => sys.OnFixedUpdate(deltaTime));
        }

        /// <summary>
        /// 延迟更新所有实现 ILateUpdate 的系统
        /// </summary>
        public void LateUpdate(float deltaTime)
        {
            if (IsPaused) return;
            InvokeLifecycle(_lateUpdateList, sys => sys.OnLateUpdate(deltaTime));
        }

        /// <summary>
        /// 应用退出时调用
        /// </summary>
        public void OnApplicationQuit() =>
            InvokeLifecycle(_quitList, sys => sys.OnApplicationQuit(), checkEnabled: false);

        /// <summary>
        /// 应用焦点变化时调用
        /// </summary>
        public void OnApplicationFocus(bool focus) =>
            InvokeLifecycle(_focusList, sys => sys.OnApplicationFocus(focus), checkEnabled: false);

        /// <summary>
        /// 应用暂停时调用
        /// </summary>
        public void OnApplicationPause(bool pause) =>
            InvokeLifecycle(_pauseList, sys => sys.OnApplicationPause(pause), checkEnabled: false);

        /// <summary>
        /// 调用生命周期事件
        /// </summary>
        private void InvokeLifecycle<T>(List<T> list, Action<T> action, bool checkEnabled = true)
        {
            foreach (var sys in list)
            {
                if (checkEnabled && sys is ISystemEnabled {Enabled: false}) continue;
                try { action(sys); }
                catch (Exception e) { _logger.Exception(e); }
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 调用所有系统的注册回调
        /// </summary>
        private void InvokeRegister()
        {
            if (IsEditorMode) return;
            foreach (var sys in _registerList)
            {
                try
                {
                    sys.OnRegister();
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }
            }
        }

        /// <summary>
        /// 异步初始化所有实现 IInitializeAsync 的系统
        /// </summary>
        private async UniTask InvokeInitializeAsync()
        {
            if (IsEditorMode)
            {
                foreach (var sys in _systems)
                    _initializedSystems.Add(sys);
                return;
            }

            foreach (var sys in _initAsyncList)
            {
                try
                {
                    await sys.OnInitializeAsync();
                    _initializedSystems.Add((ISystem) sys);
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }
            }

            // 标记所有系统为已初始化
            foreach (var sys in _systems)
                _initializedSystems.Add(sys);
        }

        /// <summary>
        /// 尝试将系统添加到生命周期事件列表
        /// </summary>
        private void TryAddEvent<T>(ISystem system, List<T> list) where T : class
        {
            if (system is T evt)
                list.Add(evt);
        }

        /// <summary>
        /// 尝试从生命周期事件列表移除系统
        /// </summary>
        private void TryRemoveEvent<T>(ISystem system, List<T> list) where T : class
        {
            if (system is T evt)
                list.Remove(evt);
        }

        /// <summary>
        /// 按优先级排序所有系统列表
        /// </summary>
        private void SortByPriority()
        {
            int Compare(ISystem a, ISystem b) => GetSystemPriority(a).CompareTo(GetSystemPriority(b));

            _systems.Sort(Compare);
            _updateList.Sort((a, b) => Compare((ISystem) a, (ISystem) b));
            _fixedUpdateList.Sort((a, b) => Compare((ISystem) a, (ISystem) b));
            _lateUpdateList.Sort((a, b) => Compare((ISystem) a, (ISystem) b));
            _quitList.Sort((a, b) => Compare((ISystem) a, (ISystem) b));
            _focusList.Sort((a, b) => Compare((ISystem) a, (ISystem) b));
            _pauseList.Sort((a, b) => Compare((ISystem) a, (ISystem) b));
            _registerList.Sort((a, b) => Compare((ISystem) a, (ISystem) b));
            _initAsyncList.Sort((a, b) => Compare((ISystem) a, (ISystem) b));
        }

        #endregion


        #region 条件注册

        /// <summary>
        /// 过滤条件注册的类型，只保留满足条件的系统
        /// </summary>
        private Type[] FilterConditionalTypes(Type[] types)
        {
            return types.Where(CheckConditional).ToArray();
        }

        /// <summary>
        /// 检查类型是否满足条件注册（ConditionalSystemAttribute）
        /// </summary>
        private bool CheckConditional(Type type)
        {
            var attr = type.GetCustomAttribute<ConditionalSystemAttribute>();
            if (attr == null) return true;
            return _definedSymbols.Contains(attr.Symbol);
        }

        #endregion

        #region 默认系统处理

        /// <summary>
        /// 分离默认系统和非默认系统
        /// </summary>
        private (Type[] defaultTypes, Type[] nonDefaultTypes) SeparateDefaultTypes(Type[] types)
        {
            var defaultList = new List<Type>();
            var nonDefaultList = new List<Type>();

            foreach (var type in types)
            {
                if (type.GetCustomAttribute<DefaultAttribute>() != null)
                    defaultList.Add(type);
                else
                    nonDefaultList.Add(type);
            }

            return (defaultList.ToArray(), nonDefaultList.ToArray());
        }

        /// <summary>
        /// 注册默认系统（只注册没有其他实现的接口）
        /// </summary>
        private void RegisterDefaultSystems(Type[] defaultTypes)
        {
            var settings = SystemRegistrySettings.Instance;

            // 按接口分组默认实现
            var interfaceToDefaults = new Dictionary<Type, List<Type>>();
            foreach (var type in defaultTypes)
            {
                foreach (var iface in type.GetInterfaces())
                {
                    if (iface != typeof(ISystem) && typeof(ISystem).IsAssignableFrom(iface))
                    {
                        if (!interfaceToDefaults.ContainsKey(iface))
                            interfaceToDefaults[iface] = new List<Type>();
                        interfaceToDefaults[iface].Add(type);
                    }
                }
            }

            // 注册默认系统
            foreach (var type in defaultTypes)
            {
                bool shouldRegister = false;

                // 检查该类型实现的所有接口
                foreach (var iface in type.GetInterfaces())
                {
                    if (iface != typeof(ISystem) && typeof(ISystem).IsAssignableFrom(iface))
                    {
                        // 如果接口还没有被注册（没有非默认实现）
                        if (!_typeToInstance.ContainsKey(iface))
                        {
                            // 检查是否有多个默认实现
                            var defaults = interfaceToDefaults[iface];
                            if (defaults.Count > 1)
                            {
                                // 检查系统注册表中是否有选择
                                var selectedImpl = settings?.GetSelectedImplementation(iface.FullName);
                                if (selectedImpl != null && selectedImpl == type.FullName)
                                {
                                    shouldRegister = true;
                                    break;
                                }
                                else if (selectedImpl == null)
                                {
                                    // 没有选择，使用第一个
                                    if (defaults[0] == type)
                                    {
                                        _logger.Warning($"接口 {iface.Name} 有多个默认实现，未指定选择，使用第一个: {type.Name}");
                                        shouldRegister = true;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // 只有一个默认实现，直接注册
                                shouldRegister = true;
                                break;
                            }
                        }
                    }
                }

                if (shouldRegister)
                {
                    try
                    {
                        Register(type);
                    }
                    catch (Exception e)
                    {
                        _logger.Exception(e);
                    }
                }
            }
        }

        #endregion

        #region 依赖排序

        /// <summary>
        /// 拓扑排序处理系统依赖关系
        /// </summary>
        private Type[] TopologicalSort(Type[] types)
        {
            var typeSet = new HashSet<Type>(types);
            var result = new List<Type>();
            var visited = new HashSet<Type>();
            var visiting = new HashSet<Type>();
            var disabled = new HashSet<Type>();

            foreach (var type in types)
            {
                if (!visited.Contains(type) && !disabled.Contains(type))
                    Visit(type, typeSet, visited, visiting, result, disabled);
            }

            return result.ToArray();
        }

        private void Visit(Type type, HashSet<Type> typeSet, HashSet<Type> visited, HashSet<Type> visiting,
            List<Type> result, HashSet<Type> disabled)
        {
            if (visited.Contains(type) || disabled.Contains(type)) return;
            if (visiting.Contains(type))
                throw new Exception($"循环依赖检测: {type.Name}");

            visiting.Add(type);

            var deps = type.GetCustomAttributes<DependsOnAttribute>();
            var settings = SystemRegistrySettings.Instance;
            foreach (var dep in deps)
            {
                // 找到实现该接口的类型，优先使用 SystemRegistrySettings 中指定的实现
                Type depType = null;
                if (dep.DependencyType.IsInterface)
                {
                    var selectedImpl = settings?.GetSelectedImplementation(dep.DependencyType.FullName);
                    if (!string.IsNullOrEmpty(selectedImpl))
                        depType = typeSet.FirstOrDefault(t => t.FullName == selectedImpl);
                }
                depType ??= typeSet.FirstOrDefault(t =>
                    dep.DependencyType.IsAssignableFrom(t) || t == dep.DependencyType);

                if (depType != null)
                {
                    Visit(depType, typeSet, visited, visiting, result, disabled);
                    // 依赖被禁用，当前系统也禁用
                    if (disabled.Contains(depType))
                    {
                        Log.Warning($"系统 {type.Name} 依赖 {depType.Name} 已被禁用，{type.Name} 也已禁用");
                        visiting.Remove(type);
                        disabled.Add(type);
                        return;
                    }
                }
                else
                {
                    // 依赖不存在，禁用当前系统
                    Log.Warning($"系统 {type.Name} 依赖 {dep.DependencyType.Name} 不存在，已禁用");
                    visiting.Remove(type);
                    disabled.Add(type);
                    return;
                }
            }

            visiting.Remove(type);
            visited.Add(type);
            result.Add(type);
        }

        #endregion

        #region 依赖注入

        /// <summary>
        /// 为所有已注册系统执行依赖注入
        /// </summary>
        private void InjectDependencies()
        {
            foreach (var system in _systems)
                InjectDependencies(system);
        }

        /// <summary>
        /// 为单个系统执行依赖注入
        /// </summary>
        private void InjectDependencies(ISystem system)
        {
            InjectTo(system);
        }

        /// <summary>
        /// 向任意对象注入依赖（使用缓存的注入信息）
        /// </summary>
        /// <param name="target">目标对象</param>
        public void InjectTo(object target)
        {
            if (target == null) return;

            var type = target.GetType();
            var info = GetOrCreateInjectionInfo(type);

            // 注入字段
            foreach (var (field, isWeak) in info.Fields)
            {
                if (_typeToInstance.TryGetValue(field.FieldType, out var dep))
                {
                    field.SetValue(target, dep);
                    _logger.Info($"注入 {type.Name}.{field.Name} = {GetSystemName(dep)}");
                }
                else if (!isWeak)
                {
                    _logger.Warning($"注入失败 {type.Name}.{field.Name}: 未找到 {field.FieldType.Name}");
                }
            }

            // 注入属性
            foreach (var (prop, isWeak) in info.Properties)
            {
                if (_typeToInstance.TryGetValue(prop.PropertyType, out var dep))
                {
                    prop.SetValue(target, dep);
                    _logger.Info($"注入 {type.Name}.{prop.Name} = {GetSystemName(dep)}");
                }
                else if (!isWeak)
                {
                    _logger.Warning($"注入失败 {type.Name}.{prop.Name}: 未找到 {prop.PropertyType.Name}");
                }
            }
        }

        #endregion

        #region 性能统计

        private class PerformanceData
        {
            private const int SampleCount = 60;
            private readonly Queue<double> _samples = new();
            public double LastMs { get; private set; }
            public double AverageMs => _samples.Count > 0 ? _samples.Average() : 0;

            public void Record(double ms)
            {
                LastMs = ms;
                _samples.Enqueue(ms);
                if (_samples.Count > SampleCount)
                    _samples.Dequeue();
            }
        }

        private void RecordPerformance(ISystem system, double ms)
        {
            if (_performanceData.TryGetValue(system, out var data))
                data.Record(ms);
        }

        #endregion

        #region Update 间隔控制

        private class UpdateIntervalData
        {
            public int Interval { get; }
            private int _lastUpdateFrame;

            public UpdateIntervalData(int interval)
            {
                Interval = interval;
            }

            public bool ShouldUpdate(int currentFrame)
            {
                if (currentFrame - _lastUpdateFrame >= Interval)
                {
                    _lastUpdateFrame = currentFrame;
                    return true;
                }

                return false;
            }
        }

        #endregion

        #region 元数据缓存

        private class SystemMetadata
        {
            public string Name;
            public string Alias;
            public int Priority;
            public int UpdateInterval;
        }

        private SystemMetadata GetOrCreateMetadata(Type type)
        {
            if (_metadataCache.TryGetValue(type, out var meta))
                return meta;

            var aliasAttr = type.GetCustomAttribute<SystemAliasAttribute>();
            var priorityAttr = type.GetCustomAttribute<SystemPriorityAttribute>();
            var intervalAttr = type.GetCustomAttribute<UpdateIntervalAttribute>();

            meta = new SystemMetadata
            {
                Name = type.Name,
                Alias = aliasAttr?.Alias,
                Priority = priorityAttr?.Priority ?? 0,
                UpdateInterval = intervalAttr?.FrameInterval ?? 0
            };
            _metadataCache[type] = meta;
            return meta;
        }

        private class InjectionInfo
        {
            public List<(FieldInfo field, bool isWeak)> Fields = new();
            public List<(PropertyInfo prop, bool isWeak)> Properties = new();
        }

        private InjectionInfo GetOrCreateInjectionInfo(Type type)
        {
            if (_injectionCache.TryGetValue(type, out var info))
                return info;

            info = new InjectionInfo();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var field in type.GetFields(flags))
            {
                var isInject = field.GetCustomAttribute<InjectAttribute>() != null;
                var isWeakInject = field.GetCustomAttribute<WeakInjectAttribute>() != null;
                if (isInject || isWeakInject)
                    info.Fields.Add((field, isWeakInject));
            }

            foreach (var prop in type.GetProperties(flags))
            {
                if (!prop.CanWrite) continue;
                var isInject = prop.GetCustomAttribute<InjectAttribute>() != null;
                var isWeakInject = prop.GetCustomAttribute<WeakInjectAttribute>() != null;
                if (isInject || isWeakInject)
                    info.Properties.Add((prop, isWeakInject));
            }

            _injectionCache[type] = info;
            return info;
        }

        #endregion

        #region 依赖图导出

        /// <summary>
        /// 依赖关系信息
        /// </summary>
        public class DependencyInfo
        {
            public string SystemName { get; set; }
            public Type SystemType { get; set; }
            public List<Type> Dependencies { get; set; } = new();
            public List<Type> Injections { get; set; } = new();
        }

        /// <summary>
        /// 获取所有系统的依赖关系
        /// </summary>
        public List<DependencyInfo> GetDependencyGraph()
        {
            var result = new List<DependencyInfo>();

            foreach (var system in _systems)
            {
                var type = system.GetType();
                var info = new DependencyInfo
                {
                    SystemName = GetSystemName(system),
                    SystemType = type
                };

                // 获取 DependsOn 依赖
                var deps = type.GetCustomAttributes<DependsOnAttribute>();
                foreach (var dep in deps)
                {
                    info.Dependencies.Add(dep.DependencyType);
                }

                // 获取注入依赖
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var field in type.GetFields(flags))
                {
                    if (field.GetCustomAttribute<InjectAttribute>() != null ||
                        field.GetCustomAttribute<WeakInjectAttribute>() != null)
                    {
                        info.Injections.Add(field.FieldType);
                    }
                }

                foreach (var prop in type.GetProperties(flags))
                {
                    if (prop.GetCustomAttribute<InjectAttribute>() != null ||
                        prop.GetCustomAttribute<WeakInjectAttribute>() != null)
                    {
                        info.Injections.Add(prop.PropertyType);
                    }
                }

                result.Add(info);
            }

            return result;
        }

        /// <summary>
        /// 导出依赖图为字符串（可用于日志或调试）
        /// </summary>
        public string ExportDependencyGraphAsText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 系统依赖图 ===");

            var graph = GetDependencyGraph();
            foreach (var info in graph)
            {
                sb.AppendLine($"\n[{info.SystemName}] ({info.SystemType.Name})");

                if (info.Dependencies.Count > 0)
                {
                    sb.AppendLine("  依赖:");
                    foreach (var dep in info.Dependencies)
                        sb.AppendLine($"    - {dep.Name}");
                }

                if (info.Injections.Count > 0)
                {
                    sb.AppendLine("  注入:");
                    foreach (var inj in info.Injections)
                        sb.AppendLine($"    - {inj.Name}");
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}