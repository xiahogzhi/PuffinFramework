using System;
using System.Collections.Generic;
using System.Linq;
using Puffin.Editor.Hub.Data;
using Puffin.Editor.Hub.Services;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.UI
{
    /// <summary>
    /// 创建模块窗口
    /// </summary>
    public class CreateModuleWindow : EditorWindow
    {
        private Action _onCreated;
        private string _moduleId = "";
        private string _displayName = "";
        private string _version = "1.0.0";
        private string _author = "";
        private string _description = "";
        private bool _createEditor = true;
        private bool _createResources;
        private bool _allowUnsafeCode;
        private Vector2 _scrollPos;

        // 依赖模块
        private List<ModuleDependency> _dependencies = new();
        private List<HubModuleInfo> _availableModules;
        private string _depSearchText = "";
        private List<HubModuleInfo> _filteredDepModules = new();
        private int _editingDepIndex = -1;
        private string _editingVersion;
        private string _editingRegistryId;
        private bool _editingOptional;

        // 环境依赖
        private List<EnvironmentDependency> _envDependencies = new();
        private int _editingEnvIndex = -1;
        private bool _showEnvSection;

        public static void Show(Action onCreated, List<HubModuleInfo> availableModules = null)
        {
            var window = GetWindow<CreateModuleWindow>(true, "创建模块");
            window._onCreated = onCreated;
            window._availableModules = availableModules;
            window.minSize = new Vector2(500, 550);
            window.RefreshFilteredModules();
            window.ShowUtility();
        }

        private void RefreshFilteredModules()
        {
            _filteredDepModules.Clear();
            if (_availableModules == null) return;

            var search = _depSearchText?.ToLower() ?? "";
            foreach (var m in _availableModules)
            {
                // 只显示已安装的模块（包括禁用的）
                if (!m.IsInstalled) continue;
                if (_dependencies.Exists(d => d.moduleId == m.ModuleId)) continue;
                if (string.IsNullOrEmpty(search) || m.ModuleId.ToLower().Contains(search) || (m.DisplayName?.ToLower().Contains(search) ?? false))
                    _filteredDepModules.Add(m);
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(10);
            _moduleId = EditorGUILayout.TextField("模块 ID *", _moduleId);
            _displayName = EditorGUILayout.TextField("显示名称", _displayName);
            _version = EditorGUILayout.TextField("版本", _version);
            _author = EditorGUILayout.TextField("作者", _author);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("描述");
            _description = EditorGUILayout.TextArea(_description, GUILayout.Height(40));

            // 目录结构
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("目录结构", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  Runtime (必需)", EditorStyles.miniLabel);
            _createEditor = EditorGUILayout.Toggle("  Editor", _createEditor);
            _createResources = EditorGUILayout.Toggle("  Resources", _createResources);

            // 程序集选项
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("程序集选项", EditorStyles.boldLabel);
            _allowUnsafeCode = EditorGUILayout.Toggle("  允许 Unsafe 代码", _allowUnsafeCode);

            // 依赖模块
            DrawDependenciesSection();

            // 环境依赖
            DrawEnvDependenciesSection();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            var moduleId = _moduleId?.Trim() ?? "";
            var folderExists = !string.IsNullOrEmpty(moduleId) && AssetDatabase.IsValidFolder($"Assets/Puffin/Modules/{moduleId}");

            if (folderExists)
                EditorGUILayout.HelpBox($"模块 '{moduleId}' 已存在", MessageType.Error);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Width(80))) Close();

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(moduleId) || folderExists);
            if (GUILayout.Button("创建", GUILayout.Width(80)))
            {
                CreateModule();
                Close();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void DrawDependenciesSection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("依赖模块", EditorStyles.boldLabel);

            // 显示已添加的依赖
            for (var i = 0; i < _dependencies.Count; i++)
            {
                var dep = _dependencies[i];
                var depInfo = _availableModules?.Find(m => m.ModuleId == dep.moduleId);
                var isInstalled = depInfo?.IsInstalled ?? System.IO.Directory.Exists(System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{dep.moduleId}"));

                if (_editingDepIndex == i)
                    DrawDependencyEditMode(i, dep, depInfo);
                else
                    DrawDependencyDisplayMode(i, dep, depInfo, isInstalled);
            }

            // 搜索添加依赖
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("添加依赖:", GUILayout.Width(60));
            var newSearch = EditorGUILayout.TextField(_depSearchText);
            if (newSearch != _depSearchText)
            {
                _depSearchText = newSearch;
                RefreshFilteredModules();
            }
            EditorGUILayout.EndHorizontal();

            // 显示搜索结果
            var showList = _filteredDepModules.Take(5).ToList();
            foreach (var m in showList)
                DrawSearchResultItem(m);
            if (_filteredDepModules.Count > 5)
                EditorGUILayout.LabelField($"    ... 还有 {_filteredDepModules.Count - 5} 个", EditorStyles.miniLabel);
        }

        private void DrawDependencyDisplayMode(int index, ModuleDependency dep, HubModuleInfo depInfo, bool isInstalled)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();

            var status = isInstalled ? "✓" : "⚠";
            EditorGUILayout.LabelField($"{status}", GUILayout.Width(18));
            EditorGUILayout.LabelField(dep.moduleId, EditorStyles.boldLabel, GUILayout.Width(150));

            var versionText = string.IsNullOrEmpty(dep.version) ? "最新" : $"v{dep.version}";
            EditorGUILayout.LabelField(versionText, GUILayout.Width(60));

            var registryName = GetRegistryName(dep.registryId, depInfo);
            EditorGUILayout.LabelField($"[{registryName}]", EditorStyles.miniLabel, GUILayout.Width(80));

            if (dep.optional)
            {
                var optStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.cyan } };
                EditorGUILayout.LabelField("可选", optStyle, GUILayout.Width(30));
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("✎", GUILayout.Width(25)))
            {
                _editingDepIndex = index;
                _editingVersion = dep.version;
                _editingRegistryId = dep.registryId;
                _editingOptional = dep.optional;
            }

            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                _dependencies.RemoveAt(index);
                RefreshFilteredModules();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawDependencyEditMode(int index, ModuleDependency dep, HubModuleInfo depInfo)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"编辑: {dep.moduleId}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("完成", GUILayout.Width(50)))
            {
                dep.version = _editingVersion;
                dep.registryId = _editingRegistryId;
                dep.optional = _editingOptional;
                _editingDepIndex = -1;
            }
            if (GUILayout.Button("取消", GUILayout.Width(50)))
                _editingDepIndex = -1;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 版本选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("版本:", GUILayout.Width(50));
            var versions = GetAvailableVersions(dep.moduleId, depInfo);
            var versionOptions = new List<string> { "最新" };
            versionOptions.AddRange(versions);
            var currentVersionIndex = string.IsNullOrEmpty(_editingVersion) ? 0 : versionOptions.IndexOf(_editingVersion);
            if (currentVersionIndex < 0) currentVersionIndex = 0;
            var newVersionIndex = EditorGUILayout.Popup(currentVersionIndex, versionOptions.ToArray());
            _editingVersion = newVersionIndex == 0 ? null : versionOptions[newVersionIndex];
            EditorGUILayout.EndHorizontal();

            // 仓库源选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("来源:", GUILayout.Width(50));
            var registries = GetAvailableRegistries(dep.moduleId);
            var registryOptions = new List<string> { "自动" };
            var registryIds = new List<string> { null };
            foreach (var r in registries)
            {
                registryOptions.Add(r.name);
                registryIds.Add(r.id);
            }
            var currentRegIndex = string.IsNullOrEmpty(_editingRegistryId) ? 0 : registryIds.IndexOf(_editingRegistryId);
            if (currentRegIndex < 0) currentRegIndex = 0;
            var newRegIndex = EditorGUILayout.Popup(currentRegIndex, registryOptions.ToArray());
            _editingRegistryId = registryIds[newRegIndex];
            EditorGUILayout.EndHorizontal();

            // 可选依赖
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("可选:", GUILayout.Width(50));
            _editingOptional = EditorGUILayout.Toggle(_editingOptional);
            EditorGUILayout.LabelField("(不会强制安装)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSearchResultItem(HubModuleInfo m)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);

            var registryName = m.SourceRegistryName ?? m.RegistryId ?? "";
            var displayText = $"{m.ModuleId} v{m.LatestVersion ?? m.InstalledVersion}";
            if (!string.IsNullOrEmpty(registryName)) displayText += $" [{registryName}]";

            EditorGUILayout.LabelField(displayText, GUILayout.Width(350));

            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                _dependencies.Add(new ModuleDependency(m.ModuleId, null, false, m.RegistryId));
                _depSearchText = "";
                RefreshFilteredModules();
            }

            if (GUILayout.Button("+可选", GUILayout.Width(50)))
            {
                _dependencies.Add(new ModuleDependency(m.ModuleId, null, true, m.RegistryId));
                _depSearchText = "";
                RefreshFilteredModules();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEnvDependenciesSection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            _showEnvSection = EditorGUILayout.Foldout(_showEnvSection, "环境依赖", true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                _envDependencies.Add(new EnvironmentDependency { id = "new-dependency", source = 0, type = 0 });
                _editingEnvIndex = _envDependencies.Count - 1;
                _showEnvSection = true;
            }
            EditorGUILayout.EndHorizontal();

            if (!_showEnvSection) return;

            var sourceNames = new[] { "NuGet", "GitHub Repo", "Direct URL", "GitHub Release" };
            var typeNames = new[] { "DLL", "Source", "Tool" };

            for (var i = 0; i < _envDependencies.Count; i++)
            {
                var env = _envDependencies[i];
                EditorGUILayout.BeginVertical(GUI.skin.box);

                if (_editingEnvIndex == i)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("编辑环境依赖", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("完成", GUILayout.Width(50))) _editingEnvIndex = -1;
                    EditorGUILayout.EndHorizontal();

                    env.id = EditorGUILayout.TextField("ID", env.id);
                    env.source = EditorGUILayout.Popup("来源", env.source, sourceNames);
                    env.type = EditorGUILayout.Popup("类型", env.type, typeNames);
                    env.url = EditorGUILayout.TextField("URL", env.url);
                    env.version = EditorGUILayout.TextField("版本", env.version);
                    env.installDir = EditorGUILayout.TextField("安装目录", env.installDir);
                    env.extractPath = EditorGUILayout.TextField("提取路径", env.extractPath);
                    env.optional = EditorGUILayout.Toggle("可选", env.optional);

                    EditorGUILayout.LabelField("必需文件 (逗号分隔):");
                    var filesStr = env.requiredFiles != null ? string.Join(",", env.requiredFiles) : "";
                    var newFilesStr = EditorGUILayout.TextField(filesStr);
                    env.requiredFiles = string.IsNullOrEmpty(newFilesStr) ? null : newFilesStr.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    var optLabel = env.optional ? " [可选]" : "";
                    EditorGUILayout.LabelField(env.id + optLabel, EditorStyles.boldLabel, GUILayout.Width(150));
                    EditorGUILayout.LabelField($"[{sourceNames[env.source]}]", GUILayout.Width(100));
                    if (!string.IsNullOrEmpty(env.version))
                        EditorGUILayout.LabelField($"v{env.version}", GUILayout.Width(60));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("✎", GUILayout.Width(25))) _editingEnvIndex = i;
                    if (GUILayout.Button("×", GUILayout.Width(25))) { _envDependencies.RemoveAt(i); i--; }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            if (_envDependencies.Count == 0)
                EditorGUILayout.LabelField("  无环境依赖", EditorStyles.miniLabel);
        }

        private string GetRegistryName(string registryId, HubModuleInfo depInfo)
        {
            if (string.IsNullOrEmpty(registryId))
                return depInfo?.SourceRegistryName ?? depInfo?.RegistryId ?? "自动";
            var registry = HubSettings.Instance.registries.Find(r => r.id == registryId);
            return registry?.name ?? registryId;
        }

        private List<string> GetAvailableVersions(string moduleId, HubModuleInfo depInfo)
        {
            var versions = new HashSet<string>();
            if (_availableModules != null)
            {
                foreach (var m in _availableModules)
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

        private int CompareVersions(string v1, string v2)
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

        private List<RegistrySource> GetAvailableRegistries(string moduleId)
        {
            var result = new List<RegistrySource>();
            foreach (var registry in HubSettings.Instance.GetEnabledRegistries())
            {
                var module = _availableModules?.Find(m => m.ModuleId == moduleId && m.RegistryId == registry.id);
                if (module != null)
                    result.Add(registry);
            }
            return result;
        }

        private void CreateModule()
        {
            var moduleId = _moduleId.Trim();
            var basePath = $"Assets/Puffin/Modules/{moduleId}";

            // 创建目录
            if (!AssetDatabase.IsValidFolder("Assets/Puffin/Modules"))
                AssetDatabase.CreateFolder("Assets/Puffin", "Modules");
            AssetDatabase.CreateFolder("Assets/Puffin/Modules", moduleId);
            AssetDatabase.CreateFolder(basePath, "Runtime");
            if (_createEditor) AssetDatabase.CreateFolder(basePath, "Editor");
            if (_createResources) AssetDatabase.CreateFolder(basePath, "Resources");

            // 创建 asmdef (名称: ModuleId.Runtime / ModuleId.Editor)
            var runtimeAsmdef = CreateAsmdef($"{moduleId}.Runtime", new[] { "PuffinFramework.Runtime" }, null, _allowUnsafeCode);
            System.IO.File.WriteAllText($"{Application.dataPath}/Puffin/Modules/{moduleId}/Runtime/{moduleId}.Runtime.asmdef", runtimeAsmdef);

            if (_createEditor)
            {
                var editorAsmdef = CreateAsmdef($"{moduleId}.Editor",
                    new[] { "PuffinFramework.Runtime", $"{moduleId}.Runtime", "PuffinFramework.Editor" },
                    new[] { "Editor" }, _allowUnsafeCode);
                System.IO.File.WriteAllText($"{Application.dataPath}/Puffin/Modules/{moduleId}/Editor/{moduleId}.Editor.asmdef", editorAsmdef);
            }

            // 创建 module.json
            var manifest = new HubModuleManifest
            {
                moduleId = moduleId,
                displayName = string.IsNullOrEmpty(_displayName) ? moduleId : _displayName,
                version = _version,
                author = _author,
                description = _description,
                envDependencies = _envDependencies.Count > 0 ? _envDependencies.ToArray() : null
            };
            manifest.SetDependencies(_dependencies);

            var json = JsonUtility.ToJson(manifest, true);
            System.IO.File.WriteAllText($"{Application.dataPath}/Puffin/Modules/{moduleId}/module.json", json);

            // 更新 asmdef 依赖
            var modulePath = System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
            AsmdefDependencyResolver.UpdateModuleAsmdefReferences(moduleId, modulePath, _dependencies);

            AssetDatabase.Refresh();
            _onCreated?.Invoke();

            Debug.Log($"[Hub] 模块 {moduleId} 创建成功");
        }

        private string CreateAsmdef(string name, string[] references, string[] includePlatforms, bool allowUnsafe = false)
        {
            var refsJson = string.Join(",\n        ", references.Select(r => $"\"{r}\""));
            var platformsJson = includePlatforms != null ? string.Join(",\n        ", includePlatforms.Select(p => $"\"{p}\"")) : "";

            return $@"{{
    ""name"": ""{name}"",
    ""references"": [
        {refsJson}
    ],
    ""includePlatforms"": [{(string.IsNullOrEmpty(platformsJson) ? "" : $"\n        {platformsJson}\n    ")}],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": {(allowUnsafe ? "true" : "false")},
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";
        }
    }
}
