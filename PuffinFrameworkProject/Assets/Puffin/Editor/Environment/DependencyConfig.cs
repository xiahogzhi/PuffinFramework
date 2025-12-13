#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Puffin.Editor.Environment
{
    [Serializable]
    public class DependencyConfig
    {
        public List<DependencyDefinition> dependencies = new();

        public static DependencyConfig LoadFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return null;
            return JsonUtility.FromJson<DependencyConfig>(File.ReadAllText(jsonPath));
        } 

        public void SaveToJson(string jsonPath)
        {
            File.WriteAllText(jsonPath, JsonUtility.ToJson(this, true));
        }
    }
}
#endif
