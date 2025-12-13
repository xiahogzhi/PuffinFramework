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
        public string moduleId;
        public string displayName;
        public string version = "1.0.0";
        public string author;
        public string description;
        public List<string> dependencies = new();

        public static ModuleInfo LoadFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return null;
            var json = File.ReadAllText(jsonPath);
            return JsonUtility.FromJson<ModuleInfo>(json);
        }

        public void SaveToJson(string jsonPath)
        {
            var json = JsonUtility.ToJson(this, true);
            File.WriteAllText(jsonPath, json);
        }
    }
}
