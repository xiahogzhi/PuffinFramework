using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Puffin.Runtime.Core;
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Interfaces.SystemEvents;
using Puffin.Runtime.Tools;
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
        private readonly Dictionary<string, object> _tablesByName = new();

        public bool IsLoaded { get; private set; }

        public async UniTask OnInitializeAsync() => await LoadAsync();

        public void OnEditorInitialize() => LoadAsync().Forget();

        public async UniTask ReloadAsync()
        {
            IsLoaded = false;
            await LoadAsync();
        }

        private async UniTask LoadAsync()
        {
            try
            {
                _tables.Clear();
                _tablesByName.Clear();

                var loaderType = Type.GetType("Puffin.Modules.ConfigModule.Runtime.ConfigLoader");
                if (loaderType == null)
                {
                    Debug.LogWarning("[ConfigSystem] 未找到 ConfigLoader，请先生成配置");
                    IsLoaded = false;
                    await UniTask.Yield();
                    return;
                }

                var tablesType = (Type) loaderType.GetProperty("TablesType")?.GetValue(null);
                var createMethod = loaderType.GetMethod("CreateTables");
                if (tablesType == null || createMethod == null)
                {
                    Debug.LogWarning("[ConfigSystem] ConfigLoader 无效");
                    IsLoaded = false;
                    await UniTask.Yield();
                    return;
                }

                var paramType = createMethod.GetParameters()[0].ParameterType;
                object loader = paramType == typeof(Func<string, byte[]>)
                    ? (object) (Func<string, byte[]>) LoadBytes
                    : (Func<string, string>) LoadText;

                var tables = createMethod.Invoke(null, new[] {loader});

                foreach (var prop in tablesType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanRead) continue;
                    var table = prop.GetValue(tables);
                    _tables[prop.PropertyType] = table;
                    _tablesByName[prop.Name] = table;
                }

                IsLoaded = true;
                Debug.Log($"[ConfigSystem] 配置加载完成，共 {_tables.Count} 张表");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigSystem] 配置加载失败: {e}");
            }

            await UniTask.Yield();

            // // 获取表
            // var itemTable = GetTable<Tbitem>();
            //
            // Log.Info(itemTable);
            // // 按名称获取
            // var table2 = GetTable("Tbitem");
            // Log.Info(table2);
            //
            // // 获取所有表类型
            // var types = GetAllTableTypes();
            // Log.Info("获取所有表类型");
            // foreach (var type in types)
            // {
            //     Log.Info(type);
            // }
            //
            // // 根据 ID 获取数据
            // var item = GetById<Tbitem, item>(1001);
            // Log.Info(item);
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

        public object GetTable(Type tableType)
        {
            return _tables.TryGetValue(tableType, out var table) ? table : null;
        }

        public object GetTable(string tableName)
        {
            return _tablesByName.TryGetValue(tableName, out var table) ? table : null;
        }

        public IReadOnlyList<Type> GetAllTableTypes() => _tables.Keys.ToList();

        public TData GetById<TTable, TData>(object id) where TTable : class where TData : class
        {
            return GetById(typeof(TTable), id) as TData;
        }

        public object GetById(Type tableType, object id)
        {
            var table = GetTable(tableType);
            if (table == null) return null;

            // 尝试调用 Get 方法
            var getMethod = tableType.GetMethod("Get", new[] { id.GetType() });
            if (getMethod != null)
                return getMethod.Invoke(table, new[] { id });

            // 尝试索引器
            var indexer = tableType.GetProperty("Item", new[] { id.GetType() });
            if (indexer != null)
                return indexer.GetValue(table, new[] { id });

            return null;
        }
    }
}