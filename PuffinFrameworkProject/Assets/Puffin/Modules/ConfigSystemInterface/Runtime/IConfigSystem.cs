using System;
using System.Collections.Generic;
using Puffin.Runtime.Interfaces;
using Unity.Android.Gradle.Manifest;

namespace Puffin.Modules.ConfigSystemInterface.Runtime
{
    public interface IConfigSystem : IGameSystem
    {
        /// <summary>
        /// 是否加载
        /// </summary>
        bool IsLoaded { get; }


        /// <summary>
        /// 获取配置
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public IConfig GetConfig(Type type, object key);

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <param name="key"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetConfig<T>(object key) where T : IConfig, new();

        /// <summary>
        /// 获取所有配置
        /// </summary>
        /// <param name="key"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T[] GetConfigs<T>(object key) where T : IConfig, new();


        /// <summary>
        /// 重新加载配置
        /// </summary>
        void Reload();
    }
}