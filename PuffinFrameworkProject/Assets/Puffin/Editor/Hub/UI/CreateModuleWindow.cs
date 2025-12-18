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
        private ModuleEditorData _data;
        private Vector2 _scrollPos;

        // 创建选项
        private bool _createEditor = true;
        private bool _createResources;
        private bool _allowUnsafeCode;

        public static void Show(Action onCreated, List<HubModuleInfo> availableModules = null)
        {
            var window = GetWindow<CreateModuleWindow>(true, "创建模块");
            window._onCreated = onCreated;
            window._data = new ModuleEditorData
            {
                Manifest = new HubModuleManifest { moduleId = "", version = "1.0.0" },
                AvailableModules = availableModules
            };
            window.minSize = new Vector2(500, 550);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (_data == null) return;

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // 1. 基础信息
            ModuleEditorHelper.DrawBasicInfo(_data);

            // 2. 目录结构选项
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("目录结构", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  Runtime (必需)", EditorStyles.miniLabel);
            _createEditor = EditorGUILayout.Toggle("  Editor", _createEditor);
            _createResources = EditorGUILayout.Toggle("  Resources", _createResources);

            // 3. 程序集选项
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("程序集选项", EditorStyles.boldLabel);
            _allowUnsafeCode = EditorGUILayout.Toggle("  允许 Unsafe 代码", _allowUnsafeCode);

            // 4. 依赖模块
            ModuleEditorHelper.DrawDependenciesSection(_data);

            // 5. 环境依赖
            ModuleEditorHelper.DrawEnvDependenciesSection(_data);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            var moduleId = _data.Manifest.moduleId?.Trim() ?? "";
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

        private void CreateModule()
        {
            var moduleId = _data.Manifest.moduleId.Trim();
            var basePath = $"Assets/Puffin/Modules/{moduleId}";

            // 创建目录
            if (!AssetDatabase.IsValidFolder("Assets/Puffin/Modules"))
                AssetDatabase.CreateFolder("Assets/Puffin", "Modules");
            AssetDatabase.CreateFolder("Assets/Puffin/Modules", moduleId);
            AssetDatabase.CreateFolder(basePath, "Runtime");
            if (_createEditor) AssetDatabase.CreateFolder(basePath, "Editor");
            if (_createResources) AssetDatabase.CreateFolder(basePath, "Resources");

            // 创建 asmdef
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
            var manifest = _data.Manifest;
            manifest.moduleId = moduleId;
            if (string.IsNullOrEmpty(manifest.displayName)) manifest.displayName = moduleId;
            manifest.envDependencies = _data.EnvDependencies.Count > 0 ? _data.EnvDependencies.ToArray() : null;
            manifest.moduleDependencies = _data.Dependencies ?? new List<ModuleDependency>();

            var json = JsonUtility.ToJson(manifest, true);
            System.IO.File.WriteAllText($"{Application.dataPath}/Puffin/Modules/{moduleId}/module.json", json);

            // 更新 asmdef 依赖
            var modulePath = System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
            AsmdefDependencyResolver.UpdateReferences(moduleId, modulePath, manifest);

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
