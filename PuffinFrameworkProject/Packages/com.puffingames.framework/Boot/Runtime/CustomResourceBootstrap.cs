using Cysharp.Threading.Tasks;
using Puffin.Boot.Runtime;
using Puffin.Runtime.Core;
using UnityEngine;

namespace Puffin.Examples
{
    /// <summary>
    /// 自定义资源系统 Bootstrap 示例
    /// 演示如何在框架启动前替换资源加载器
    ///
    /// 使用方法：
    /// 1. 创建此类的实例（框架会自动扫描并创建）
    /// 2. 在 OnPreSetup 中配置自定义资源加载器
    /// 3. 框架后续的所有资源加载都会使用你的加载器
    /// </summary>
    public class CustomResourceBootstrap : IBootstrap
    {
        // 优先级设为 -1000，确保在其他 Bootstrap 之前执行
        public int Priority => -1000;

        // 设置为 true 可在编辑器模式下也执行
        public bool SupportEditorMode => false;

        public async UniTask OnPreSetup(SetupContext context)
        {
            Debug.Log("[CustomResourceBootstrap] 配置自定义资源加载器");

            // 这里可以替换为你的自定义资源加载器
            // 例如：AssetBundle、Addressables、热更新资源系统等
            // context.ResourcesLoader = new YourCustomResourceLoader();

            // 示例：如果需要异步初始化资源系统
            // await InitializeYourResourceSystem();

            await UniTask.CompletedTask;
        }

        public async UniTask OnPostSetup()
        {
            Debug.Log("[CustomResourceBootstrap] Setup 完成后的处理");

            // 这里可以执行：
            // - 热更新检查
            // - 资源预加载
            // - 版本验证等

            await UniTask.CompletedTask;
        }

        public async UniTask OnPostStart()
        {
            Debug.Log("[CustomResourceBootstrap] 框架启动完成后的处理");

            // 这里可以执行：
            // - 加载首个场景
            // - 显示主界面
            // - 播放开场动画等

            await UniTask.CompletedTask;
        }
    }
}
