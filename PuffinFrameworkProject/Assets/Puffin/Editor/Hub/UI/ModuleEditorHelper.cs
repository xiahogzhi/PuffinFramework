using System;
using System.Collections.Generic;
using System.Linq;
using Puffin.Editor.Hub.Data;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.UI
{
    /// <summary>
    /// 模块编辑器共享数据
    /// </summary>
    public class ModuleEditorData
    {
        public HubModuleManifest Manifest;
        public List<ModuleDependency> Dependencies = new();
        public List<EnvironmentDependency> EnvDependencies = new();
        public List<HubModuleInfo> AvailableModules;

        // 依赖编辑状态
        public int EditingDepIndex = -1;
        public string EditingVersion;
        public string EditingRegistryId;
        public bool EditingOptional;

        // 环境依赖显示状态
        public bool ShowEnvSection;

        // 当前模块ID（用于过滤自身）
        public string CurrentModuleId;

        public List<string> GetExcludeModuleIds()
        {
            var list = Dependencies.Select(d => d.moduleId).ToList();
            if (!string.IsNullOrEmpty(CurrentModuleId))
                list.Add(CurrentModuleId);
            return list;
        }
    }

    /// <summary>
    /// 模块编辑器共享绘制方法
    /// </summary>
    public static class ModuleEditorHelper
    {
        /// <summary>
        /// 绘制基础信息
        /// </summary>
        public static void DrawBasicInfo(ModuleEditorData data)
        {
            EditorGUILayout.Space(10);
            data.Manifest.moduleId = EditorGUILayout.TextField("模块 ID *", data.Manifest.moduleId);
            // 同步更新 CurrentModuleId，用于排除自身依赖
            data.CurrentModuleId = data.Manifest.moduleId;
            data.Manifest.displayName = EditorGUILayout.TextField("显示名称", data.Manifest.displayName);
            data.Manifest.version = EditorGUILayout.TextField("版本", data.Manifest.version);
            data.Manifest.author = EditorGUILayout.TextField("作者", data.Manifest.author);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("描述");
            data.Manifest.description = EditorGUILayout.TextArea(data.Manifest.description, GUILayout.Height(40));
        }

        /// <summary>
        /// 绘制依赖模块部分
        /// </summary>
        public static void DrawDependenciesSection(ModuleEditorData data)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("依赖模块", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 添加依赖", GUILayout.Width(80)))
            {
                DependencySelectorWindow.Show(data.AvailableModules, data.GetExcludeModuleIds(), dep =>
                {
                    data.Dependencies.Add(dep);
                });
            }
            EditorGUILayout.EndHorizontal();

            if (data.Dependencies.Count == 0)
            {
                EditorGUILayout.LabelField("  无依赖模块", EditorStyles.miniLabel);
                return;
            }

            for (var i = 0; i < data.Dependencies.Count; i++)
            {
                var dep = data.Dependencies[i];
                var depInfo = data.AvailableModules?.Find(m => m.ModuleId == dep.moduleId);
                var isInstalled = depInfo?.IsInstalled ?? System.IO.Directory.Exists(System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{dep.moduleId}"));

                if (data.EditingDepIndex == i)
                    DrawDependencyEditMode(data, i, dep, depInfo);
                else
                    DrawDependencyDisplayMode(data, i, dep, depInfo, isInstalled);
            }
        }

        private static void DrawDependencyDisplayMode(ModuleEditorData data, int index, ModuleDependency dep, HubModuleInfo depInfo, bool isInstalled)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();

            var status = isInstalled ? "✓" : "⚠";
            EditorGUILayout.LabelField($"{status}", GUILayout.Width(18));

            // 显示格式: hub|模块@版本
            var registryName = GetRegistryName(dep.registryId, depInfo);
            var versionText = string.IsNullOrEmpty(dep.version) ? "最新" : dep.version;
            var displayText = $"{registryName}|{dep.moduleId}@{versionText}";
            EditorGUILayout.LabelField(displayText, EditorStyles.boldLabel, GUILayout.Width(250));

            if (dep.optional)
            {
                var optStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.cyan } };
                EditorGUILayout.LabelField("可选", optStyle, GUILayout.Width(30));
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("✎", GUILayout.Width(25)))
            {
                data.EditingDepIndex = index;
                data.EditingVersion = dep.version;
                data.EditingRegistryId = dep.registryId;
                data.EditingOptional = dep.optional;
            }

            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                data.Dependencies.RemoveAt(index);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static void DrawDependencyEditMode(ModuleEditorData data, int index, ModuleDependency dep, HubModuleInfo depInfo)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"编辑: {dep.moduleId}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("完成", GUILayout.Width(50)))
            {
                dep.version = data.EditingVersion;
                dep.registryId = data.EditingRegistryId;
                dep.optional = data.EditingOptional;
                data.EditingDepIndex = -1;
            }
            if (GUILayout.Button("取消", GUILayout.Width(50)))
                data.EditingDepIndex = -1;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 版本选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("版本:", GUILayout.Width(50));
            var versions = GetAvailableVersions(data, dep.moduleId, depInfo);
            var versionOptions = new List<string> { "最新" };
            versionOptions.AddRange(versions);
            var currentVersionIndex = string.IsNullOrEmpty(data.EditingVersion) ? 0 : versionOptions.IndexOf(data.EditingVersion);
            if (currentVersionIndex < 0) currentVersionIndex = 0;
            var newVersionIndex = EditorGUILayout.Popup(currentVersionIndex, versionOptions.ToArray());
            data.EditingVersion = newVersionIndex == 0 ? null : versionOptions[newVersionIndex];
            EditorGUILayout.EndHorizontal();

            // 仓库源选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("来源:", GUILayout.Width(50));
            var registries = GetAvailableRegistries(data, dep.moduleId);
            var registryOptions = new List<string> { "自动" };
            var registryIds = new List<string> { null };
            foreach (var r in registries)
            {
                registryOptions.Add(r.name);
                registryIds.Add(r.id);
            }
            var currentRegIndex = string.IsNullOrEmpty(data.EditingRegistryId) ? 0 : registryIds.IndexOf(data.EditingRegistryId);
            if (currentRegIndex < 0) currentRegIndex = 0;
            var newRegIndex = EditorGUILayout.Popup(currentRegIndex, registryOptions.ToArray());
            data.EditingRegistryId = registryIds[newRegIndex];
            EditorGUILayout.EndHorizontal();

            // 可选依赖
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("可选:", GUILayout.Width(50));
            data.EditingOptional = EditorGUILayout.Toggle(data.EditingOptional);
            EditorGUILayout.LabelField("(不会强制安装)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制环境依赖部分
        /// </summary>
        public static void DrawEnvDependenciesSection(ModuleEditorData data)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            data.ShowEnvSection = EditorGUILayout.Foldout(data.ShowEnvSection, "环境依赖", true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 添加", GUILayout.Width(60)))
            {
                EnvDependencyEditorWindow.ShowNew(env =>
                {
                    data.EnvDependencies.Add(env);
                    data.ShowEnvSection = true;
                });
            }
            EditorGUILayout.EndHorizontal();

            if (!data.ShowEnvSection) return;

            var sourceNames = new[] { "NuGet", "GitHub Repo", "Direct URL", "GitHub Release", "Unity Package" };

            for (var i = 0; i < data.EnvDependencies.Count; i++)
            {
                var env = data.EnvDependencies[i];
                var index = i; // 闭包捕获

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();

                var optLabel = env.optional ? " [可选]" : "";
                EditorGUILayout.LabelField(env.id + optLabel, EditorStyles.boldLabel, GUILayout.Width(150));
                EditorGUILayout.LabelField($"[{sourceNames[env.source]}]", GUILayout.Width(100));
                if (!string.IsNullOrEmpty(env.version))
                    EditorGUILayout.LabelField($"v{env.version}", GUILayout.Width(60));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("✎", GUILayout.Width(25)))
                {
                    EnvDependencyEditorWindow.ShowEdit(env, updated =>
                    {
                        data.EnvDependencies[index] = updated;
                    });
                }
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    data.EnvDependencies.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            if (data.EnvDependencies.Count == 0)
                EditorGUILayout.LabelField("  无环境依赖", EditorStyles.miniLabel);
        }

        public static string GetRegistryName(string registryId, HubModuleInfo depInfo)
        {
            if (string.IsNullOrEmpty(registryId))
                return depInfo?.SourceRegistryName ?? depInfo?.RegistryId ?? "自动";
            var registry = HubSettings.Instance.registries.Find(r => r.id == registryId);
            return registry?.name ?? registryId;
        }

        public static List<string> GetAvailableVersions(ModuleEditorData data, string moduleId, HubModuleInfo depInfo)
        {
            var versions = new HashSet<string>();
            if (data.AvailableModules != null)
            {
                foreach (var m in data.AvailableModules)
                {
                    if (m.ModuleId != moduleId) continue;
                    if (m.Versions != null)
                        foreach (var v in m.Versions) versions.Add(v);
                    if (!string.IsNullOrEmpty(m.LatestVersion))
                        versions.Add(m.LatestVersion);
                    if (!string.IsNullOrEmpty(m.InstalledVersion))
                        versions.Add(m.InstalledVersion);
                }
            }
            var result = versions.ToList();
            result.Sort((a, b) => CompareVersions(b, a));
            return result;
        }

        public static List<RegistrySource> GetAvailableRegistries(ModuleEditorData data, string moduleId)
        {
            var result = new List<RegistrySource>();
            foreach (var registry in HubSettings.Instance.GetEnabledRegistries())
            {
                var module = data.AvailableModules?.Find(m => m.ModuleId == moduleId && m.RegistryId == registry.id);
                if (module != null)
                    result.Add(registry);
            }
            return result;
        }

        public static int CompareVersions(string v1, string v2)
        {
            var parts1 = v1.Split('.');
            var parts2 = v2.Split('.');
            for (var i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
            {
                var p1 = i < parts1.Length && int.TryParse(parts1[i], out var n1) ? n1 : 0;
                var p2 = i < parts2.Length && int.TryParse(parts2[i], out var n2) ? n2 : 0;
                if (p1 != p2) return p1.CompareTo(p2);
            }
            return 0;
        }
    }
}
