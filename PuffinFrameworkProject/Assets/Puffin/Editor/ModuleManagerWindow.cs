#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puffin.Runtime.Core;
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;
using L = Puffin.Editor.Localization.EditorLocalization;

namespace Puffin.Editor
{
    public class ModuleManagerWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private List<ModuleDisplayInfo> _modules = new();
        private ModuleRegistrySettings _settings;

        // 面板模式
        private enum PanelMode { List, Create, Edit }
        private PanelMode _panelMode = PanelMode.List;

        // 编辑字段
        private ModuleDisplayInfo _editingModule;
        private string _editModuleId = "";
        private string _editDisplayName = "";
        private string _editVersion = "";
        private string _editAuthor = "";
        private string _editDescription = "";
        private List<string> _editDependencies = new();

        // 创建选项
        private bool _createEditor = true;
        private bool _createResources;
        private bool _createPlugins;
        private bool _createTests;

        private class ModuleDisplayInfo
        {
            public ModuleInfo Info;
            public string AssetPath;
            public string FolderPath;
            public bool IsEnabled;
            public bool IsDisabledByDependency;
            public bool IsConflict;
            public string DisabledReason;
        }

        [MenuItem("Puffin Framework/Module Manager")]
        public static void ShowWindow()
        {
            GetWindow<ModuleManagerWindow>("Module Manager");
        }

        private void OnEnable() => ReloadModules();

        private void ReloadModules()
        {
            _settings = ModuleRegistrySettings.Instance;
            ScanModules();
        }

