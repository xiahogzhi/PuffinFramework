# Bootstrap 目录

此目录用于存放模块的启动器（Bootstrap）实现。

## 目录说明

Bootstrap 类应该放在此目录下，框架会自动扫描并执行。

## 文件组织

```
Bootstrap/
├── YourModuleBootstrap.cs    # 模块主启动器
├── ResourceBootstrap.cs       # 资源系统启动器（如果需要）
├── HotUpdateBootstrap.cs      # 热更新启动器（如果需要）
└── ...                        # 其他启动器
```

## 注意事项

1. **无需程序集引用**：Bootstrap 目录不需要单独的 .asmdef 文件
2. **自动发现**：框架会自动扫描并执行所有实现 `IBootstrap` 的类
3. **优先级**：通过 `Priority` 属性控制执行顺序
4. **命名规范**：建议以 `Bootstrap` 结尾命名

## 示例

```csharp
using Cysharp.Threading.Tasks;
using Puffin.Boot.Runtime;
using Puffin.Runtime.Core;

namespace YourModule.Bootstrap
{
    public class YourModuleBootstrap : IBootstrap
    {
        public int Priority => 0;

        public async UniTask OnPreSetup(SetupContext context)
        {
            // 配置资源系统等
            await UniTask.CompletedTask;
        }

        public async UniTask OnPostSetup()
        {
            // Setup 后处理
            await UniTask.CompletedTask;
        }

        public async UniTask OnPostStart()
        {
            // 启动后处理
            await UniTask.CompletedTask;
        }
    }
}
```

## 更多信息

详细使用方法请参考：`Assets/Puffin/Boot/BOOTSTRAP.md`
