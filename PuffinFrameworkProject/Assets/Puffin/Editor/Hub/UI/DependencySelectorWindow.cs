using System;
using System.Collections.Generic;
using System.Linq;
using Puffin.Editor.Hub;
using Puffin.Editor.Hub.Data;
using Puffin.Editor.Hub.Services;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.UI
{
    /// <summary>
    /// 依赖选择窗口 - 树状搜索列表
    /// </summary>
    public class DependencySelectorWindow : EditorWindow
    {
        private List<HubModuleInfo> _availableModules;
        private List<string> _excludeModuleIds;
        private Action<ModuleDependency> _onSelected;

        private string _searchText = "";
        private Vector2 _scrollPos;

        // 树状结构：按仓库源分组
        private Dictionary<string, List<HubModuleInfo>> _groupedModules = new();
        private HashSet<string> _expandedGroups = new();

        // 选中的模块
        private HubModuleInfo _selectedModule;
        private string _selectedVersion;
        private int _selectedVersionIndex;
        private bool _isOptional;

        public static void Show(List<HubModuleInfo> availableModules, List<string> excludeModuleIds, Action<ModuleDependency> onSelected)
        {
            var window = GetWindow<DependencySelectorWindow>(true, "选择依赖模块");
            window._availableModules = availableModules ?? new List<HubModuleInfo>();
            window._excludeModuleIds = excludeModuleIds ?? new List<string>();
            window._onSelected = onSelected;
            window.minSize = new Vector2(400, 500);
            window.maxSize = new Vector2(500, 700);
            window.RefreshGroupedModules();
            window.ShowUtility();
        }

        private void RefreshGroupedModules()
        {
            _groupedModules.Clear();
            var search = _searchText?.ToLower() ?? "";

            foreach (var m in _availableModules)
            {
                // 排除已添加的
                if (_excludeModuleIds.Contains(m.ModuleId)) continue;

                // 搜索过滤
                if (!string.IsNullOrEmpty(search))
                {
                    if (!m.ModuleId.ToLower().Contains(search) &&
                        !(m.DisplayName?.ToLower().Contains(search) ?? false))
                        continue;
                }

                // 按仓库源分组
                var registryId = m.SourceRegistryId ?? m.RegistryId ?? "local";
                if (!_groupedModules.ContainsKey(registryId))
                    _groupedModules[registryId] = new List<HubModuleInfo>();
                _groupedModules[registryId].Add(m);
            }

            // 搜索时自动展开所有组
            if (!string.IsNullOrEmpty(search))
            {
                foreach (var key in _groupedModules.Keys)
                    _expandedGroups.Add(key);
            }
        }

        private void OnGUI()
        {
            // 搜索框
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.SetNextControlName("SearchField");
            var newSearch = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                RefreshGroupedModules();
            }
            EditorGUILayout.EndHorizontal();

            // 自动聚焦搜索框
            if (Event.current.type == EventType.Repaint && string.IsNullOrEmpty(_searchText))
                EditorGUI.FocusTextInControl("SearchField");

            // 树状列表
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (_groupedModules.Count == 0)
            {
                EditorGUILayout.HelpBox("没有找到可用的模块", MessageType.Info);
            }
            else
            {
                foreach (var kvp in _groupedModules.OrderBy(k => k.Key))
                {
                    DrawRegistryGroup(kvp.Key, kvp.Value);
                }
            }

            EditorGUILayout.EndScrollView();

            // 选中模块的选项
            if (_selectedModule != null)
            {
                EditorGUILayout.Space(5);
                DrawSelectedModuleOptions();
            }
        }

        private void DrawRegistryGroup(string registryId, List<HubModuleInfo> modules)
        {
            var registryName = GetRegistryName(registryId);
            var isExpanded = _expandedGroups.Contains(registryId);

            // 组头
            EditorGUILayout.BeginHorizontal();
            var foldoutRect = GUILayoutUtility.GetRect(16, 18, GUILayout.Width(16));
            var newExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, "", true);
            if (newExpanded != isExpanded)
            {
                if (newExpanded) _expandedGroups.Add(registryId);
                else _expandedGroups.Remove(registryId);
            }

            EditorGUILayout.LabelField($"{registryName} ({modules.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            if (!isExpanded) return;

            // 模块列表
            EditorGUI.indentLevel++;
            foreach (var module in modules.OrderBy(m => m.ModuleId))
            {
                DrawModuleItem(module, registryName);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawModuleItem(HubModuleInfo module, string registryName)
        {
            var isSelected = _selectedModule == module;
            var displayName = module.DisplayName ?? module.ModuleId;
            var version = module.InstalledVersion ?? module.LatestVersion ?? "";
            var isInstalled = module.IsInstalled;

            // 背景高亮
            var rect = EditorGUILayout.BeginHorizontal();
            if (isSelected && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.91f, 0.3f));

            GUILayout.Space(20); // 缩进

            // 安装状态图标
            var statusIcon = isInstalled ? "✓ " : "  ";

            // 显示格式: hub|模块@版本
            var label = $"{statusIcon}{registryName}|{displayName}";
            if (!string.IsNullOrEmpty(version))
                label += $"@{version}";

            if (GUILayout.Button(label, EditorStyles.label))
            {
                if (_selectedModule == module)
                {
                    // 双击确认
                    ConfirmSelection();
                }
                else
                {
                    _selectedModule = module;
                    _selectedVersion = null;
                    _selectedVersionIndex = 0;
                    _isOptional = false;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedModuleOptions()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            var registryName = GetRegistryName(_selectedModule.SourceRegistryId ?? _selectedModule.RegistryId);

            // 模块标题
            EditorGUILayout.LabelField($"{registryName}|{_selectedModule.DisplayName ?? _selectedModule.ModuleId}", EditorStyles.largeLabel);

            // 基本信息
            EditorGUILayout.LabelField($"ID: {_selectedModule.ModuleId}", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(_selectedModule.Author))
                EditorGUILayout.LabelField($"作者: {_selectedModule.Author}", EditorStyles.miniLabel);
            if (_selectedModule.Tags != null && _selectedModule.Tags.Length > 0)
                EditorGUILayout.LabelField($"标签: {string.Join(", ", _selectedModule.Tags)}", EditorStyles.miniLabel);

            var installedVer = _selectedModule.InstalledVersion;
            var latestVer = _selectedModule.LatestVersion;
            if (!string.IsNullOrEmpty(installedVer))
                EditorGUILayout.LabelField($"已安装: v{installedVer}" + (_selectedModule.HasUpdate ? $" → v{latestVer}" : ""), EditorStyles.miniLabel);
            else if (!string.IsNullOrEmpty(latestVer))
                EditorGUILayout.LabelField($"最新版本: v{latestVer}", EditorStyles.miniLabel);

            // 描述
            if (!string.IsNullOrEmpty(_selectedModule.Description))
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(_selectedModule.Description, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space(5);

            // 依赖选项
            EditorGUILayout.LabelField("依赖选项", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("版本:", GUILayout.Width(50));
            var versions = GetVersionOptions();
            _selectedVersionIndex = EditorGUILayout.Popup(_selectedVersionIndex, versions);
            _selectedVersion = _selectedVersionIndex == 0 ? null : versions[_selectedVersionIndex];
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("可选:", GUILayout.Width(50));
            _isOptional = EditorGUILayout.Toggle(_isOptional);
            EditorGUILayout.LabelField("(不会强制安装)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Width(80)))
                Close();
            if (GUILayout.Button("添加", GUILayout.Width(80)))
                ConfirmSelection();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ConfirmSelection()
        {
            if (_selectedModule == null) return;

            var dep = new ModuleDependency
            {
                moduleId = _selectedModule.ModuleId,
                version = _selectedVersion,
                optional = _isOptional,
                registryId = _selectedModule.SourceRegistryId ?? _selectedModule.RegistryId
            };
            _onSelected?.Invoke(dep);
            Close();
        }

        private string GetRegistryName(string registryId)
        {
            if (string.IsNullOrEmpty(registryId) || registryId == "local") return "本地";
            var registry = HubSettings.Instance.registries.Find(r => r.id == registryId);
            return registry?.name ?? registryId;
        }

        private string[] GetVersionOptions()
        {
            var options = new List<string> { "最新" };
            if (_selectedModule.Versions != null)
            {
                var sorted = _selectedModule.Versions.OrderByDescending(v => v, VersionHelper.Comparer).ToList();
                options.AddRange(sorted);
            }
            else if (!string.IsNullOrEmpty(_selectedModule.InstalledVersion))
            {
                options.Add(_selectedModule.InstalledVersion);
            }
            return options.ToArray();
        }
    }
}
