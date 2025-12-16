# Puffin Framework 项目记忆文档

## 项目概述

Puffin Framework 是一个 Unity 游戏框架，采用模块化设计，提供系统管理、事件分发、依赖注入等核心功能。

## 目录结构

```
Assets/Puffin/
├── Boot/                          # 启动模块
│   └── Runtime/
│       ├── Launcher.cs            # 框架启动器（入口点）
│       └── LauncherSetting.cs     # 启动配置
│
├── Editor/                        # 编辑器工具
│   ├── Core/                      # 核心编辑器功能
│   │   ├── GameScriptEditor.cs
│   │   ├── LogSettingsEditor.cs
│   │   ├── PuffinFrameworkSettingsEditor.cs
│   │   ├── SettingsInitializer.cs
│   │   ├── SettingsMenuEditor.cs
│   │   ├── SystemMonitorWindow.cs
│   │   └── SystemRegistryWindow.cs
│   ├── Environment/               # 环境依赖管理
│   │   ├── Core/
│   │   │   ├── AsmdefHelper.cs    # 程序集定义辅助
│   │   │   ├── DownloadService.cs
│   │   │   ├── Downloader.cs
│   │   │   ├── EnvironmentChecker.cs
│   │   │   └── Extractor.cs
│   │   ├── Installers/            # 各类安装器
│   │   │   ├── DirectUrlInstaller.cs
│   │   │   ├── GitHubReleaseInstaller.cs
│   │   │   ├── GitHubRepoInstaller.cs
│   │   │   ├── IPackageInstaller.cs
│   │   │   └── NuGetInstaller.cs
│   │   ├── DependencyDefinition.cs
│   │   └── DependencyManager.cs
│   ├── Hub/                       # 模块管理中心（重要）
│   │   ├── Data/
│   │   │   ├── HubModuleManifest.cs
│   │   │   ├── HubSettings.cs
│   │   │   ├── InstalledModulesLock.cs
│   │   │   └── RegistrySource.cs
│   │   ├── Services/
│   │   │   ├── AsmdefDependencyResolver.cs  # 程序集依赖解析
│   │   │   ├── ModuleDependencyResolver.cs  # 模块依赖解析
│   │   │   ├── ModuleInstaller.cs           # 模块安装
│   │   │   ├── ModulePublisher.cs           # 模块发布
│   │   │   ├── ModuleResolver.cs            # 模块解析
│   │   │   └── RegistryService.cs           # 仓库服务
│   │   ├── Templates/
│   │   └── UI/
│   └── Localization/              # 编辑器本地化
│
├── Modules/                       # 功能模块目录
│   ├── TimerModule/               # 计时器模块
│   │   ├── Runtime/
│   │   │   ├── Timer.cs
│   │   │   └── TimerSystem.cs
│   │   └── module.json
│   ├── UISystemModule/            # UI系统模块
│   │   ├── Editor/
│   │   ├── Runtime/
│   │   ├── Resources/
│   │   └── module.json
│   └── t/                         # 测试模块
│
├── Resources/                     # 框架资源
│
└── Runtime/                       # 运行时核心
    ├── Behaviours/                # MonoBehaviour 扩展
    │   ├── Attributes/
    │   │   ├── AnyRefAttribute.cs
    │   │   ├── AutoCreateAttribute.cs
    │   │   ├── FindRefAttribute.cs
    │   │   ├── GetInChildrenAttribute.cs
    │   │   ├── GetInParentAttribute.cs
    │   │   └── RequiredAttribute.cs
    │   ├── Enums/
    │   └── GameScript.cs          # MonoBehaviour 基类
    ├── Core/                      # 核心系统
    │   ├── Attributes/
    │   │   ├── AutoRegisterAttribute.cs     # 自动注册
    │   │   ├── ConditionalSystemAttribute.cs # 条件系统
    │   │   ├── DependsOnAttribute.cs        # 依赖声明
    │   │   ├── InjectAttribute.cs           # 依赖注入
    │   │   ├── SystemAliasAttribute.cs      # 系统别名
    │   │   ├── SystemPriorityAttribute.cs   # 优先级
    │   │   ├── UpdateIntervalAttribute.cs   # 更新间隔
    │   │   └── WeakInjectAttribute.cs       # 弱依赖注入
    │   ├── Configs/
    │   │   ├── RuntimeConfig.cs
    │   │   └── ScannerConfig.cs
    │   ├── DefaultResourceLoader.cs
    │   ├── GameSystemRuntime.cs   # 系统运行时管理（核心）
    │   ├── GameSystemScanner.cs   # 系统扫描器
    │   ├── ModuleInfo.cs
    │   ├── PuffinFramework.cs     # 框架入口（核心）
    │   ├── PuffinFrameworkRuntimeBehaviour.cs
    │   ├── SetupContext.cs
    │   ├── SystemEventDefines.cs
    │   └── XFrameworkAutoInitializer.cs
    ├── Events/                    # 事件系统
    │   ├── Core/
    │   │   ├── EventActions.cs
    │   │   ├── EventCollector.cs
    │   │   ├── EventDispatcher.cs # 事件分发器（核心）
    │   │   ├── EventResultDestroyer.cs
    │   │   └── IEventCollector.cs
    │   ├── Enums/
    │   └── Interfaces/
    ├── Interfaces/                # 接口定义
    │   ├── SystemEvents/          # 系统生命周期接口
    │   │   ├── IApplicationFocusChanged.cs
    │   │   ├── IApplicationPause.cs
    │   │   ├── IApplicationQuit.cs
    │   │   ├── IEditorSupport.cs
    │   │   ├── IFixedUpdate.cs
    │   │   ├── IGameSystemEvent.cs
    │   │   ├── IInitializeAsync.cs
    │   │   ├── ILateUpdate.cs
    │   │   ├── IRegisterEvent.cs
    │   │   ├── ISystemEnabled.cs
    │   │   └── IUpdate.cs
    │   ├── IGameSystem.cs         # 系统基础接口
    │   ├── IPuffinLogger.cs
    │   └── IResourcesLoader.cs
    ├── Settings/                  # 配置系统
    │   ├── LogSettings.cs
    │   ├── ModuleRegistrySettings.cs
    │   ├── PuffinFrameworkSettings.cs
    │   ├── SettingsBase.cs        # 配置基类
    │   └── SystemRegistrySettings.cs
    └── Tools/                     # 工具类
        ├── FSM/
        │   ├── IState.cs
        │   └── StateMachine.cs
        ├── Pool/
        │   ├── GameObjectPool.cs
        │   ├── IPoolable.cs
        │   └── ObjectPool.cs
        ├── DefaultLogger.cs
        ├── Log.cs
        ├── SimpleJson.cs
        └── Singleton.cs
```