        private void OnGUI()
        {
            DrawToolbar();
            switch (_panelMode)
            {
                case PanelMode.Create: DrawCreatePanel(); break;
                case PanelMode.Edit: DrawEditPanel(); break;
                default: DrawModuleList(); break;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (_panelMode != PanelMode.List)
            {
                if (GUILayout.Button("← 返回", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    _panelMode = PanelMode.List;
            }
            else
            {
                if (GUILayout.Button("+ 创建", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    _panelMode = PanelMode.Create;
                    ResetEditFields();
                }

                if (GUILayout.Button("导入", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    ImportModule();

                if (GUILayout.Button(L.L("registry.rescan"), EditorStyles.toolbarButton, GUILayout.Width(60)))
                    ReloadModules();
            }

            GUILayout.FlexibleSpace();

            if (_panelMode == PanelMode.List)
            {
                if (GUILayout.Button(L.L("registry.enable_all"), EditorStyles.toolbarButton, GUILayout.Width(70)))
                    SetAllEnabled(true);
                if (GUILayout.Button(L.L("registry.disable_all"), EditorStyles.toolbarButton, GUILayout.Width(70)))
                    SetAllEnabled(false);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawModuleList()
        {
            var enabledCount = _modules.Count(m => m.IsEnabled);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{L.L("registry.total")}: {_modules.Count}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"{L.L("common.enabled")}: {enabledCount}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"{L.L("common.disabled")}: {_modules.Count - enabledCount}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            foreach (var module in _modules)
                DrawModuleCard(module);
            EditorGUILayout.EndScrollView();
        }

        private void DrawModuleCard(ModuleDisplayInfo module)
        {
            var bgColor = module.IsEnabled ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            if (module.IsDisabledByDependency) bgColor = new Color(1f, 0.9f, 0.7f);
            if (module.IsConflict) bgColor = new Color(1f, 0.7f, 0.7f);

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            // 第一行：启用开关 + 名称 + 版本 + 操作按钮
            EditorGUILayout.BeginHorizontal();

            var isDisabled = module.IsDisabledByDependency || module.IsConflict;
            EditorGUI.BeginDisabledGroup(isDisabled);
            var newEnabled = EditorGUILayout.Toggle(module.IsEnabled && !module.IsConflict, GUILayout.Width(20));
            if (newEnabled != module.IsEnabled && !isDisabled)
                SetModuleEnabled(module, newEnabled);
            EditorGUI.EndDisabledGroup();

            var titleStyle = new GUIStyle(EditorStyles.boldLabel);
            if (!module.IsEnabled || module.IsConflict) titleStyle.normal.textColor = Color.gray;

            var displayName = module.IsConflict ? $"[冲突] {module.Info.displayName}" : module.Info.displayName;
            GUILayout.Label(displayName, titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"v{module.Info.version}", EditorStyles.miniLabel);

            // 编辑按钮
            if (GUILayout.Button("✎", GUILayout.Width(22)))
                StartEdit(module);

            // 导出按钮
            if (GUILayout.Button("↑", GUILayout.Width(22)))
                ExportModule(module);

            // 删除按钮
            if (GUILayout.Button("×", GUILayout.Width(22)))
                DeleteModule(module);

            // 定位按钮
            if (GUILayout.Button("◎", GUILayout.Width(22)))
                PingModule(module);

            EditorGUILayout.EndHorizontal();

            // 第二行：模块ID + 作者
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"ID: {module.Info.moduleId}", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(module.Info.author))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Author: {module.Info.author}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            // 第三行：说明
            if (!string.IsNullOrEmpty(module.Info.description))
            {
                var descStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 };
                GUILayout.Label(module.Info.description, descStyle);
            }

            // 第四行：依赖
            if (module.Info.dependencies != null && module.Info.dependencies.Count > 0)
            {
                var depStyle = new GUIStyle(EditorStyles.miniLabel);
                if (module.IsDisabledByDependency)
                    depStyle.normal.textColor = new Color(0.8f, 0.4f, 0f);
                GUILayout.Label($"Dependencies: {string.Join(", ", module.Info.dependencies)}", depStyle);
            }

            // 禁用原因
            if (!string.IsNullOrEmpty(module.DisabledReason))
            {
                var warnStyle = new GUIStyle(EditorStyles.miniLabel);
                warnStyle.normal.textColor = module.IsConflict ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.8f, 0.4f, 0f);
                GUILayout.Label($"⚠ {module.DisabledReason}", warnStyle);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        #region Create/Edit Panel

        private void ResetEditFields()
        {
            _editingModule = null;
            _editModuleId = "";
            _editDisplayName = "";
            _editVersion = "1.0.0";
            _editAuthor = "";
            _editDescription = "";
            _editDependencies = new List<string>();
            _createEditor = true;
            _createResources = false;
            _createPlugins = false;
            _createTests = false;
        }

        private void StartEdit(ModuleDisplayInfo module)
        {
            _panelMode = PanelMode.Edit;
            _editingModule = module;
            _editModuleId = module.Info.moduleId;
            _editDisplayName = module.Info.displayName;
            _editVersion = module.Info.version;
            _editAuthor = module.Info.author ?? "";
            _editDescription = module.Info.description ?? "";
            _editDependencies = module.Info.dependencies != null ? new List<string>(module.Info.dependencies) : new List<string>();
        }

        private void DrawCreatePanel()
        {
            GUILayout.Space(10);
            GUILayout.Label("创建新模块", EditorStyles.boldLabel);
            GUILayout.Space(5);

            DrawEditFields(true);

            GUILayout.Space(10);

            var moduleId = _editModuleId?.Trim() ?? "";
            var idExists = !string.IsNullOrEmpty(moduleId) && _modules.Any(m => m.Info.moduleId == moduleId);
            var folderExists = !string.IsNullOrEmpty(moduleId) && AssetDatabase.IsValidFolder($"Assets/Puffin/Modules/{moduleId}");

            if (idExists)
                EditorGUILayout.HelpBox($"模块 ID '{moduleId}' 已被使用", MessageType.Error);
            else if (folderExists)
                EditorGUILayout.HelpBox($"目录 'Modules/{moduleId}' 已存在", MessageType.Error);
            else if (!string.IsNullOrEmpty(moduleId))
            {
                // 构建目录预览
                var preview = $"Assets/Puffin/Modules/{moduleId}/\n";
                if (_createEditor) preview += $"  ├── Editor/ + PuffinFramework.{moduleId}.Editor.asmdef\n";
                if (_createPlugins) preview += "  ├── Plugins/\n";
                if (_createResources) preview += "  ├── Resources/\n";
                preview += $"  ├── Runtime/ + PuffinFramework.{moduleId}.Runtime.asmdef\n";
                if (_createTests) preview += "  ├── Tests/\n";
                preview += "  └── module.json";

                EditorGUILayout.HelpBox(preview, MessageType.Info);
            }

            GUILayout.Space(10);

            var canCreate = !string.IsNullOrEmpty(moduleId) && !idExists && !folderExists;
            EditorGUI.BeginDisabledGroup(!canCreate);
            if (GUILayout.Button("创建模块", GUILayout.Height(30)))
                CreateModule();
            EditorGUI.EndDisabledGroup();
        }

        private void DrawEditPanel()
        {
            GUILayout.Space(10);
            GUILayout.Label($"编辑模块: {_editingModule.Info.moduleId}", EditorStyles.boldLabel);
            GUILayout.Space(5);

            DrawEditFields(false);

            // 添加目录选项
            GUILayout.Space(5);
            GUILayout.Label("添加目录");
            EditorGUILayout.BeginHorizontal();
            var basePath = _editingModule.FolderPath;
            if (!Directory.Exists(Path.Combine(basePath, "Editor")))
            {
                if (GUILayout.Button("+ Editor", GUILayout.Width(70)))
                    AddModuleDirectory(_editingModule, "Editor", true);
            }
            if (!Directory.Exists(Path.Combine(basePath, "Resources")))
            {
                if (GUILayout.Button("+ Resources", GUILayout.Width(85)))
                    AddModuleDirectory(_editingModule, "Resources", false);
            }
            if (!Directory.Exists(Path.Combine(basePath, "Plugins")))
            {
                if (GUILayout.Button("+ Plugins", GUILayout.Width(70)))
                    AddModuleDirectory(_editingModule, "Plugins", false);
            }
            if (!Directory.Exists(Path.Combine(basePath, "Tests")))
            {
                if (GUILayout.Button("+ Tests", GUILayout.Width(60)))
                    AddModuleDirectory(_editingModule, "Tests", false);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            var moduleId = _editModuleId?.Trim() ?? "";
            var oldId = _editingModule.Info.moduleId;
            var idChanged = moduleId != oldId;
            var idConflict = idChanged && _modules.Any(m => m != _editingModule && m.Info.moduleId == moduleId);

            if (idConflict)
                EditorGUILayout.HelpBox($"模块 ID '{moduleId}' 已被使用", MessageType.Error);
            else if (idChanged && !string.IsNullOrEmpty(moduleId))
                EditorGUILayout.HelpBox($"ID 变更将重命名目录和更新 asmdef 文件", MessageType.Warning);

            GUILayout.Space(10);

            var canSave = !string.IsNullOrEmpty(moduleId) && !idConflict;
            EditorGUI.BeginDisabledGroup(!canSave);
            if (GUILayout.Button("保存", GUILayout.Height(30)))
                SaveModule();
            EditorGUI.EndDisabledGroup();
        }

        private void AddModuleDirectory(ModuleDisplayInfo module, string dirName, bool createAsmdef)
        {
            var assetPath = GetAssetPath(module.FolderPath);
            AssetDatabase.CreateFolder(assetPath, dirName);

            if (createAsmdef && dirName == "Editor")
            {
                var moduleId = module.Info.moduleId;
                var deps = module.Info.dependencies ?? new List<string>();
                var editorRefs = new List<string> { "PuffinFramework.Runtime", $"PuffinFramework.{moduleId}.Runtime", "PuffinFramework.Editor" };
                foreach (var dep in deps)
                {
                    editorRefs.Add($"PuffinFramework.{dep}.Runtime");
                    editorRefs.Add($"PuffinFramework.{dep}.Editor");
                }
                var editorAsmdef = CreateAsmdefJson($"PuffinFramework.{moduleId}.Editor", $"Puffin.Modules.{moduleId}.Editor", editorRefs, new[] { "Editor" });
                File.WriteAllText($"{module.FolderPath}/Editor/PuffinFramework.{moduleId}.Editor.asmdef", editorAsmdef);
            }

            AssetDatabase.Refresh();
        }

        private void DrawEditFields(bool isCreate)
        {
            _editModuleId = EditorGUILayout.TextField("模块 ID *", _editModuleId);
            _editDisplayName = EditorGUILayout.TextField("显示名称", _editDisplayName);
            _editVersion = EditorGUILayout.TextField("版本", _editVersion);
            _editAuthor = EditorGUILayout.TextField("作者", _editAuthor);

            GUILayout.Label("说明");
            _editDescription = EditorGUILayout.TextArea(_editDescription, GUILayout.Height(60));

            // 目录选项（仅创建时）
            if (isCreate)
            {
                GUILayout.Space(5);
                GUILayout.Label("目录选项");
                EditorGUILayout.BeginHorizontal();
                _createEditor = GUILayout.Toggle(_createEditor, "Editor", GUILayout.Width(60));
                _createResources = GUILayout.Toggle(_createResources, "Resources", GUILayout.Width(80));
                _createPlugins = GUILayout.Toggle(_createPlugins, "Plugins", GUILayout.Width(60));
                _createTests = GUILayout.Toggle(_createTests, "Tests", GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
            }

            // 依赖选择
            GUILayout.Space(5);
            GUILayout.Label("依赖模块");

            var availableModules = _modules
                .Where(m => !m.IsConflict && m.Info.moduleId != _editModuleId)
                .Select(m => m.Info.moduleId)
                .ToList();

            for (int i = 0; i < _editDependencies.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var options = new List<string> { _editDependencies[i] };
                options.AddRange(availableModules.Where(id => !_editDependencies.Contains(id)));
                var currentIndex = 0;
                var newIndex = EditorGUILayout.Popup(currentIndex, options.ToArray());
                if (newIndex != currentIndex)
                    _editDependencies[i] = options[newIndex];

                if (GUILayout.Button("-", GUILayout.Width(22)))
                {
                    _editDependencies.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            var unusedModules = availableModules.Where(id => !_editDependencies.Contains(id)).ToList();
            if (unusedModules.Count > 0)
            {
                if (GUILayout.Button("+ 添加依赖", GUILayout.Width(100)))
                    _editDependencies.Add(unusedModules[0]);
            }
        }

        #endregion

        #region Module Operations

        private void CreateModule()
        {
            var moduleId = _editModuleId.Trim();
            var basePath = $"Assets/Puffin/Modules/{moduleId}";

            if (AssetDatabase.IsValidFolder(basePath))
            {
                EditorUtility.DisplayDialog("错误", $"模块 {moduleId} 已存在", "确定");
                return;
            }

            // 创建目录
            AssetDatabase.CreateFolder("Assets/Puffin/Modules", moduleId);
            AssetDatabase.CreateFolder(basePath, "Runtime");
            if (_createEditor) AssetDatabase.CreateFolder(basePath, "Editor");
            if (_createResources) AssetDatabase.CreateFolder(basePath, "Resources");
            if (_createPlugins) AssetDatabase.CreateFolder(basePath, "Plugins");
            if (_createTests) AssetDatabase.CreateFolder(basePath, "Tests");

            // 创建 asmdef
            WriteAsmdef(basePath, moduleId, _editDependencies, _createEditor);

            // 创建 module.json
            var info = new ModuleInfo
            {
                moduleId = moduleId,
                displayName = string.IsNullOrEmpty(_editDisplayName) ? moduleId : _editDisplayName,
                version = _editVersion,
                author = _editAuthor,
                description = _editDescription,
                dependencies = new List<string>(_editDependencies)
            };
            info.SaveToJson($"{basePath}/module.json");

            AssetDatabase.Refresh();
            _panelMode = PanelMode.List;
            ReloadModules();

            var folder = AssetDatabase.LoadAssetAtPath<Object>(basePath);
            if (folder != null)
            {
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }

            Debug.Log($"[PuffinFramework] 模块 {moduleId} 创建成功");
        }

        private void SaveModule()
        {
            var newId = _editModuleId.Trim();
            var oldId = _editingModule.Info.moduleId;
            var oldPath = _editingModule.FolderPath;
            var idChanged = newId != oldId;

            // 更新 module.json
            _editingModule.Info.moduleId = newId;
            _editingModule.Info.displayName = string.IsNullOrEmpty(_editDisplayName) ? newId : _editDisplayName;
            _editingModule.Info.version = _editVersion;
            _editingModule.Info.author = _editAuthor;
            _editingModule.Info.description = _editDescription;
            _editingModule.Info.dependencies = new List<string>(_editDependencies);

            if (idChanged)
            {
                // 重命名目录
                var newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, newId);
                var assetOldPath = GetAssetPath(oldPath);
                var assetNewPath = GetAssetPath(newPath);

                AssetDatabase.MoveAsset(assetOldPath, assetNewPath);

                // 更新 asmdef
                RenameAsmdef(newPath, oldId, newId, _editDependencies);

                _editingModule.AssetPath = Path.Combine(newPath, "module.json");
                _editingModule.FolderPath = newPath;
            }
            else
            {
                // 只更新依赖
                UpdateAsmdefDependencies(oldPath, newId, _editDependencies);
            }

            _editingModule.Info.SaveToJson(_editingModule.AssetPath);

            AssetDatabase.Refresh();
            _panelMode = PanelMode.List;
            ReloadModules();

            Debug.Log($"[PuffinFramework] 模块 {newId} 保存成功");
        }

        private void DeleteModule(ModuleDisplayInfo module)
        {
            // 检查是否有其他模块依赖此模块
            var dependents = _modules
                .Where(m => m != module && m.Info.dependencies != null && m.Info.dependencies.Contains(module.Info.moduleId))
                .Select(m => m.Info.displayName)
                .ToList();

            if (dependents.Count > 0)
            {
                EditorUtility.DisplayDialog("无法删除",
                    $"以下模块依赖 '{module.Info.displayName}':\n\n{string.Join("\n", dependents)}\n\n请先移除这些依赖关系。",
                    "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("删除模块",
                $"确定要删除模块 '{module.Info.displayName}' 吗?\n\n这将删除整个模块目录，此操作不可撤销。",
                "删除", "取消"))
                return;

            var assetPath = GetAssetPath(module.FolderPath);
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.Refresh();
            ReloadModules();

            Debug.Log($"[PuffinFramework] 模块 {module.Info.moduleId} 已删除");
        }

        private void ExportModule(ModuleDisplayInfo module)
        {
            var defaultName = $"{module.Info.moduleId}-v{module.Info.version}.unitypackage";
            var path = EditorUtility.SaveFilePanel("导出模块", "", defaultName, "unitypackage");
            if (string.IsNullOrEmpty(path)) return;

            var assetPath = GetAssetPath(module.FolderPath);
            AssetDatabase.ExportPackage(assetPath, path, ExportPackageOptions.Recurse);

            Debug.Log($"[PuffinFramework] 模块 {module.Info.moduleId} 已导出到 {path}");
            EditorUtility.RevealInFinder(path);
        }

        private void ImportModule()
        {
            var path = EditorUtility.OpenFilePanel("导入模块", "", "unitypackage");
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.ImportPackage(path, true);
            // 导入完成后会自动触发 AssetDatabase.Refresh，然后用户需要手动刷新列表
        }

        private void PingModule(ModuleDisplayInfo module)
        {
            var assetPath = GetAssetPath(module.FolderPath);
            var folder = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (folder != null)
            {
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
        }

        #endregion

        #region Asmdef Operations

        private void WriteAsmdef(string basePath, string moduleId, List<string> dependencies, bool includeEditor = true)
        {
            var runtimeRefs = new List<string> { "PuffinFramework.Runtime" };
            foreach (var dep in dependencies)
                runtimeRefs.Add($"PuffinFramework.{dep}.Runtime");

            var runtimeAsmdef = CreateAsmdefJson($"PuffinFramework.{moduleId}.Runtime", $"Puffin.Modules.{moduleId}", runtimeRefs, null);
            File.WriteAllText($"{basePath}/Runtime/PuffinFramework.{moduleId}.Runtime.asmdef", runtimeAsmdef);

            if (!includeEditor) return;

            var editorRefs = new List<string>(runtimeRefs) { $"PuffinFramework.{moduleId}.Runtime", "PuffinFramework.Editor" };
            foreach (var dep in dependencies)
                editorRefs.Add($"PuffinFramework.{dep}.Editor");

            var editorAsmdef = CreateAsmdefJson($"PuffinFramework.{moduleId}.Editor", $"Puffin.Modules.{moduleId}.Editor", editorRefs, new[] { "Editor" });
            File.WriteAllText($"{basePath}/Editor/PuffinFramework.{moduleId}.Editor.asmdef", editorAsmdef);
        }

        private void RenameAsmdef(string basePath, string oldId, string newId, List<string> dependencies)
        {
            // 删除旧文件
            var oldRuntimePath = $"{basePath}/Runtime/PuffinFramework.{oldId}.Runtime.asmdef";
            var oldEditorPath = $"{basePath}/Editor/PuffinFramework.{oldId}.Editor.asmdef";
            if (File.Exists(oldRuntimePath)) File.Delete(oldRuntimePath);
            if (File.Exists(oldEditorPath)) File.Delete(oldEditorPath);

            // 写入新文件
            WriteAsmdef(basePath, newId, dependencies);
        }

        private void UpdateAsmdefDependencies(string basePath, string moduleId, List<string> dependencies)
        {
            WriteAsmdef(basePath, moduleId, dependencies);
        }

        private string CreateAsmdefJson(string name, string rootNamespace, List<string> references, string[] includePlatforms)
        {
            var refsJson = string.Join(",\n        ", references.Select(r => $"\"{r}\""));
            var platformsJson = includePlatforms != null ? string.Join(",\n        ", includePlatforms.Select(p => $"\"{p}\"")) : "";

            return $@"{{
    ""name"": ""{name}"",
    ""rootNamespace"": """",
    ""references"": [
        {refsJson}
    ],
    ""includePlatforms"": [{(string.IsNullOrEmpty(platformsJson) ? "" : $"\n        {platformsJson}\n    ")}],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";
        }

        #endregion

        #region Module Scanning

        private void ScanModules()
        {
            _modules.Clear();

            var modulesPath = Path.GetFullPath("Assets/Puffin/Modules");
            if (!Directory.Exists(modulesPath)) return;

            foreach (var dir in Directory.GetDirectories(modulesPath))
            {
                var jsonPath = Path.Combine(dir, "module.json");
                var info = ModuleInfo.LoadFromJson(jsonPath);
                if (info == null || string.IsNullOrEmpty(info.moduleId))
                    continue;

                _modules.Add(new ModuleDisplayInfo
                {
                    Info = info,
                    AssetPath = jsonPath,
                    FolderPath = dir
                });
            }

            DetectConflicts();
            SyncToSettings();
            UpdateEnabledStates();
        }

        private void DetectConflicts()
        {
            var groups = _modules.GroupBy(m => m.Info.moduleId).Where(g => g.Count() > 1);
            foreach (var group in groups)
            {
                var list = group.ToList();
                for (int i = 1; i < list.Count; i++)
                {
                    list[i].IsConflict = true;
                    list[i].DisabledReason = $"ID 冲突: 与 {list[0].AssetPath} 重复";
                }
            }
        }

        private void SyncToSettings()
        {
            var existingIds = _settings.modules.Select(m => m.moduleId).ToHashSet();
            var validModules = _modules.Where(m => !m.IsConflict);

            foreach (var module in validModules)
            {
                if (!existingIds.Contains(module.Info.moduleId))
                {
                    _settings.modules.Add(new ModuleEntry
                    {
                        moduleId = module.Info.moduleId,
                        enabled = true,
                        dependencies = module.Info.dependencies != null ? new List<string>(module.Info.dependencies) : new List<string>()
                    });
                }
                else
                {
                    var entry = _settings.modules.First(m => m.moduleId == module.Info.moduleId);
                    entry.dependencies = module.Info.dependencies != null ? new List<string>(module.Info.dependencies) : new List<string>();
                }
            }

            var validIds = validModules.Select(m => m.Info.moduleId).ToHashSet();
            _settings.modules.RemoveAll(m => !validIds.Contains(m.moduleId));

            EditorUtility.SetDirty(_settings);
        }

        private void UpdateEnabledStates()
        {
            _settings.RebuildCache();

            foreach (var module in _modules)
            {
                var entry = _settings.modules.FirstOrDefault(m => m.moduleId == module.Info.moduleId);
                var directEnabled = entry?.enabled ?? true;
                var effectiveEnabled = _settings.IsModuleEnabled(module.Info.moduleId);

                module.IsEnabled = effectiveEnabled;
                module.IsDisabledByDependency = directEnabled && !effectiveEnabled;

                if (module.IsDisabledByDependency && module.Info.dependencies != null)
                {
                    var disabledDeps = module.Info.dependencies
                        .Where(d => !_settings.IsModuleEnabled(d))
                        .ToList();
                    if (disabledDeps.Count > 0)
                        module.DisabledReason = $"Disabled due to: {string.Join(", ", disabledDeps)}";
                }
            }
        }

        private void SetModuleEnabled(ModuleDisplayInfo module, bool enabled)
        {
            var entry = _settings.modules.FirstOrDefault(m => m.moduleId == module.Info.moduleId);
            if (entry != null)
            {
                entry.enabled = enabled;
                _settings.ClearCache();
                EditorUtility.SetDirty(_settings);
                UpdateEnabledStates();
            }
        }

        private void SetAllEnabled(bool enabled)
        {
            foreach (var entry in _settings.modules)
                entry.enabled = enabled;
            _settings.ClearCache();
            EditorUtility.SetDirty(_settings);
            UpdateEnabledStates();
        }

        #endregion

        #region Utilities

        private string GetAssetPath(string fullPath)
        {
            fullPath = fullPath.Replace("\\", "/");
            var assetsPath = Path.GetFullPath("Assets").Replace("\\", "/");
            if (fullPath.StartsWith(assetsPath))
                return "Assets" + fullPath.Substring(assetsPath.Length);
            return fullPath;
        }

        #endregion
    }
}
#endif
