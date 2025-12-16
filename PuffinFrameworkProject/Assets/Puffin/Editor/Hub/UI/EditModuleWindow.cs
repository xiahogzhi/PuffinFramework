using System;
using System.Collections.Generic;
using System.Linq;
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
        private ModuleEditorData _data;
        private string _originalId;
        private Vector2 _scrollPos;
        private bool _hasEditor, _hasResources;

        public static void Show(string modulePath, List<HubModuleInfo> availableModules, Action onSaved)
        {
            var window = GetWindow<EditModuleWindow>(true, "编辑模块");
            window._modulePath = modulePath;
            window._onSaved = onSaved;
            window._data = new ModuleEditorData { AvailableModules = availableModules };
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
                _data.Manifest = JsonUtility.FromJson<HubModuleManifest>(json);
                _originalId = _data.Manifest.moduleId;
                _data.Dependencies = _data.Manifest.GetAllDependencies();
                _data.EnvDependencies = _data.Manifest.envDependencies != null ? new List<EnvironmentDependency>(_data.Manifest.envDependencies) : new();
            }
            else
            {
                _data.Manifest = new HubModuleManifest { moduleId = System.IO.Path.GetFileName(_modulePath), version = "1.0.0" };
                _originalId = _data.Manifest.moduleId;
            }

            _data.CurrentModuleId = _data.Manifest.moduleId;
            _hasEditor = System.IO.Directory.Exists(System.IO.Path.Combine(_modulePath, "Editor"));
            _hasResources = System.IO.Directory.Exists(System.IO.Path.Combine(_modulePath, "Resources"));
        }

        private void OnGUI()
        {
            if (_data?.Manifest == null) { Close(); return; }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // 1. 基础信息
            ModuleEditorHelper.DrawBasicInfo(_data);

            // 2. 目录结构
            DrawDirectorySection();

            // 3. 依赖模块
            ModuleEditorHelper.DrawDependenciesSection(_data);

            // 4. 环境依赖
            ModuleEditorHelper.DrawEnvDependenciesSection(_data);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // 检查未安装的必需依赖
            var uninstalledDeps = _data.Dependencies
                .Where(d => !d.optional && !System.IO.Directory.Exists(System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{d.moduleId}")))
                .Select(d => d.moduleId).ToList();
            if (uninstalledDeps.Count > 0)
                EditorGUILayout.HelpBox($"以下必需依赖未安装: {string.Join(", ", uninstalledDeps)}\n保存后请手动安装这些依赖", MessageType.Warning);

            var newId = _data.Manifest.moduleId?.Trim() ?? "";
            var idChanged = newId != _originalId;
            if (idChanged)
                EditorGUILayout.HelpBox("修改 ID 将重命名模块目录", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
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
                var moduleId = _data.Manifest.moduleId;
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
            var newId = _data.Manifest.moduleId.Trim();
            var idChanged = newId != _originalId;

            // 保存依赖
            _data.Manifest.SetDependencies(_data.Dependencies);
            _data.Manifest.envDependencies = _data.EnvDependencies.Count > 0 ? _data.EnvDependencies.ToArray() : null;

            // 先保存 module.json
            var jsonPath = System.IO.Path.Combine(_modulePath, "module.json");
            var json = JsonUtility.ToJson(_data.Manifest, true);
            System.IO.File.WriteAllText(jsonPath, json);

            // 如果 ID 改变，重命名 asmdef 文件并更新内容
            if (idChanged)
            {
                RenameAsmdefs(_originalId, newId);
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
            AsmdefDependencyResolver.UpdateModuleAsmdefReferences(newId, _modulePath, _data.Dependencies);

            AssetDatabase.Refresh();
            _onSaved?.Invoke();
            Debug.Log($"[Hub] 模块 {newId} 保存成功");
        }

        private void RenameAsmdefs(string oldId, string newId)
        {
            RenameAsmdef("Runtime", oldId, newId);
            if (_hasEditor)
                RenameAsmdef("Editor", oldId, newId);
        }

        private void UpdateModuleRegistrySettings(string oldId, string newId)
        {
            var settings = ModuleRegistrySettings.Instance;
            if (settings == null) return;

            var entry = settings.modules.Find(m => m.moduleId == oldId);
            if (entry != null)
                entry.moduleId = newId;

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
            var newName = $"{newId}.{folder}";
            var newFile = System.IO.Path.Combine(dir, $"{newName}.asmdef");

            var content = System.IO.File.ReadAllText(oldFile);
            content = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"""name"":\s*""[^""]*""",
                $"\"name\": \"{newName}\"");
            content = content.Replace($"{oldId}.Runtime", $"{newId}.Runtime");
            content = content.Replace($"{oldId}.Editor", $"{newId}.Editor");
            content = content.Replace($"PuffinFramework.{oldId}.Runtime", $"{newId}.Runtime");
            content = content.Replace($"PuffinFramework.{oldId}.Editor", $"{newId}.Editor");

            System.IO.File.WriteAllText(oldFile, content);

            if (oldFile != newFile)
            {
                var oldMeta = oldFile + ".meta";
                var newMeta = newFile + ".meta";

                if (System.IO.File.Exists(oldMeta))
                    System.IO.File.Move(oldMeta, newMeta);

                System.IO.File.Move(oldFile, newFile);
            }
        }
    }
}
