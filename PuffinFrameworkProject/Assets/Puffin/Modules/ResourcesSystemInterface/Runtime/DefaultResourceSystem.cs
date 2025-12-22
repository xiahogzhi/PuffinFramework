using Cysharp.Threading.Tasks;
using Puffin.Runtime.Core;
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Interfaces.SystemEvents;
using UnityEngine;

namespace Puffin.Modules.ResourcesSystemInterface.Runtime
{
    /// <summary>
    /// 默认资源加载器，基于 Unity Resources 系统实现
    /// </summary>
    [Default]
    [AutoRegister]
    [SystemPriority(-1000)]
    public class DefaultResourceSystem : IResourcesSystem
    {
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="key">资源路径（相对于 Resources 文件夹）</param>
        /// <returns>加载的资源实例</returns>
        async UniTask<T> IResourcesLoader.LoadAsync<T>(string key)
        {
            return await PuffinFramework.ResourcesLoader.LoadAsync<T>(key);
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="key">资源路径（相对于 Resources 文件夹）</param>
        /// <returns>加载的资源实例</returns>
        T IResourcesLoader.Load<T>(string key)
        {
            return  PuffinFramework.ResourcesLoader.Load<T>(key);
        }

       
    }
}