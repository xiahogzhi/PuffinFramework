using System;
using System.Collections.Generic;
using System.Linq;
using Puffin.Editor.Environment;
using Puffin.Editor.Hub.Data;
using Puffin.Editor.Hub.Services;
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.UI
{
    /// <summary>
    /// 编辑模块窗口
    /// </summary>
    public class EditModuleWindow : EditorWindow
    {
        private string _modulePath;
        private Action _onSaved;
        private HubModuleManifest _manifest;
        private string _originalId;
        private List<ModuleDependency> _dependencies = new();
        private Vector2 _scrollPos;
        private bool _hasEditor, _hasResources;
        private List<HubModuleInfo> _availableModules;
        private string _depSearchText = "";
        private List<HubModuleInfo> _filteredDepModules = new();

        // 依赖编辑状态
        private int _editingDepIndex = -1;
        private string _editingVersion;
        private string _editingRegistryId;
        private bool _editingOptional;

        // 环境依赖
        private List<EnvironmentDependency> _envDependencies = new();
        private int _editingEnvIndex = -1;
        private bool _showEnvSection;

        public static void Show(string modulePath, List<HubModuleInfo> availableModules, Action onSaved)
        {
            var window = GetWindow<EditModuleWindow>(true, "编辑模块");
            window._modulePath = modulePath;
            window._onSaved = onSaved;
            window._availableModules = availableModules;
            window.LoadManifest();
            window.minSize = new Vector2(500, 550);
            window.ShowUtility();
        }

        private void LoadManifest()
        {
            var jsonPath = System.IO.Path.Combine(_modulePath, "module.json");
            if (System.IO.File.Exists(jsonPath))
            {
                var json = System.IO.File.ReadAllText(jsonPath);
                _manifest = JsonUtility.FromJson<HubModuleManifest>(json);
                _originalId = _manifest.moduleId;
                _dependencies = _manifest.GetAllDependencies();
                _envDependencies = _manifest.envDependencies != null ? new List<EnvironmentDependency>(_manifest.envDependencies) : new();
            }
            else
            {
                _manifest = new HubModuleManifest { moduleId = System.IO.Path.GetFileName(_modulePath), version = "1.0.0" };
                _originalId = _manifest.moduleId;
                _envDependencies = new();
            }

            _hasEditor = System.IO.Directory.Exists(System.IO.Path.Combine(_modulePath, "Editor"));
            _hasResources = System.IO.Directory.Exists(System.IO.Path.Combine(_modulePath, "Resources"));
            RefreshFilteredModules();
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
                if (m.ModuleId == _manifest.moduleId || _dependencies.Exists(d => d.moduleId == m.ModuleId)) continue;
                if (string.IsNullOrEmpty(search) || m.ModuleId.ToLower().Contains(search) || (m.DisplayName?.ToLower().Contains(search) ?? false))
                    _filteredDepModules.Add(m);
            }
        }

        private void OnGUI()
        {
            if (_manifest == null) { Close(); return; }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(10);
            _manifest.moduleId = EditorGUILayout.TextField("模块 ID", _manifest.moduleId);
            _manifest.displayName = EditorGUILayout.TextField("显示名称", _manifest.displayName);
            _manifest.version = EditorGUILayout.TextField("版本", _manifest.version);
            _manifest.author = EditorGUILayout.TextField("作者", _manifest.author);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("描述");
            _manifest.description = EditorGUILayout.TextArea(_manifest.description, GUILayout.Height(40));

            // 依赖编辑
            DrawDependenciesSection();

            // 环境依赖
            DrawEnvDependenciesSection();

            // 目录管理
            DrawDirectorySection();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // 检查未安装的必需依赖
            var uninstalledDeps = _dependencies
                .Where(d => !d.optional && !System.IO.Directory.Exists(System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{d.moduleId}")))
                .Select(d => d.moduleId).ToList();
            if (uninstalledDeps.Count > 0)
                EditorGUILayout.HelpBox($"以下必需依赖未安装: {string.Join(", ", uninstalledDeps)}\n保存后请手动安装这些依赖", MessageType.Warning);

            var newId = _manifest.moduleId?.Trim() ?? "";
            var idChanged = newId != _originalId;
            if (idChanged)
                EditorGUILayout.HelpBox("修改 ID 将重命名模块目录", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("定位", GUILayout.Width(60)))
            {
                var assetPath = "Assets" + _modulePath.Substring(Application.dataPath.Length).Replace("\\", "/");
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Width(80))) Close();

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(newId));
            if (GUILayout.Button("保存", GUILayout.Width(80)))
            {
                SaveManifest();
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
                {
                    // 编辑模式
                    DrawDependencyEditMode(i, dep, depInfo);
                }
                else
                {
                    // 显示模式
                    DrawDependencyDisplayMode(i, dep, depInfo, isInstalled);
                }
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

            // 显示搜索结果（复制列表避免迭代时修改）
            var showList = _filteredDepModules.Take(5).ToList();
            foreach (var m in showList)
                DrawSearchResultItem(m);
            if (_filteredDepModules.Count > 5)
                EditorGUILayout.LabelField($"    ... 还有 {_filteredDepModules.Count - 5} 个", EditorStyles.miniLabel);
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
                    // 编辑模式
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
                    env.keepOnUninstall = EditorGUILayout.Toggle("卸载时保留", env.keepOnUninstall);

                    EditorGUILayout.LabelField("必需文件 (逗号分隔):");
                    var filesStr = env.requiredFiles != null ? string.Join(",", env.requiredFiles) : "";
                    var newFilesStr = EditorGUILayout.TextField(filesStr);
                    env.requiredFiles = string.IsNullOrEmpty(newFilesStr) ? null : newFilesStr.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                }
                else
                {
                    // 显示模式
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

        private void DrawDependencyDisplayMode(int index, ModuleDependency dep, HubModuleInfo depInfo, bool isInstalled)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();

            // 状态图标
            var status = isInstalled ? "✓" : "⚠";
            var optionalTag = dep.optional ? " [可选]" : "";
            EditorGUILayout.LabelField($"{status}", GUILayout.Width(18));

            // 模块名称
            EditorGUILayout.LabelField(dep.moduleId, EditorStyles.boldLabel, GUILayout.Width(150));

            // 版本
            var versionText = string.IsNullOrEmpty(dep.version) ? "最新" : $"v{dep.version}";
            EditorGUILayout.LabelField(versionText, GUILayout.Width(60));

            // 来源
            var registryName = GetRegistryName(dep.registryId, depInfo);
            EditorGUILayout.LabelField($"[{registryName}]", EditorStyles.miniLabel, GUILayout.Width(80));

            // 可选标记
            if (dep.optional)
            {
                var optStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.cyan } };
                EditorGUILayout.LabelField("可选", optStyle, GUILayout.Width(30));
            }

            GUILayout.FlexibleSpace();

            // 编辑按钮
            if (GUILayout.Button("✎", GUILayout.Width(25)))
            {
                _editingDepIndex = index;
                _editingVersion = dep.version;
                _editingRegistryId = dep.registryId;
                _editingOptional = dep.optional;
            }

            // 删除按钮
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

            // 标题行
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
            {
                _editingDepIndex = -1;
            }
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

            // 添加为可选依赖
            if (GUILayout.Button("+可选", GUILayout.Width(50)))
            {
                _dependencies.Add(new ModuleDependency(m.ModuleId, null, true, m.RegistryId));
                _depSearchText = "";
                RefreshFilteredModules();
            }

            EditorGUILayout.EndHorizontal();
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

            // 从所有可用模块中收集版本
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

            // 按版本号降序排序
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

        private void DrawDirectorySection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("目录结构", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("  Editor", GUILayout.Width(100));
            if (_hasEditor)
                EditorGUILayout.LabelField("✓ 已存在", EditorStyles.miniLabel);
            else if (GUILayout.Button("添加", GUILayout.Width(60)))
                AddDirectory("Editor");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("  Resources", GUILayout.Width(100));
            if (_hasResources)
                EditorGUILayout.LabelField("✓ 已存在", EditorStyles.miniLabel);
            else if (GUILayout.Button("添加", GUILayout.Width(60)))
                AddDirectory("Resources");
            EditorGUILayout.EndHorizontal();
        }

        private void AddDirectory(string dirName)
        {
            var assetPath = "Assets" + _modulePath.Substring(Application.dataPath.Length).Replace("\\", "/");
            AssetDatabase.CreateFolder(assetPath, dirName);

            if (dirName == "Editor")
            {
                var moduleId = _manifest.moduleId;
                var asmdef = $@"{{
    ""name"": ""{moduleId}.Editor"",
    ""references"": [
        ""PuffinFramework.Runtime"",
        ""{moduleId}.Runtime"",
        ""PuffinFramework.Editor""
    ],
    ""includePlatforms"": [
        ""Editor""
    ],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(_modulePath, "Editor", $"{moduleId}.Editor.asmdef"), asmdef);
            }

            AssetDatabase.Refresh();
            _hasEditor = System.IO.Directory.Exists(System.IO.Path.Combine(_modulePath, "Editor"));
            _hasResources = System.IO.Directory.Exists(System.IO.Path.Combine(_modulePath, "Resources"));
        }

        private void SaveManifest()
        {
            var newId = _manifest.moduleId.Trim();
            var idChanged = newId != _originalId;

            // 保存依赖
            _manifest.SetDependencies(_dependencies);
            _manifest.envDependencies = _envDependencies.Count > 0 ? _envDependencies.ToArray() : null;

            // 先保存 module.json
            var jsonPath = System.IO.Path.Combine(_modulePath, "module.json");
            var json = JsonUtility.ToJson(_manifest, true);
            System.IO.File.WriteAllText(jsonPath, json);

            // 如果 ID 改变，重命名 asmdef 文件并更新内容
            if (idChanged)
            {
                RenameAsmdefs(_originalId, newId);

                // 同步更新 ModuleRegistrySettings 中的记录
                UpdateModuleRegistrySettings(_originalId, newId);

                var parentDir = System.IO.Path.GetDirectoryName(_modulePath);
                var newPath = System.IO.Path.Combine(parentDir, newId);
                var assetOldPath = "Assets" + _modulePath.Substring(Application.dataPath.Length).Replace("\\", "/");
                var assetNewPath = "Assets" + newPath.Substring(Application.dataPath.Length).Replace("\\", "/");

                var result = AssetDatabase.MoveAsset(assetOldPath, assetNewPath);
                if (!string.IsNullOrEmpty(result))
                {
                    Debug.LogError($"[Hub] 重命名失败: {result}");
                    return;
                }
                _modulePath = newPath;
            }

            // 更新 asmdef 依赖
            AsmdefDependencyResolver.UpdateModuleAsmdefReferences(newId, _modulePath, _dependencies);

            AssetDatabase.Refresh();
            _onSaved?.Invoke();
            Debug.Log($"[Hub] 模块 {newId} 保存成功");
        }

        private void RenameAsmdefs(string oldId, string newId)
        {
            // 重命名 Runtime asmdef
            RenameAsmdef("Runtime", oldId, newId);
            // 重命名 Editor asmdef
            if (_hasEditor)
                RenameAsmdef("Editor", oldId, newId);
        }

        private void UpdateModuleRegistrySettings(string oldId, string newId)
        {
            var settings = Puffin.Runtime.Settings.ModuleRegistrySettings.Instance;
            if (settings == null) return;

            // 找到旧 ID 的记录并更新
            var entry = settings.modules.Find(m => m.moduleId == oldId);
            if (entry != null)
                entry.moduleId = newId;

            // 更新其他模块依赖中的引用
            foreach (var module in settings.modules)
            {
                for (var i = 0; i < module.dependencies.Count; i++)
                {
                    if (module.dependencies[i] == oldId)
                        module.dependencies[i] = newId;
                }
            }

            settings.ClearCache();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private void RenameAsmdef(string folder, string oldId, string newId)
        {
            var dir = System.IO.Path.Combine(_modulePath, folder);
            if (!System.IO.Directory.Exists(dir)) return;

            var asmdefFiles = System.IO.Directory.GetFiles(dir, "*.asmdef");
            if (asmdefFiles.Length == 0) return;

            var oldFile = asmdefFiles[0];

            // 计算新名称 (ModuleId.Runtime 或 ModuleId.Editor)
            var newName = $"{newId}.{folder}";
            var newFile = System.IO.Path.Combine(dir, $"{newName}.asmdef");

            // 读取并更新 asmdef 内容
            var content = System.IO.File.ReadAllText(oldFile);
            // 更新 name 字段
            content = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"""name"":\s*""[^""]*""",
                $"\"name\": \"{newName}\"");
            // 更新引用中的旧模块名
            content = content.Replace($"{oldId}.Runtime", $"{newId}.Runtime");
            content = content.Replace($"{oldId}.Editor", $"{newId}.Editor");
            // 兼容旧格式 PuffinFramework.XXX
            content = content.Replace($"PuffinFramework.{oldId}.Runtime", $"{newId}.Runtime");
            content = content.Replace($"PuffinFramework.{oldId}.Editor", $"{newId}.Editor");

            System.IO.File.WriteAllText(oldFile, content);

            // 重命名文件（同时移动 .meta 文件保持 GUID）
            if (oldFile != newFile)
            {
                var oldMeta = oldFile + ".meta";
                var newMeta = newFile + ".meta";

                // 先重命名 .meta 文件
                if (System.IO.File.Exists(oldMeta))
                    System.IO.File.Move(oldMeta, newMeta);

                // 再重命名 asmdef 文件
                System.IO.File.Move(oldFile, newFile);
            }
        }
    }
}
