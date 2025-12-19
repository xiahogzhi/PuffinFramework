using Cysharp.Threading.Tasks;
using Puffin.Runtime.Interfaces;
using UnityEngine;

namespace Puffin.Runtime.Core
{
    /// <summary>
    /// 默认资源加载器，基于 Unity Resources 系统实现
    /// </summary>
    public class DefaultResourceLoader : IResourcesLoader
    {
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="key">资源路径（相对于 Resources 文件夹）</param>
        /// <returns>加载的资源实例</returns>
        public async UniTask<T> LoadAsync<T>(string key) where T : Object
        {
            var t = Resources.LoadAsync<T>(key);
            await t.ToUniTask();
            return t.asset as T;
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="key">资源路径（相对于 Resources 文件夹）</param>
        /// <returns>加载的资源实例</returns>
        public T Load<T>(string key) where T : Object
        {
            return Resources.Load<T>(key);
        }
    }
}