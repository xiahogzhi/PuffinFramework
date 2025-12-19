# PuffinFramework
# 这个仓库为开发版，自用，请勿使用这个仓库
一个轻量级、模块化的 Unity 游戏框架，提供系统管理、依赖注入、事件分发等核心功能。

## 目录

- [特性](#特性)
- [快速开始](#快速开始)
- [核心概念](#核心概念)
- [系统开发](#系统开发)
- [事件系统](#事件系统)
- [依赖注入](#依赖注入)
- [生命周期](#生命周期)
- [工具类](#工具类)
- [模块管理](#模块管理)
- [配置表集成](#配置表集成)
- [编辑器工具](#编辑器工具)

## 特性

- **自动扫描注册** - 通过特性标记自动发现和注册系统
- **依赖注入** - 支持字段和属性的自动注入
- **事件系统** - 强大的事件分发机制，支持优先级、拦截器、一次性事件
- **生命周期管理** - 完整的 Unity 生命周期事件支持
- **模块化架构** - 系统热插拔，支持模块安装/卸载
- **环境依赖管理** - 自动处理 NuGet、GitHub 等外部依赖
- **性能监控** - 内置系统性能统计
- **编辑器支持** - 丰富的编辑器工具和本地化（中/英文）

## 快速开始

### 1. 场景设置

在场景中创建一个 GameObject，添加 `Launcher` 组件：

```
Hierarchy
└── [Launcher]
    └── Launcher (Component)
```

框架会在场景加载后自动初始化（如果 `PuffinSettings` 中启用了 `autoInitialize`）。

### 2. 创建第一个系统

```csharp
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Interfaces.SystemEvents;
using Puffin.Runtime.Tools;

[AutoRegister]
public class HelloSystem : IGameSystem, IRegisterEvent
{
    public void OnRegister()
    {
        Log.Info("Hello, PuffinFramework!");
    }

    public void OnUnRegister()
    {
        Log.Info("Goodbye!");
    }
}
```

### 3. 访问系统

```csharp
// 获取系统实例
var helloSystem = PuffinFramework.GetSystem<HelloSystem>();

// 检查系统是否存在
if (PuffinFramework.HasSystem<HelloSystem>())
{
    // ...
}
```

## 核心概念

### 系统 (System)

系统是框架的核心单元，实现 `IGameSystem` 接口的类都可以作为系统注册到框架中。

```csharp
public interface IGameSystem { }
```

### 特性 (Attributes)

| 特性 | 说明 |
|------|------|
| `[AutoRegister]` | 标记系统为自动注册 |
| `[Inject]` | 标记字段/属性需要依赖注入 |
| `[WeakInject]` | 弱依赖注入（系统不存在时不报错） |
| `[DependsOn(typeof(...))]` | 声明系统依赖 |
| `[SystemPriority(100)]` | 设置系统优先级（数值越大越先初始化） |
| `[SystemAlias("别名")]` | 为系统设置别名 |
| `[ConditionalSystem("SYMBOL")]` | 条件注册（需要符号存在） |
| `[UpdateInterval(0.1f)]` | 设置 Update 调用间隔 |

## 系统开发

### 基础系统

```csharp
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Interfaces.SystemEvents;

[AutoRegister]
public class PlayerSystem : IGameSystem, IRegisterEvent, IInitializeAsync
{
    public void OnRegister()
    {
        // 系统注册时调用
    }

    public async UniTask OnInitializeAsync()
    {
        // 异步初始化
        await LoadPlayerDataAsync();
    }

    public void OnUnRegister()
    {
        // 系统注销时调用
    }
}
```

### 带 Update 的系统

```csharp
[AutoRegister]
public class GameLoopSystem : IGameSystem, IUpdate, IFixedUpdate, ILateUpdate
{
    public void OnUpdate(float deltaTime)
    {
        // 每帧调用
    }

    public void OnFixedUpdate(float fixedDeltaTime)
    {
        // 固定时间步调用
    }

    public void OnLateUpdate(float deltaTime)
    {
        // LateUpdate 调用
    }
}
```

### 控制 Update 频率

```csharp
[AutoRegister]
[UpdateInterval(0.5f)] // 每 0.5 秒调用一次
public class SlowUpdateSystem : IGameSystem, IUpdate
{
    public void OnUpdate(float deltaTime)
    {
        // 每 0.5 秒调用一次
    }
}
```

### 系统依赖

```csharp
[AutoRegister]
[DependsOn(typeof(ConfigSystem))]
[DependsOn(typeof(NetworkSystem))]
public class GameSystem : IGameSystem, IRegisterEvent
{
    [Inject] private ConfigSystem _config;
    [Inject] private NetworkSystem _network;

    public void OnRegister()
    {
        // ConfigSystem 和 NetworkSystem 已经初始化完成
        // _config 和 _network 已经被注入
    }

    public void OnUnRegister() { }
}
```

### 条件注册

```csharp
[AutoRegister]
[ConditionalSystem("ENABLE_DEBUG")]
public class DebugSystem : IGameSystem
{
    // 只有当 ENABLE_DEBUG 符号存在时才会注册
}
```

### 系统优先级

```csharp
[AutoRegister]
[SystemPriority(1000)] // 高优先级，先初始化
public class CoreSystem : IGameSystem { }

[AutoRegister]
[SystemPriority(100)] // 低优先级，后初始化
public class GameplaySystem : IGameSystem { }
```

### 编辑器支持

```csharp
[AutoRegister]
public class EditorToolSystem : IGameSystem, IEditorSupport
{
    public void OnEditorInitialize()
    {
        // 编辑器模式下初始化（非 Play 模式）
    }

    public void OnEditorUpdate()
    {
        // 编辑器模式下的 Update
    }
}
```

## 事件系统

### 定义事件

```csharp
using Puffin.Runtime.Events.Interfaces;

// 简单事件
public struct PlayerDiedEvent : IEventDefine { }

// 带数据的事件
public struct DamageEvent : IEventDefine
{
    public int damage;
    public string source;
}
```

### 发送事件

```csharp
// 发送无参数事件
PuffinFramework.Dispatcher.SendDefault<PlayerDiedEvent>();

// 发送带数据的事件
PuffinFramework.Dispatcher.Send(new DamageEvent
{
    damage = 100,
    source = "Enemy"
});

// 使用初始化器发送（推荐用于 struct）
PuffinFramework.Dispatcher.Send<DamageEvent>(ref evt =>
{
    evt.damage = 100;
    evt.source = "Enemy";
});
```

### 监听事件

```csharp
[AutoRegister]
public class UISystem : IGameSystem, IRegisterEvent
{
    public void OnRegister()
    {
        // 注册事件监听
        PuffinFramework.Dispatcher.Register<DamageEvent>(OnDamage)
            .Priority(100)  // 设置优先级
            .AddTo(this);   // 绑定生命周期
    }

    private void OnDamage(DamageEvent evt)
    {
        Log.Info($"受到 {evt.damage} 点伤害，来源: {evt.source}");
    }

    public void OnUnRegister() { }
}
```

### 事件选项

```csharp
// 一次性事件（触发后自动注销）
PuffinFramework.Dispatcher.Register<GameStartEvent>(OnGameStart)
    .Once();

// 高优先级（数值越大越先执行）
PuffinFramework.Dispatcher.Register<GameStartEvent>(OnGameStart)
    .Priority(1000);

// 注册后立即调用一次
PuffinFramework.Dispatcher.Register<ConfigLoadedEvent>(OnConfigLoaded)
    .InvokeNow();

// 绑定到 GameObject 生命周期
PuffinFramework.Dispatcher.Register<UpdateEvent>(OnUpdate)
    .AddTo(gameObject);
```

### 事件收集器

```csharp
public class MyComponent : MonoBehaviour
{
    private EventCollector _collector = new();

    void Start()
    {
        PuffinFramework.Dispatcher.Register<EventA>(OnEventA).AddTo(_collector);
        PuffinFramework.Dispatcher.Register<EventB>(OnEventB).AddTo(_collector);
    }

    void OnDestroy()
    {
        _collector.Dispose(); // 自动注销所有事件
    }
}
```

### 事件拦截器

```csharp
// 添加拦截器
var interceptorId = PuffinFramework.Dispatcher.AddInterceptor<DamageEvent>(
    (ref EventSendPackage package) =>
    {
        var evt = (DamageEvent)package.eventData;
        if (evt.damage > 1000)
        {
            Log.Warning("伤害过高，已拦截");
            return InterceptorStateEnum.Return; // 阻止事件继续传播
        }
        return InterceptorStateEnum.Next; // 继续传播
    },
    "伤害检查拦截器",
    priority: 100
);

// 移除拦截器
PuffinFramework.Dispatcher.RemoveInterceptor(interceptorId);
```

## 依赖注入

### 强依赖注入

```csharp
[AutoRegister]
public class CombatSystem : IGameSystem
{
    [Inject] private PlayerSystem _player;      // 字段注入
    [Inject] public ConfigSystem Config { get; set; }  // 属性注入

    // 如果 PlayerSystem 或 ConfigSystem 不存在，框架会报错
}
```

### 弱依赖注入

```csharp
[AutoRegister]
public class AnalyticsSystem : IGameSystem
{
    [WeakInject] private DebugSystem _debug;  // 可选依赖

    public void TrackEvent(string name)
    {
        _debug?.Log(name);  // 需要判空
    }
}
```

## 生命周期

### 系统生命周期接口

| 接口 | 方法 | 调用时机 |
|------|------|----------|
| `IRegisterEvent` | `OnRegister()` | 系统注册时 |
| `IRegisterEvent` | `OnUnRegister()` | 系统注销时 |
| `IInitializeAsync` | `OnInitializeAsync()` | 注册后异步初始化 |
| `IUpdate` | `OnUpdate(float)` | 每帧 Update |
| `IFixedUpdate` | `OnFixedUpdate(float)` | FixedUpdate |
| `ILateUpdate` | `OnLateUpdate(float)` | LateUpdate |
| `IApplicationQuit` | `OnApplicationQuit()` | 应用退出 |
| `IApplicationPause` | `OnApplicationPause(bool)` | 应用暂停/恢复 |
| `IApplicationFocusChanged` | `OnApplicationFocusChanged(bool)` | 焦点变化 |
| `ISystemEnabled` | `OnSystemEnabled()` / `OnSystemDisabled()` | 系统启用/禁用 |
| `IEditorSupport` | `OnEditorInitialize()` | 编辑器模式初始化 |

### 调用顺序

```
1. OnRegister()
2. 依赖注入
3. OnInitializeAsync()
4. OnSystemEnabled()
5. Update/FixedUpdate/LateUpdate (循环)
6. OnSystemDisabled()
7. OnUnRegister()
```

## 工具类

### 日志系统

```csharp
using Puffin.Runtime.Tools;

Log.Info("普通信息");
Log.Warning("警告信息");
Log.Error("错误信息");
Log.Exception(exception);
Log.Separator("分隔线标题");
```

### 对象池

```csharp
using Puffin.Runtime.Tools.Pool;

// 通用对象池
var pool = new ObjectPool<MyClass>(
    createFunc: () => new MyClass(),
    onGet: obj => obj.Reset(),
    onRelease: obj => obj.Clear(),
    maxSize: 100
);

var obj = pool.Get();
pool.Release(obj);

// GameObject 对象池
var goPool = new GameObjectPool(prefab, parent, initialSize: 10);
var go = goPool.Get();
goPool.Release(go);
```

### 状态机

```csharp
using Puffin.Runtime.Tools.FSM;

// 定义状态
public class IdleState : IState<PlayerController>
{
    public void OnEnter(PlayerController owner) { }
    public void OnUpdate(PlayerController owner, float deltaTime) { }
    public void OnExit(PlayerController owner) { }
}

// 使用状态机
var fsm = new StateMachine<PlayerController, IState<PlayerController>>(player);
fsm.AddState(new IdleState());
fsm.AddState(new RunState());
fsm.AddState(new JumpState());

fsm.ChangeState<IdleState>();
fsm.Update(Time.deltaTime);
```

### 单例基类

```csharp
using Puffin.Runtime.Tools;

public class GameManager : Singleton<GameManager>
{
    public int Score { get; set; }
}

// 使用
GameManager.Instance.Score = 100;
```

## GameScript 基类

继承 `GameScript` 替代 `MonoBehaviour`，获得更好的生命周期管理：

```csharp
using Puffin.Runtime.Behaviours;

public class PlayerController : GameScript
{
    protected override void OnScriptInitialize()
    {
        // 替代 Awake
    }

    protected override void OnScriptStart()
    {
        // 替代 Start
    }

    protected override void OnEventRegister()
    {
        // 注册事件
        PuffinFramework.Dispatcher.Register<DamageEvent>(OnDamage)
            .AddTo(gameObject);
    }

    protected override void OnScriptActivate()
    {
        // 替代 OnEnable
    }

    protected override void OnScriptDeactivate()
    {
        // 替代 OnDisable
    }

    protected override void OnScriptEnd()
    {
        // 替代 OnDestroy
    }
}
```

## 模块管理

### 通过编辑器安装模块

1. 打开 `Window > Puffin > Module Hub`
2. 浏览可用模块
3. 点击安装

### 创建自定义模块

1. 打开 `Window > Puffin > Create Module`
2. 填写模块信息
3. 创建模块结构

模块目录结构：
```
Assets/Puffin/Modules/
└── MyModule/
    ├── Runtime/
    │   ├── MyModule.asmdef
    │   └── MySystem.cs
    ├── Editor/
    │   └── MyModule.Editor.asmdef
    └── module.json
```

### module.json 示例

```json
{
    "name": "MyModule",
    "version": "1.0.0",
    "description": "我的自定义模块",
    "author": "Your Name",
    "dependencies": [
        {
            "name": "CoreModule",
            "version": ">=1.0.0"
        }
    ],
    "environments": [
        {
            "type": "nuget",
            "package": "Newtonsoft.Json",
            "version": "13.0.1"
        }
    ]
}
```


## 编辑器工具

### 系统监控窗口

`Window > Puffin > System Monitor`

- 查看所有已注册系统
- 监控系统性能（Update 耗时等）
- 启用/禁用系统

### 系统注册表窗口

`Window > Puffin > System Registry`

- 管理系统的启用/禁用状态
- 查看系统依赖关系

### 模块中心

`Window > Puffin > Module Hub`

- 浏览和安装模块
- 管理已安装模块
- 发布自定义模块

## 框架配置

在 `Assets/Puffin/Resources/PuffinSetting.asset` 中配置：

| 配置项 | 说明 |
|--------|------|
| Auto Initialize | 是否自动初始化框架 |
| Scan Mode | 扫描模式（All/Specified） |
| Require AutoRegister | 是否要求 [AutoRegister] 特性 |
| Assembly Prefixes | 要扫描的程序集前缀 |
| Enable Profiling | 启用性能统计 |
| Symbols | 条件符号列表 |
| System Info Level | 启动时输出的系统信息级别 |
| Editor Language | 编辑器语言（中文/英文） |

## 最佳实践

1. **系统职责单一** - 每个系统只负责一个功能领域
2. **使用依赖注入** - 避免直接调用 `GetSystem`，使用 `[Inject]` 特性
3. **声明依赖关系** - 使用 `[DependsOn]` 明确系统依赖
4. **事件解耦** - 系统间通信优先使用事件系统
5. **生命周期管理** - 使用 `AddTo()` 绑定事件到对象生命周期
6. **性能监控** - 开发时启用 Profiling 监控系统性能

## 依赖

- Unity 2021.3+
- UniTask（已集成）

## 许可证

MIT License