## 核心类说明

### 1. PuffinFramework (框架入口)
**路径**: `Assets/Puffin/Runtime/Core/PuffinFramework.cs`

静态类，提供框架的全局访问点：
- `Setup()` - 初始化框架环境
- `Start()` - 启动框架
- `GetSystem<T>()` - 获取系统实例
- `Dispatcher` - 全局事件分发器
- `Logger` - 日志系统
- `ResourcesLoader` - 资源加载器

### 2. GameSystemRuntime (系统运行时)
**路径**: `Assets/Puffin/Runtime/Core/GameSystemRuntime.cs`

管理所有游戏系统的生命周期：
- 系统注册/注销
- 依赖注入（通过 `[Inject]` 特性）
- 生命周期事件分发 (Update, FixedUpdate, LateUpdate)
- 性能统计
- 拓扑排序处理依赖关系

### 3. GameSystemScanner (系统扫描器)
**路径**: `Assets/Puffin/Runtime/Core/GameSystemScanner.cs`

自动扫描并发现实现 `IGameSystem` 接口的类：
- 支持程序集过滤
- 支持 `[AutoRegister]` 特性过滤
- 支持模块启用/禁用状态检查

### 4. EventDispatcher (事件分发器)
**路径**: `Assets/Puffin/Runtime/Events/Core/EventDispatcher.cs`

强类型事件系统：
- 支持同步/异步事件处理
- 事件优先级
- 一次性事件
- 事件拦截器
- 自动生命周期管理 (AddTo)

### 5. Launcher (启动器)
**路径**: `Assets/Puffin/Boot/Runtime/Launcher.cs`

框架启动入口：
- 运行时自动初始化 (`[RuntimeInitializeOnLoadMethod]`)
- 编辑器模式支持 (`[InitializeOnLoadMethod]`)
- 支持 `IEditorSupport` 系统在编辑器中运行

## 程序集依赖关系

```
PuffinFramework.Runtime (核心运行时)
    └── UniTask (异步支持)

PuffinFramework.Launcher (启动器)
    └── PuffinFramework.Runtime

PuffinFramework.Editor (编辑器)
    ├── PuffinFramework.Runtime
    └── UniTask

各模块.Runtime
    └── PuffinFramework.Runtime
```

## 设计模式

### 1. 服务定位器模式
```csharp
var system = PuffinFramework.GetSystem<IMySystem>();
```

