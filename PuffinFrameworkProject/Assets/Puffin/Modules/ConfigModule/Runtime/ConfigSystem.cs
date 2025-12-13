using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Puffin.Runtime.Core;
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Interfaces.SystemEvents;
using UnityEngine;

namespace Puffin.Modules.ConfigModule.Runtime
{
    /// <summary>
    /// 配置系统实现
    /// </summary>
    [AutoRegister]
    public class ConfigSystem : IConfigSystem, IInitializeAsync, IEditorSupport
    {
        private readonly Dictionary<Type, object> _tables = new();
        public bool IsLoaded { get; private set; }

        public async UniTask OnInitializeAsync()
        {
            await LoadAsync();
        }

        public void OnEditorInitialize()
        {
            LoadAsync().Forget();
        }

        private async UniTask LoadAsync()
        {
            try
            {
                _tables.Clear();

                var loaderType = Type.GetType("Puffin.Modules.ConfigModule.Runtime.ConfigLoader");
                if (loaderType == null)
                {
                    Debug.LogWarning("[ConfigSystem] 未找到 ConfigLoader，请先生成配置");
                    IsLoaded = false;
                    await UniTask.Yield();
                    return;
                }

                var tablesType = (Type)loaderType.GetProperty("TablesType")?.GetValue(null);
                var createMethod = loaderType.GetMethod("CreateTables");
                if (tablesType == null || createMethod == null)
                {
                    Debug.LogWarning("[ConfigSystem] ConfigLoader 无效");
                    IsLoaded = false;
                    await UniTask.Yield();
                    return;
                }

                // 根据参数类型决定传入 byte[] 或 string
                var paramType = createMethod.GetParameters()[0].ParameterType;
                object loader = paramType == typeof(Func<string, byte[]>)
                    ? (object)(Func<string, byte[]>)LoadBytes
                    : (Func<string, string>)LoadText;

                var tables = createMethod.Invoke(null, new[] { loader });

                foreach (var prop in tablesType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.CanRead)
                        _tables[prop.PropertyType] = prop.GetValue(tables);
                }

                IsLoaded = true;
                Debug.Log($"[ConfigSystem] 配置加载完成，共 {_tables.Count} 张表");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigSystem] 配置加载失败: {e}");
            }

            await UniTask.Yield();
        }

        private static byte[] LoadBytes(string name)
        {
            var asset = PuffinFramework.ResourcesLoader.Load<TextAsset>(name);
            if (asset == null) throw new Exception($"[ConfigSystem] 找不到: Resources/{name}");
            return asset.bytes;
        }

        private static string LoadText(string name)
        {
            var asset = PuffinFramework.ResourcesLoader.Load<TextAsset>(name);
            if (asset == null) throw new Exception($"[ConfigSystem] 找不到: Resources/{name}");
            return asset.text;
        }

        public T GetTable<T>() where T : class
        {
            return _tables.TryGetValue(typeof(T), out var table) ? table as T : null;
        }
    }
}
