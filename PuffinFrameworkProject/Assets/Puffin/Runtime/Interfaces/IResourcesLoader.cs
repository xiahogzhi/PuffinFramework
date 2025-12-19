using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Puffin.Runtime.Interfaces
{
    /// <summary>
    /// 资源加载器接口，定义资源加载的标准方法
    /// 可通过实现此接口对接不同的资源管理系统（如 Addressables）
    /// </summary>
    public interface IResourcesLoader
    {
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="key">资源标识符</param>
        /// <returns>加载的资源实例</returns>
        public UniTask<T> LoadAsync<T>(string key) where T : Object;

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="key">资源标识符</param>
        /// <returns>加载的资源实例</returns>
        public T Load<T>(string key) where T : Object;
    }
}