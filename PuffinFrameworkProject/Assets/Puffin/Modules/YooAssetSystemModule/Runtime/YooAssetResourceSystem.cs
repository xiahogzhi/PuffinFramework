using Cysharp.Threading.Tasks;
using Puffin.Modules.ResourcesSystemInterface.Runtime;
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Interfaces.SystemEvents;
using UnityEngine;
using YooAsset;

namespace YooAssetSystemModule.Runtime
{
    /// <summary>
    /// YooAsset资源系统实现
    /// </summary>
    [AutoRegister]
    [SystemPriority(-1000)]
    public class YooAssetResourceSystem : IResourcesSystem, ISystemInitialize
    {
        private ResourcePackage _defaultPackage;

        public async UniTask OnInitialize()
        {
            YooAssets.Initialize();
            _defaultPackage = YooAssets.TryGetPackage("DefaultPackage");
            if (_defaultPackage == null)
            {
                _defaultPackage = YooAssets.CreatePackage("DefaultPackage");
                YooAssets.SetDefaultPackage(_defaultPackage);
            }
            await UniTask.CompletedTask;
        }

        public async UniTask<T> LoadAsync<T>(string key) where T : Object
        {
            if (_defaultPackage == null)
            {
                Debug.LogError("YooAsset DefaultPackage is not initialized");
                return null;
            }

            var handle = _defaultPackage.LoadAssetAsync<T>(key);
            await handle.ToUniTask();

            if (handle.Status == EOperationStatus.Succeed)
            {
                return handle.AssetObject as T;
            }

            Debug.LogError($"Failed to load asset: {key}");
            return null;
        }

        public T Load<T>(string key) where T : Object
        {
            if (_defaultPackage == null)
            {
                Debug.LogError("YooAsset DefaultPackage is not initialized");
                return null;
            }

            var handle = _defaultPackage.LoadAssetSync<T>(key);

            if (handle.Status == EOperationStatus.Succeed)
            {
                return handle.AssetObject as T;
            }

            Debug.LogError($"Failed to load asset: {key}");
            return null;
        }

        public UniTask OnInitializeAsync()
        {
            throw new System.NotImplementedException();
        }
    }
}
