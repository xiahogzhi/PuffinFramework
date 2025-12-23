# 快速创建 Bootstrap 目录

## 使用方法

1. 打开 `Puffin/Module Manager` 窗口
2. 选择一个已安装的模块
3. 点击模块详情面板右上角的 🚀 按钮
4. 在弹出的对话框中点击"创建"

## 创建内容

点击按钮后会自动创建：

```
YourModule/
└── Bootstrap/
    ├── README.md                    # 使用说明
    └── YourModuleBootstrap.cs       # Bootstrap 模板代码
```

## 模板内容

生成的 Bootstrap 类包含：
- 完整的接口实现
- 详细的注释说明
- TODO 提示
- 三个生命周期方法：
  - `OnPreSetup()` - 配置资源系统、日志等
  - `OnPostSetup()` - 热更新检查、预加载等
  - `OnPostStart()` - 加载场景、显示界面等

## 注意事项

1. **目录已存在**：如果 Bootstrap 目录已存在，会提示并取消创建
2. **自动定位**：创建成功后会自动在 Project 窗口中定位到新创建的目录
3. **无需配置**：Bootstrap 代码会自动包含在模块的 Runtime 程序集中，无需额外配置

## 下一步

创建完成后：
1. 打开生成的 `YourModuleBootstrap.cs` 文件
2. 根据注释和 TODO 提示实现你的启动逻辑
3. 框架会自动发现并执行你的 Bootstrap

## 更多信息

详细使用方法请参考：
- `Assets/Puffin/Boot/BOOTSTRAP.md` - Bootstrap 系统完整文档
- `Assets/Puffin/Editor/Hub/Templates/MODULE_STRUCTURE.md` - 模块结构说明
