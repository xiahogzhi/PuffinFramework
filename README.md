# PuffinFramework

轻量级 Unity 游戏框架，提供模块化的系统管理和常用游戏开发工具。

## 核心特性

- **系统管理** - 基于特性的自动注册、依赖注入、拓扑排序初始化
- **事件系统** - 支持优先级、拦截器、生命周期绑定的事件分发
- **GameScript** - MonoBehaviour 扩展，提供引用自动赋值和生命周期钩子
- **工具集** - 定时器、对象池、状态机、日志系统
- **配置模块** - Luban 集成，支持热重载

## 项目结构

```
Assets/Puffin/
├── Runtime/                    # 运行时代码
│   ├── Core/                   # 核心系统（系统管理、依赖注入、生命周期）
│   ├── Events/                 # 事件系统
│   ├── Behaviours/             # MonoBehaviour 扩展
│   ├── Settings/               # 配置管理
│   └── Tools/                  # 工具类（FSM、Pool、Timer、Log）
├── Modules/                    # 功能模块
│   └── ConfigModule/           # 配置模块（Luban 集成）
├── Editor/                     # 编辑器扩展
└── Tests/                      # 测试代码
```

## 快速开始

### 初始化框架

```csharp
await PuffinFramework.InitializeAsync();
```

### 创建系统

```csharp
[AutoRegister]
public class MySystem : IGameSystem, IUpdate
{
    [Inject] private IOtherSystem _other;

    public void OnUpdate(float deltaTime)
    {
        // 每帧更新逻辑
    }
}
```

### 获取系统

```csharp
var mySystem = PuffinFramework.GetSystem<IMySystem>();
```

### 事件系统

```csharp
// 注册事件
PuffinFramework.Dispatcher.Register<MyEvent>(e => Handle(e))
    .Priority(100)
    .Once()
    .AddTo(gameObject);

// 发送事件
PuffinFramework.Dispatcher.Send(new MyEvent { Data = value });
```

### 定时器

```csharp
Timer.Delay(2f, () => Debug.Log("延迟执行")).AddTo(gameObject);
Timer.Repeat(0.5f, () => Debug.Log("重复"), repeatCount: 5);
```

### 配置系统

```csharp
// 继承 SettingsBase<T> 创建配置
[SettingsPath("MySettings")]
public class MySettings : SettingsBase<MySettings>
{
    public int Value;
}

// 访问配置（自动从 Resources 加载，模块内配置自动生成到模块目录）
var value = MySettings.Instance.Value;
```

## 系统特性

| 特性 | 说明 |
|------|------|
| `[AutoRegister]` | 标记自动注册 |
| `[Inject]` | 必需依赖注入 |
| `[WeakInject]` | 可选依赖注入 |
| `[DependsOn(Type)]` | 声明初始化依赖 |
| `[SystemPriority(int)]` | 执行优先级 |

## 生命周期接口

| 接口 | 说明 |
|------|------|
| `IUpdate` | 每帧更新 |
| `IFixedUpdate` | 物理更新 |
| `ILateUpdate` | 后更新 |
| `IInitializeAsync` | 异步初始化 |
| `IRegisterEvent` | 注册生命周期 |

## 依赖

- UniTask

## License

MIT