### 2. 依赖注入模式
```csharp
[AutoRegister]
public class MySystem : IGameSystem
{
    [Inject] private IOtherSystem _other;        // 强依赖
    [WeakInject] private IOptionalSystem _opt;   // 弱依赖（可选）
}
```

### 3. 观察者模式（事件系统）
```csharp
// 注册事件
PuffinFramework.Dispatcher.Register<MyEvent>(e => HandleEvent(e));

// 发送事件
PuffinFramework.Dispatcher.Send(new MyEvent { Data = "test" });
```

### 4. 模板方法模式（GameScript）
```csharp
public class MyScript : GameScript
{
    protected override void OnScriptInitialize() { }
    protected override void OnScriptStart() { }
    protected override void OnEventRegister() { }
}
```

## 系统生命周期接口

| 接口 | 说明 |
|------|------|
| `IGameSystem` | 基础系统接口 |
| `IRegisterEvent` | 注册/注销回调 |
| `IInitializeAsync` | 异步初始化 |
| `IUpdate` | 每帧更新 |
| `IFixedUpdate` | 固定时间步更新 |
| `ILateUpdate` | 延迟更新 |
| `IApplicationQuit` | 应用退出 |
| `IApplicationPause` | 应用暂停 |
| `IApplicationFocusChanged` | 焦点变化 |
| `ISystemEnabled` | 可启用/禁用 |
| `IEditorSupport` | 编辑器模式支持 |

## 核心特性（Attributes）

| 特性 | 说明 |
|------|------|
| `[AutoRegister]` | 自动注册系统 |
| `[DependsOn(typeof(T))]` | 声明系统依赖 |
| `[Inject]` | 依赖注入（强依赖） |
| `[WeakInject]` | 弱依赖注入（可选） |
| `[SystemPriority(n)]` | 系统优先级 |
| `[UpdateInterval(ms)]` | 更新间隔控制 |
| `[ConditionalSystem]` | 条件系统 |
| `[SystemAlias]` | 系统别名 |

## 配置文件

### 1. 框架配置
**路径**: `Assets/Puffin/Resources/PuffinSetting.asset`
- `scanMode` - 扫描模式
- `requireAutoRegister` - 是否需要 AutoRegister 特性
- `assemblyNames` - 指定程序集
- `enableProfiling` - 性能统计
- `autoInitialize` - 自动初始化
- `editorLanguage` - 编辑器语言

### 2. 模块清单 (module.json)
```json
{
    "moduleId": "模块ID",
    "displayName": "显示名称",
    "version": "1.0.0",
    "author": "作者",
    "description": "描述",
    "dependencies": [],
    "moduleDependencies": [],
    "envDependencies": []
}
```

## 开发规范

### 创建新系统
```csharp
[AutoRegister]
[SystemPriority(100)]  // 可选：设置优先级
public class MySystem : IGameSystem, IUpdate, IRegisterEvent
{
    [Inject] private IOtherSystem _other;

    public void OnRegister() { /* 注册时调用 */ }
    public void OnUnregister() { /* 注销时调用 */ }
    public void OnUpdate() { /* 每帧调用 */ }
}
```

### 创建新模块
1. 在 `Assets/Puffin/Modules/` 下创建模块目录
2. 创建 `Runtime/` 和 `Editor/` 子目录
3. 创建对应的 `.asmdef` 文件
4. 创建 `module.json` 配置文件
5. 在 `.asmdef` 中添加对 `PuffinFramework.Runtime` 的引用

## 修改注意事项

1. **全局影响评估**: 修改核心类（PuffinFramework, GameSystemRuntime, EventDispatcher）前，需要评估对所有模块的影响

2. **依赖关系**: 修改接口或基类时，需要检查所有实现类

3. **中间层抽象**: 常用功能应该抽象为接口或基类，避免直接写死代码

4. **配置优先**: 可配置的内容不要硬编码，使用 Settings 系统

5. **事件解耦**: 模块间通信优先使用事件系统，避免直接依赖

## 关键文件路径快速索引

- 框架入口: `Assets/Puffin/Runtime/Core/PuffinFramework.cs`
- 系统运行时: `Assets/Puffin/Runtime/Core/GameSystemRuntime.cs`
- 事件分发器: `Assets/Puffin/Runtime/Events/Core/EventDispatcher.cs`
- 启动器: `Assets/Puffin/Boot/Runtime/Launcher.cs`
- 框架配置: `Assets/Puffin/Runtime/Settings/PuffinFrameworkSettings.cs`
- 模块管理: `Assets/Puffin/Editor/Hub/`
- 工具类: `Assets/Puffin/Runtime/Tools/`
