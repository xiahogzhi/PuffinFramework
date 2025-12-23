// using System;
// using System.Collections.Generic;
// using Cysharp.Threading.Tasks;
// using Puffin.Runtime.Interfaces;
//
// namespace Puffin.Modules.LubanConfigModule.Runtime
// {
//     /// <summary>
//     /// 配置系统接口
//     /// </summary>
//     public interface IConfigSystem : IGameSystem
//     {
//         /// <summary>
//         /// 配置是否已加载
//         /// </summary>
//         bool IsLoaded { get; }
//
//         /// <summary>
//         /// 获取配置表（泛型）
//         /// </summary>
//         T GetTable<T>() where T : class;
//
//         /// <summary>
//         /// 获取配置表（按类型）
//         /// </summary>
//         object GetTable(Type tableType);
//
//         /// <summary>
//         /// 获取配置表（按名称）
//         /// </summary>
//         object GetTable(string tableName);
//
//         /// <summary>
//         /// 获取所有已加载的表类型
//         /// </summary>
//         IReadOnlyList<Type> GetAllTableTypes();
//
//         /// <summary>
//         /// 根据 ID 从表中获取数据（泛型）
//         /// </summary>
//         TData GetById<TTable, TData>(object id) where TTable : class where TData : class;
//
//         /// <summary>
//         /// 根据 ID 从表中获取数据（按类型）
//         /// </summary>
//         object GetById(Type tableType, object id);
//
//         /// <summary>
//         /// 重新加载配置
//         /// </summary>
//         UniTask ReloadAsync();
//     }
// }
