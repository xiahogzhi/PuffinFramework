# Bootstrap 启动器扩展系统

## 概述

Bootstrap 系统允许模块在框架启动的不同阶段注入自定义逻辑，无需修改 `Launcher` 核心代码。

## 启动流程

```
1. Launcher.Setup()
   ↓
2. 扫描所有 IBootstrap 实现
   ↓
3. 按优先级排序
   ↓
4. 执行 OnPreSetup() - 配置 SetupContext
   ↓
5. PuffinFramework.Setup() - 框架初始化
   ↓
6. 执行 OnPostSetup() - Setup 后处理
   ↓
7. Launcher.StartAsync()
   ↓
8. PuffinFramework.Start() - 启动系统
   ↓
9. 执行 OnPostStart() - 启动后处理
```

## 使用方法

### 1. 创建 Bootstrap 类

```csharp
using Cysharp.Threading.Tasks;
using Puffin.Boot.Runtime;
using Puffin.Runtime.Core;

public class MyCustomBootstrap : IBootstrap
{
    // 优先级：数值越小越先执行
    // -1000: 资源系统配置
    // -500: 热更新系统
    // 0: 普通模块（默认）
    // 1000: 后处理
    public int Priority => -500;

    // 是否在编辑器模式下执行（默认 false）
    // 设置为 true 时，Bootstrap 会在编辑器模式下也执行
    public bool SupportEditorMode => false;

    // 在 Setup 之前执行，配置 SetupContext
    public async UniTask OnPreSetup(SetupContext context)
    {
        // 替换资源加载器
        context.ResourcesLoader = new MyResourceLoader();

        // 替换日志系统
        context.Logger = new MyLogger();

        // 修改扫描配置
        context.ScannerConfig.AssemblyPrefixes.Add("MyGame");

        await UniTask.CompletedTask;
    }

    // 在 Setup 之后、Start 之前执行
    public async UniTask OnPostSetup()
    {
        // 热更新检查
        await CheckHotUpdate();

        // 预加载资源
        await PreloadAssets();
    }

    // 在 Start 之后执行
    public async UniTask OnPostStart()
    {
        // 加载首个场景
        await LoadFirstScene();
    }
}
```

### 2. 配置 LauncherSetting

在 Unity 编辑器中：
1. 打开 `Puffin/Preference` 窗口
2. 找到 `Launcher` 配置
3. 配置扫描选项：
   - `enableBootstrap`: 是否启用 Bootstrap 系统
   - `scanAssemblyPrefixes`: 要扫描的程序集前缀（为空则扫描所有）
   - `excludeAssemblyPrefixes`: 排除的程序集前缀
   - `showBootstrapLogs`: 是否显示 Bootstrap 执行日志

### 3. 自动发现

框架会自动扫描并执行所有实现 `IBootstrap` 的类，无需手动注册。

## 典型使用场景

### 场景 1：自定义资源系统（AssetBundle/Addressables）

```csharp
public class AssetBundleBootstrap : IBootstrap
{
    public int Priority => -1000; // 最高优先级

    public async UniTask OnPreSetup(SetupContext context)
    {
        // 初始化 AssetBundle 系统
        await AssetBundleManager.Initialize();

        // 替换资源加载器
        context.ResourcesLoader = new AssetBundleResourceLoader();
    }

    public async UniTask OnPostSetup()
    {
        // 检查资源更新
        await AssetBundleManager.CheckUpdate();
    }

    public async UniTask OnPostStart()
    {
        await UniTask.CompletedTask;
    }
}
```

### 场景 2：热更新系统

```csharp
public class HotUpdateBootstrap : IBootstrap
{
    public int Priority => -500;

    public async UniTask OnPreSetup(SetupContext context)
    {
        await UniTask.CompletedTask;
    }

    public async UniTask OnPostSetup()
    {
        // 检查热更新
        var hasUpdate = await HotUpdateManager.CheckUpdate();
        if (hasUpdate)
        {
            await HotUpdateManager.DownloadAndApply();
        }
    }

    public async UniTask OnPostStart()
    {
        await UniTask.CompletedTask;
    }
}
```

