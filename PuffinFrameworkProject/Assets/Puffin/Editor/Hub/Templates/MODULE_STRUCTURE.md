# 模块标准目录结构

## 完整结构示例

```
YourModule/
├── Runtime/                          # 运行时代码
│   ├── YourModule.Runtime.asmdef     # 运行时程序集定义
│   ├── Systems/                      # 系统实现
│   │   └── YourSystem.cs
│   ├── Interfaces/                   # 接口定义
│   │   └── IYourSystem.cs
│   └── ...
│
├── Editor/                           # 编辑器代码
│   ├── YourModule.Editor.asmdef      # 编辑器程序集定义
│   └── ...
│
├── Bootstrap/                        # 启动器（可选）
│   ├── YourModuleBootstrap.cs        # 模块启动器
│   └── README.md                     # 说明文档
│
├── Resources/                        # 资源文件（可选）
│   └── YourModuleSettings.asset
│
└── module.json                       # 模块配置文件
```

## Bootstrap 目录

### 用途
存放模块的启动器实现，用于在框架启动的不同阶段注入自定义逻辑。

### 特点
- **无需独立程序集**：Bootstrap 代码会被包含在 Runtime 程序集中
- **自动发现**：框架会自动扫描并执行
- **优先级控制**：通过 `Priority` 属性控制执行顺序
- **可选目录**：如果模块不需要自定义启动流程，可以不创建此目录

### 使用场景
- 自定义资源加载系统
- 热更新检查和下载
- 模块初始化配置
- 第三方 SDK 初始化
- 预加载必要资源

### 示例代码

```csharp
// Bootstrap/YourModuleBootstrap.cs
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
            // 配置阶段：替换资源加载器等
            await UniTask.CompletedTask;
        }

        public async UniTask OnPostSetup()
        {
            // Setup 后处理：热更新检查等
            await UniTask.CompletedTask;
        }

        public async UniTask OnPostStart()
        {
            // 启动后处理：加载场景等
            await UniTask.CompletedTask;
        }
    }
}
```

## 注意事项

1. **命名空间**：建议使用 `YourModule.Bootstrap` 命名空间
2. **文件命名**：建议以 `Bootstrap` 结尾
3. **程序集引用**：Bootstrap 代码需要引用 `Puffin.Boot.Runtime`
4. **无参构造函数**：Bootstrap 类必须有无参构造函数

## 模板文件

可以从以下位置复制模板：
- `Assets/Puffin/Editor/Hub/Templates/Bootstrap/BootstrapTemplate.cs.txt`
- `Assets/Puffin/Editor/Hub/Templates/Bootstrap/README.md`

## 更多信息

详细使用方法请参考：`Assets/Puffin/Boot/BOOTSTRAP.md`
