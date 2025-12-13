using Cysharp.Threading.Tasks;
using Puffin.Runtime.Interfaces;
using UnityEngine;

namespace Puffin.Runtime.Core
{
    public class DefaultResourceLoader : IResourcesLoader
    {
        public async UniTask<T> LoadAsync<T>(string key) where T : Object
        {
            var t = Resources.LoadAsync<T>(key);
            await t.ToUniTask();
            return t.asset as T;
        }

        public T Load<T>(string key) where T : Object
        {
            return Resources.Load<T>(key);
        }
    }
}