### 场景 3：第三方 SDK 初始化

```csharp
public class SDKBootstrap : IBootstrap
{
    public int Priority => 0; // 普通优先级

    public async UniTask OnPreSetup(SetupContext context)
    {
        await UniTask.CompletedTask;
    }

    public async UniTask OnPostSetup()
    {
        // 初始化第三方 SDK
        await InitializeAnalytics();
        await InitializeAds();
    }

    public async UniTask OnPostStart()
    {
        await UniTask.CompletedTask;
    }
}
```

### 场景 4：游戏启动流程

```csharp
public class GameStartBootstrap : IBootstrap
{
    public int Priority => 1000; // 最低优先级，最后执行

    public async UniTask OnPreSetup(SetupContext context)
    {
        await UniTask.CompletedTask;
    }

    public async UniTask OnPostSetup()
    {
        await UniTask.CompletedTask;
    }

    public async UniTask OnPostStart()
    {
        // 显示 Logo
        await ShowLogo();

        // 加载主菜单
        await LoadMainMenu();
    }
}
```

## 注意事项

1. **无参构造函数**：Bootstrap 类必须有无参构造函数
2. **优先级设计**：合理设置优先级，避免依赖冲突
3. **异常处理**：框架会捕获异常并记录日志，不会中断启动流程
4. **性能考虑**：避免在 Bootstrap 中执行耗时操作，使用异步方法
5. **模块隔离**：每个模块的 Bootstrap 应该独立，不依赖其他模块的 Bootstrap

## 编辑器模式支持

Bootstrap 系统支持在编辑器模式下执行，通过 `SupportEditorMode` 属性控制：

```csharp
public class EditorResourceBootstrap : IBootstrap
{
    public int Priority => -1000;

    // 设置为 true 在编辑器模式下也执行
    public bool SupportEditorMode => true;

    public async UniTask OnPreSetup(SetupContext context)
    {
        // 在编辑器模式下配置资源系统
        // 例如：使用 AssetDatabase 而不是 AssetBundle
        context.ResourcesLoader = new EditorResourceLoader();
        await UniTask.CompletedTask;
    }

    public async UniTask OnPostSetup()
    {
        // 编辑器模式下的初始化
        await UniTask.CompletedTask;
    }

    public async UniTask OnPostStart()
    {
        // 编辑器模式不会调用此方法（编辑器模式没有 Start 阶段）
        await UniTask.CompletedTask;
    }
}
```

**编辑器模式特点**：
- 仅执行 `OnPreSetup()` 阶段（在编辑器初始化时）
- 不执行 `OnPostSetup()` 和 `OnPostStart()`（编辑器模式没有这些阶段）
- 适用于配置编辑器专用的资源系统、日志系统等
- 默认 `SupportEditorMode = false`，避免不必要的执行

**使用场景**：
- 编辑器工具需要特殊的资源加载方式
- 编辑器模式下的日志配置
- 编辑器专用的系统初始化

## 与 Launcher 继承的对比

| 特性 | Bootstrap 系统 | Launcher 继承 |
|------|---------------|--------------|
| 修改核心代码 | 否 | 是 |
| 模块化 | 是 | 否 |
| 多个扩展点 | 是 | 否 |
| 优先级控制 | 是 | 否 |
| 编辑器模式支持 | 是 | 否 |
| 适用场景 | 模块级扩展 | 项目级定制 |

## 推荐实践

1. **资源系统**：使用 Bootstrap 在 OnPreSetup 中配置
2. **热更新**：使用 Bootstrap 在 OnPostSetup 中执行
3. **游戏流程**：使用 Bootstrap 在 OnPostStart 中启动
4. **项目定制**：继承 Launcher 并重写 SetupAsync/StartAsync
