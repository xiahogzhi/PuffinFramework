# Puffin Framework 项目记忆文档

## 项目概述

Puffin Framework 是一个 Unity 游戏框架，采用模块化设计，提供系统管理、事件分发、依赖注入等核心功能。

## 目录结构

```
Assets/Puffin/
├── Boot/                          # 启动模块
│   └── Runtime/
│       ├── Launcher.cs            # 框架启动器（入口点）
│       ├── LauncherSetting.cs     # 启动配置
│       ├── IBootstrap.cs          # Bootstrap 接口
│       ├── BootstrapScanner.cs    # Bootstrap 扫描器
│       ├── CustomResourceBootstrap.cs  # 示例 Bootstrap
│       └── BOOTSTRAP.md           # Bootstrap 使用文档
│
├── Editor/                        # 编辑器工具
│   ├── Core/                      # 核心编辑器功能
│   │   ├── GameScriptEditor.cs
│   │   ├── LogSettingsEditor.cs
│   │   ├── PuffinFrameworkSettingsEditor.cs
│   │   ├── PuffinSettingsWindow.cs   # 配置浏览窗口
│   │   ├── SettingsInitializer.cs
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
│   │   │   ├── NuGetInstaller.cs
│   │   │   └── UnityPackageInstaller.cs  # Unity Package Manager 安装器
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
│   │   │   ├── ManifestService.cs           # 模块清单服务
│   │   │   ├── ModuleDependencyResolver.cs  # 模块依赖解析
│   │   │   ├── ModuleInstaller.cs           # 模块安装
│   │   │   ├── ModulePublisher.cs           # 模块发布
│   │   │   ├── ModuleResolver.cs            # 模块解析
│   │   │   └── RegistryService.cs           # 仓库服务
│   │   ├── HubConstants.cs                  # Hub常量定义
│   │   ├── VersionHelper.cs                 # 版本号工具
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
    │   │   ├── DefaultAttribute.cs          # 默认系统标记
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
    │   ├── PuffinSettingAttribute.cs  # 配置窗口显示特性
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
- 支持 Bootstrap 扩展系统（无需修改核心代码）

### 6. Bootstrap 系统（启动器扩展）
**路径**: `Assets/Puffin/Boot/Runtime/`

允许模块在启动流程的不同阶段注入自定义逻辑：

**启动流程**：
```
Launcher.Setup()
  ↓
扫描 IBootstrap 实现
  ↓
OnPreSetup() - 配置 SetupContext（资源系统、日志等）
  ↓
PuffinFramework.Setup()
  ↓
OnPostSetup() - Setup 后处理（热更新、预加载等）
  ↓
Launcher.StartAsync()
  ↓
PuffinFramework.Start()
  ↓
OnPostStart() - 启动后处理（加载场景等）
```

**使用示例**：
```csharp
public class MyBootstrap : IBootstrap
{
    public int Priority => -1000; // 优先级

    public async UniTask OnPreSetup(SetupContext context)
    {
        // 替换资源加载器
        context.ResourcesLoader = new MyResourceLoader();
    }

    public async UniTask OnPostSetup()
    {
        // 热更新检查
        await CheckHotUpdate();
    }

    public async UniTask OnPostStart()
    {
        // 加载首个场景
        await LoadFirstScene();
    }
}
```

**详细文档**: `Assets/Puffin/Boot/BOOTSTRAP.md`


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
| `[Default]` | 标记默认系统实现（无其他实现时使用） |
| `[DependsOn(typeof(T))]` | 声明系统依赖 |
| `[Inject]` | 依赖注入（强依赖） |
| `[WeakInject]` | 弱依赖注入（可选） |
| `[SystemPriority(n)]` | 系统优先级 |
| `[UpdateInterval(ms)]` | 更新间隔控制 |
| `[ConditionalSystem]` | 条件系统 |
| `[SystemAlias]` | 系统别名 |
| `[PuffinSetting("名称")]` | 标记设置类在 Preference 窗口显示 |

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
    "moduleDependencies": [
        { "moduleId": "OtherModule", "version": "1.0.0", "optional": false }
    ],
    "envDependencies": [
        { "id": "UniTask", "source": 0, "type": 0, "version": "2.5.0" }
    ],
    "references": {
        "asmdefReferences": ["UniTask", "#DOTween"],
        "dllReferences": ["Newtonsoft.Json.dll", "#Optional.dll"]
    }
}
```

**引用配置说明：**
- `asmdefReferences`: 程序集定义引用（无后缀）
- `dllReferences`: DLL 引用（.dll 后缀）
- `#` 前缀表示可选引用，不存在时跳过，不会报错

