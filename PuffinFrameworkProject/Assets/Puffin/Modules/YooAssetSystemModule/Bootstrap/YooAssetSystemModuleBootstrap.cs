using Cysharp.Threading.Tasks;
using Puffin.Boot.Runtime;
using Puffin.Runtime.Core;
using UnityEngine;
using YooAsset;
using YooAssetSystemModule.Runtime;

namespace YooAssetSystemModule.Bootstrap
{
    /// <summary>
    /// YooAssetSystemModule 模块启动器
    /// </summary>
    public class YooAssetSystemModuleBootstrap : IBootstrap
    {
        /// <summary>
        /// 优先级（数值越小越先执行）
        /// -1000: 资源系统配置
        /// </summary>
        public int Priority => -1000;

        /// <summary>
        /// 在 Setup 之前执行，用于配置 SetupContext
        /// </summary>
        public async UniTask OnPreSetup(SetupContext context)
        {
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 在 Setup 之后、Start 之前执行
        /// </summary>
        public async UniTask OnPostSetup()
        {
            var settings = YooAssetSettings.Instance;

            YooAssets.Initialize();

            var package = YooAssets.TryGetPackage(settings.defaultPackageName);
            if (package == null)
            {
                package = YooAssets.CreatePackage(settings.defaultPackageName);
                YooAssets.SetDefaultPackage(package);
            }

            InitializationOperation initOperation = null;

            // switch (settings.playMode)
            // {
            //     case EPlayMode.EditorSimulateMode:
            //         var editorParams = new EditorSimulateModeParameters();
            //         editorParams.SimulateManifestFilePath = EditorSimulateModeHelper.SimulateBuild(settings.defaultPackageName);
            //         initOperation = package.InitializeAsync(editorParams);
            //         break;
            //
            //     case EPlayMode.OfflinePlayMode:
            //         var offlineParams = new OfflinePlayModeParameters();
            //         initOperation = package.InitializeAsync(offlineParams);
            //         break;
            //
            //     case EPlayMode.HostPlayMode:
            //         var hostParams = new HostPlayModeParameters();
            //         hostParams.BuildinQueryServices = new GameQueryServices();
            //         hostParams.RemoteServices = new RemoteServices(settings.hostServerURL, settings.fallbackHostServerURL);
            //         initOperation = package.InitializeAsync(hostParams);
            //         break;
            // }

            await initOperation.ToUniTask();

            if (initOperation.Status == EOperationStatus.Succeed)
            {
                Debug.Log($"YooAsset初始化成功: {settings.playMode}");
            }
            else
            {
                Debug.LogError($"YooAsset初始化失败: {initOperation.Error}");
            }
        }

        /// <summary>
        /// 在框架 Start 之后执行
        /// </summary>
        public async UniTask OnPostStart()
        {
            await UniTask.CompletedTask;
        }

        // private class GameQueryServices : IBuildinQueryServices
        // {
        //     public bool Query(string packageName, string fileName, string fileCRC)
        //     {
        //         return false;
        //     }
        // }

        private class RemoteServices : IRemoteServices
        {
            private readonly string _defaultHostServer;
            private readonly string _fallbackHostServer;

            public RemoteServices(string defaultHostServer, string fallbackHostServer)
            {
                _defaultHostServer = defaultHostServer;
                _fallbackHostServer = fallbackHostServer;
            }

            public string GetRemoteMainURL(string fileName)
            {
                return $"{_defaultHostServer}/{fileName}";
            }

            public string GetRemoteFallbackURL(string fileName)
            {
                return $"{_fallbackHostServer}/{fileName}";
            }
        }
    }
}
