using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Puffin.Runtime.Interfaces
{
    /// <summary>
    /// 资源加载器
    /// </summary>
    public interface IResourcesLoader
    {
        public UniTask<T> LoadAsync<T>(string key) where T : Object;
        
        public T Load<T>(string key) where T : Object;
    }
}