#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puffin.Editor.Hub.Data;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Environment.UI
{
    /// <summary>
    /// 模块依赖检测 UI 组件
    /// </summary>
    public static class ModuleDependencyUI
    {
        /// <summary>
        /// 绘制依赖检测界面，返回是否所有必须依赖已安装
        /// </summary>
        public static bool DrawDependencyCheck(string moduleId, DependencyManager manager)
        {
            var envDeps = GetModuleEnvDependencies(moduleId);
            if (envDeps == null || envDeps.Count == 0) return true;

            // 只检查必须依赖
            var requiredDeps = envDeps.Where(d => d.requirement == DependencyRequirement.Required).ToList();
            if (requiredDeps.Count == 0) return true;

            var missingDeps = requiredDeps.Where(d => !manager.IsInstalled(d)).ToList();
            if (missingDeps.Count == 0) return true;

            // 绘制安装界面
            EditorGUILayout.Space(20);
            GUILayout.FlexibleSpace();

            // 状态标签
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            foreach (var dep in requiredDeps)
            {
                var installed = manager.IsInstalled(dep);
                DrawStatusLabel(dep.displayName ?? dep.id, installed);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            // 安装按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("安装环境", GUILayout.Width(200), GUILayout.Height(40)))
            {
                InstallWindow.Show(missingDeps.ToArray(), () => {
                    if (EditorWindow.focusedWindow != null)
                        EditorWindow.focusedWindow.Repaint();
                });
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            return false;
        }

        /// <summary>
        /// 从 module.json 获取模块的环境依赖
        /// </summary>
        public static List<DependencyDefinition> GetModuleEnvDependencies(string moduleId)
        {
            var manifestPath = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}/module.json");
            if (!File.Exists(manifestPath)) return null;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonUtility.FromJson<HubModuleManifest>(json);
                if (manifest?.envDependencies == null) return null;

                return manifest.envDependencies.Select(ConvertToDepDefinition).ToList();
            }
            catch { return null; }
        }

        /// <summary>
        /// 根据 ID 查找环境依赖
        /// </summary>
        public static DependencyDefinition FindEnvDependency(string moduleId, string depId)
        {
            var deps = GetModuleEnvDependencies(moduleId);
            return deps?.Find(d => d.id == depId);
        }

        private static DependencyDefinition ConvertToDepDefinition(EnvironmentDependency envDep)
        {
            return new DependencyDefinition
            {
                id = envDep.id,
                displayName = envDep.id,
                source = (DependencySource)envDep.source,
                type = (DependencyType)envDep.type,
                url = envDep.url,
                version = envDep.version,
                installDir = envDep.installDir,
                extractPath = envDep.extractPath,
                requiredFiles = envDep.requiredFiles,
                targetFrameworks = envDep.targetFrameworks,
                requirement = envDep.optional ? DependencyRequirement.Optional : DependencyRequirement.Required
            };
        }

        private static void DrawStatusLabel(string label, bool ok)
        {
            var color = ok ? Color.green : Color.gray;
            var icon = ok ? "✓" : "○";
            var style = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
            GUILayout.Label($"{icon} {label}", style, GUILayout.Width(100));
        }
    }
}
#endif