**环境依赖来源 (source)：**
- 0: NuGet
- 1: GitHub Repo
- 2: Direct URL
- 3: GitHub Release
- 4: Unity Package

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
2. 创建标准子目录：
   - `Runtime/` - 运行时代码
   - `Editor/` - 编辑器代码
   - `Bootstrap/` - 启动器（可选，用于自定义启动流程）
   - `Resources/` - 资源文件（可选）
3. 创建对应的 `.asmdef` 文件
4. 创建 `module.json` 配置文件
5. 在 `.asmdef` 中添加对 `PuffinFramework.Runtime` 的引用

**Bootstrap 目录说明**：
- Bootstrap 目录用于存放模块的启动器实现
- 不需要单独的 `.asmdef` 文件，会被包含在模块的 Runtime 程序集中
- 框架会自动扫描并执行所有实现 `IBootstrap` 的类
- 可以使用模板快速创建：`Assets/Puffin/Editor/Hub/Templates/Bootstrap/`

### 默认系统机制

`[Default]` 特性用于标记接口的默认实现，提供开箱即用的功能，同时允许用户自定义替换。

**工作原理：**
1. **无其他实现时**：自动使用默认实现
2. **存在其他实现时**：优先使用非默认实现，跳过默认实现
3. **多个默认实现时**：
   - 检查 `SystemRegistrySettings.interfaceSelections` 中的用户选择
   - 如果未指定，使用第一个并记录警告

**示例：**
```csharp
// 默认资源系统（基于 Unity Resources）
[Default]
[AutoRegister]
public class DefaultResourceSystem : IResourcesSystem
{
    public T Load<T>(string key) where T : Object
    {
        return Resources.Load<T>(key);
    }
}

// 用户自定义实现（会自动替换默认实现）
[AutoRegister]
public class AddressableResourceSystem : IResourcesSystem
{
    public T Load<T>(string key) where T : Object
    {
        // 使用 Addressables 加载
    }
}
```

**配置多个默认实现：**

如果有多个默认实现，在 `SystemRegistrySettings` 中配置：
```csharp
// 在 SystemRegistrySettings.interfaceSelections 中添加：
{
    interfaceTypeName = "Puffin.Runtime.Interfaces.IResourcesSystem",
    selectedImplementation = "MyProject.CustomResourceSystem"
}
```

**注意事项：**
- 默认系统必须同时标记 `[Default]` 和 `[AutoRegister]`
- 默认系统在非默认系统之后注册
- 适用于提供框架内置功能的备选实现

## 修改注意事项

1. **全局影响评估**: 修改核心类（PuffinFramework, GameSystemRuntime, EventDispatcher）前，需要评估对所有模块的影响

2. **依赖关系**: 修改接口或基类时，需要检查所有实现类

3. **中间层抽象**: 常用功能应该抽象为接口或基类，避免直接写死代码

4. **配置优先**: 可配置的内容不要硬编码，使用 Settings 系统

5. **事件解耦**: 模块间通信优先使用事件系统，避免直接依赖

## 编辑器窗口

| 菜单路径 | 窗口 | 说明 |
|----------|------|------|
| `Puffin/Preference` | PuffinSettingsWindow | 配置浏览窗口，显示所有带 `[PuffinSetting]` 特性的设置 |
| `Puffin/Module Manager` | ModuleHubWindow | 模块管理中心，支持一键创建 Bootstrap 目录（🚀 按钮） |
| `Puffin/Environment Manager` | EnvironmentManagerWindow | 环境依赖管理 |
| `Puffin/System Monitor` | SystemMonitorWindow | 系统监控 |
| `Puffin/System Registry` | SystemRegistryWindow | 系统注册表 |

### Module Manager 快捷按钮

在模块详情面板中，已安装的模块会显示以下快捷按钮：
- 📍 定位：在 Project 窗口中定位模块
- ✏ 编辑：打开模块编辑窗口
- ⬆ 上传：发布模块到仓库
- 📦 导出：导出模块为 .unitypackage
- 🚀 创建 Bootstrap：一键创建 Bootstrap 目录和模板文件

## 关键文件路径快速索引

- 框架入口: `Assets/Puffin/Runtime/Core/PuffinFramework.cs`
- 系统运行时: `Assets/Puffin/Runtime/Core/GameSystemRuntime.cs`
- 事件分发器: `Assets/Puffin/Runtime/Events/Core/EventDispatcher.cs`
- 启动器: `Assets/Puffin/Boot/Runtime/Launcher.cs`
- 框架配置: `Assets/Puffin/Runtime/Settings/PuffinFrameworkSettings.cs`
- 配置浏览窗口: `Assets/Puffin/Editor/Core/PuffinSettingsWindow.cs`
- 模块管理: `Assets/Puffin/Editor/Hub/`
- 工具类: `Assets/Puffin/Runtime/Tools/`
