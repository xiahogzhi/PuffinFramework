using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Puffin.Runtime.Core
{
    /// <summary>
    /// 模块信息定义
    /// 每个模块文件夹下放置一个 module.json
    /// </summary>
    [Serializable]
    public class ModuleInfo
    {
        /// <summary>
        /// 模块唯一标识符
        /// </summary>
        public string moduleId;

        /// <summary>
        /// 模块显示名称
        /// </summary>
        public string displayName;

        /// <summary>
        /// 模块版本号
        /// </summary>
        public string version = "1.0.0";

        /// <summary>
        /// 模块作者
        /// </summary>
        public string author;

        /// <summary>
        /// 模块描述
        /// </summary>
        public string description;

        /// <summary>
        /// 模块依赖列表
        /// </summary>
        public List<string> dependencies = new();

        /// <summary>
        /// 从 JSON 文件加载模块信息
        /// </summary>
        /// <param name="jsonPath">JSON 文件路径</param>
        /// <returns>模块信息实例，文件不存在则返回 null</returns>
        public static ModuleInfo LoadFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return null;
            var json = File.ReadAllText(jsonPath);
            return JsonUtility.FromJson<ModuleInfo>(json);
        }

        /// <summary>
        /// 将模块信息保存到 JSON 文件
        /// </summary>
        /// <param name="jsonPath">保存路径</param>
        public void SaveToJson(string jsonPath)
        {
            var json = JsonUtility.ToJson(this, true);
            File.WriteAllText(jsonPath, json);
        }
    }
}
