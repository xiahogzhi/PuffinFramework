#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;
using L = Puffin.Editor.Localization.EditorLocalization;

namespace Puffin.Editor.Core
{
    /// <summary>
    /// 系统注册表窗口，管理游戏系统的启用/禁用状态和接口实现选择
    /// </summary>
    public class SystemRegistryWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private string _searchFilter = "";
        private List<ScannedSystemInfo> _scannedSystems = new();
        private SystemRegistrySettings _settings;
        private bool _showOnlyDisabled;
        private bool _showOnlyEnabled;

        // 列宽
        private float _colEnabled = 50f;
        private float _colName = 250f;
        private float _colPriority = 60f;
        private float _colDependencies = 200f;
        private const float SplitterWidth = 4f;
        private const float MinColWidth = 40f;

        // 拖拽状态
        private int _draggingCol = -1;
        private float _dragStartX;
        private float _dragStartWidth;

        private class ScannedSystemInfo
        {
            public Type Type;
            public string DisplayName;
            public string Alias;
            public int Priority;
            public List<string> Dependencies = new();
            public bool IsEnabled;
            public bool HasDisabledDependency;
            public bool IsDefault; // 是否为默认系统
            // 接口冲突信息
            public Type ConflictInterface;
            public List<Type> ConflictImplementations;
            // 模块信息
            public string ModuleId;
            public bool IsModuleDisabled;
        }

        // 被隐藏的冲突系统类型名（不从配置中删除）
        private HashSet<string> _hiddenConflictTypes = new();

        // 分组折叠状态
        private Dictionary<string, bool> _groupFoldouts = new();
        private const string CoreGroupName = "Core";

        [MenuItem("Puffin/System Registry")]
        public static void ShowWindow()
        {
            GetWindow<SystemRegistryWindow>("System Registry");
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ReloadSettings();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Play 模式结束后重新加载配置
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SystemRegistrySettings.ClearInstance();
                ReloadSettings();
            }
        }

        private void ReloadSettings()
        {
            _settings = SystemRegistrySettings.Instance;
            ScanSystems();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSystemList();
            HandleDragging();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label(L.L("common.search") + ":", GUILayout.Width(45));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));

            GUILayout.Space(10);

            _showOnlyDisabled = GUILayout.Toggle(_showOnlyDisabled, L.L("registry.show_disabled_only"), EditorStyles.toolbarButton, GUILayout.Width(90));
            _showOnlyEnabled = GUILayout.Toggle(_showOnlyEnabled, L.L("registry.show_enabled_only"), EditorStyles.toolbarButton, GUILayout.Width(90));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(L.L("registry.rescan"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ScanSystems();
            }

            if (GUILayout.Button(L.L("registry.enable_all"), EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                SetAllEnabled(true);
            }

            if (GUILayout.Button(L.L("registry.disable_all"), EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                SetAllEnabled(false);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSystemList()
        {
            // 统计
            var enabledCount = _scannedSystems.Count(s => s.IsEnabled);
            var disabledCount = _scannedSystems.Count - enabledCount;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{L.L("registry.total")}: {_scannedSystems.Count}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"{L.L("common.enabled")}: {enabledCount}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"{L.L("common.disabled")}: {disabledCount}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            // 列表
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            var filtered = _scannedSystems
                .Where(s => string.IsNullOrEmpty(_searchFilter) ||
                            s.DisplayName.ToLower().Contains(_searchFilter.ToLower()) ||
                            (s.Alias != null && s.Alias.ToLower().Contains(_searchFilter.ToLower())))
                .Where(s => !_showOnlyDisabled || !s.IsEnabled)
                .Where(s => !_showOnlyEnabled || s.IsEnabled)
                .OrderBy(s => s.Priority)
                .ToList();

            // 按模块分组
            var groups = filtered
                .GroupBy(s => string.IsNullOrEmpty(s.ModuleId) ? CoreGroupName : s.ModuleId)
                .OrderBy(g => g.Key == CoreGroupName ? 0 : 1)
                .ThenBy(g => g.Key);

            foreach (var group in groups)
            {
                DrawModuleGroup(group.Key, group.ToList());
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawModuleGroup(string groupName, List<ScannedSystemInfo> systems)
        {
            if (!_groupFoldouts.ContainsKey(groupName))
                _groupFoldouts[groupName] = true;

            var enabledCount = systems.Count(s => s.IsEnabled);
            var isModuleDisabled = systems.Any(s => s.IsModuleDisabled);

            // 分组头
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var foldoutStyle = new GUIStyle(EditorStyles.foldout);
            if (isModuleDisabled)
                foldoutStyle.normal.textColor = Color.gray;

            _groupFoldouts[groupName] = EditorGUILayout.Foldout(_groupFoldouts[groupName],
                $"{groupName} ({enabledCount}/{systems.Count})", true, foldoutStyle);

            GUILayout.FlexibleSpace();

            // 分组启用/禁用按钮
            EditorGUI.BeginDisabledGroup(isModuleDisabled);
            if (GUILayout.Button("All", EditorStyles.toolbarButton, GUILayout.Width(30)))
                SetGroupEnabled(systems, true);
            if (GUILayout.Button("None", EditorStyles.toolbarButton, GUILayout.Width(35)))
                SetGroupEnabled(systems, false);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            if (!_groupFoldouts[groupName]) return;

            // 表头
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            DrawHeaderCell(L.L("common.enabled"), _colEnabled, 0);
            DrawHeaderCell(L.L("monitor.system_name"), _colName, 1);
            DrawHeaderCell(L.L("monitor.priority"), _colPriority, 2);
            DrawHeaderCell(L.L("registry.dependencies"), _colDependencies, -1);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 系统列表
            foreach (var system in systems)
                DrawSystemRow(system);
        }

        private void SetGroupEnabled(List<ScannedSystemInfo> systems, bool enabled)
        {
            foreach (var system in systems.Where(s => !s.IsModuleDisabled))
                system.IsEnabled = enabled;
            UpdateDependencyStatus();
            SaveSettings();
        }

        private void DrawHeaderCell(string label, float width, int colIndex)
        {
            GUILayout.Label(label, GUILayout.Width(width));

            if (colIndex >= 0)
            {
                var splitterRect = GUILayoutUtility.GetRect(SplitterWidth, 18f, GUILayout.Width(SplitterWidth));
                var lineRect = new Rect(splitterRect.x + 1, splitterRect.y + 2, 1, splitterRect.height - 4);
                EditorGUI.DrawRect(lineRect, new Color(0.3f, 0.3f, 0.3f));
                EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

                if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
                {
                    _draggingCol = colIndex;
                    _dragStartX = Event.current.mousePosition.x;
                    _dragStartWidth = GetColumnWidth(colIndex);
                    Event.current.Use();
                }
            }
        }

        private float GetColumnWidth(int colIndex)
        {
            return colIndex switch
            {
                0 => _colEnabled,
                1 => _colName,
                2 => _colPriority,
                _ => 0
            };
        }

        private void SetColumnWidth(int colIndex, float width)
        {
            width = Mathf.Max(MinColWidth, width);
            switch (colIndex)
            {
                case 0: _colEnabled = width; break;
                case 1: _colName = width; break;
                case 2: _colPriority = width; break;
            }
        }

        private void HandleDragging()
        {
            if (_draggingCol < 0) return;

            if (Event.current.type == EventType.MouseDrag)
            {
                var delta = Event.current.mousePosition.x - _dragStartX;
                SetColumnWidth(_draggingCol, _dragStartWidth + delta);
                Repaint();
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                _draggingCol = -1;
                Event.current.Use();
            }
        }

        private void DrawSystemRow(ScannedSystemInfo system)
        {
            var bgColor = system.IsEnabled ? Color.white : new Color(0.8f, 0.8f, 0.8f);
            if (system.HasDisabledDependency)
                bgColor = new Color(1f, 0.9f, 0.7f);
            if (system.IsModuleDisabled)
                bgColor = new Color(0.7f, 0.7f, 0.8f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16); // 缩进

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            // 启用复选框
            EditorGUI.BeginDisabledGroup(system.IsModuleDisabled);
            var newEnabled = EditorGUILayout.Toggle(system.IsEnabled, GUILayout.Width(_colEnabled + SplitterWidth));
            if (newEnabled != system.IsEnabled && !system.IsModuleDisabled)
            {
                system.IsEnabled = newEnabled;
                if (system.ConflictInterface != null)
                {
                    var entry = _settings.interfaceSelections.FirstOrDefault(s => s.interfaceTypeName == system.ConflictInterface.FullName);
                    if (entry != null) entry.enabled = newEnabled;
                }
                UpdateDependencyStatus();
                SaveSettings();
            }
            EditorGUI.EndDisabledGroup();

            // 名称
            if (system.ConflictInterface != null && system.ConflictImplementations != null)
            {
                var options = system.ConflictImplementations.Select(t => t.Name).ToArray();
                var currentIndex = system.ConflictImplementations.FindIndex(t => t == system.Type);

                EditorGUI.BeginDisabledGroup(system.IsModuleDisabled);
                var newIndex = EditorGUILayout.Popup(currentIndex, options, GUILayout.Width(_colName + SplitterWidth));
                if (newIndex != currentIndex && !system.IsModuleDisabled)
                {
                    var entry = _settings.interfaceSelections.First(s => s.interfaceTypeName == system.ConflictInterface.FullName);
                    entry.selectedImplementation = system.ConflictImplementations[newIndex].FullName;
                    SyncEnabledToSettings();
                    EditorUtility.SetDirty(_settings);
                    ScanSystems();
                    GUIUtility.ExitGUI();
                }
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                var nameStyle = new GUIStyle(EditorStyles.label);
                if (system.IsModuleDisabled)
                    nameStyle.normal.textColor = Color.gray;

                var displayName = string.IsNullOrEmpty(system.Alias)
                    ? system.DisplayName
                    : $"{system.DisplayName} ({system.Alias})";

                // 添加默认系统标记
                if (system.IsDefault)
                    displayName += " [Default]";

                GUILayout.Label(displayName, nameStyle, GUILayout.Width(_colName + SplitterWidth));
            }

            // 优先级
            GUILayout.Label(system.Priority.ToString(), GUILayout.Width(_colPriority + SplitterWidth));

            // 依赖
            var depStyle = new GUIStyle(EditorStyles.label);
            if (system.HasDisabledDependency)
                depStyle.normal.textColor = new Color(0.8f, 0.4f, 0f);
            if (system.IsModuleDisabled)
                depStyle.normal.textColor = Color.gray;

            var depText = system.Dependencies.Count > 0 ? string.Join(", ", system.Dependencies) : "-";
            GUILayout.Label(depText, depStyle, GUILayout.Width(_colDependencies));

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndHorizontal();
        }

        private void ScanSystems()
        {
            _scannedSystems.Clear();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.GetName().Name.StartsWith("System") &&
                            !a.GetName().Name.StartsWith("Microsoft") &&
                            !a.GetName().Name.StartsWith("Unity") &&
                            !a.GetName().Name.StartsWith("mscorlib"));

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => typeof(ISystem).IsAssignableFrom(t) &&
                                    !t.IsAbstract && !t.IsInterface &&
                                    t.GetCustomAttribute<AutoRegisterAttribute>() != null);

                    var assemblyName = assembly.GetName().Name;

                    foreach (var type in types)
                    {
                        var alias = type.GetCustomAttribute<SystemAliasAttribute>();
                        var priorityAttr = type.GetCustomAttribute<SystemPriorityAttribute>();
                        var dependsOnAttrs = type.GetCustomAttributes<DependsOnAttribute>();
                        var isDefault = type.GetCustomAttribute<DefaultAttribute>() != null;

                        var info = new ScannedSystemInfo
                        {
                            Type = type,
                            DisplayName = type.Name,
                            Alias = alias?.Alias,
                            Priority = priorityAttr?.Priority ?? 0,
                            Dependencies = dependsOnAttrs.Select(d => d.DependencyType.Name).ToList(),
                            IsDefault = isDefault
                        };

                        // 从配置中读取启用状态
                        var entry = _settings.systems.FirstOrDefault(s => s.typeName == type.FullName);
                        info.IsEnabled = entry?.enabled ?? true;

                        // 检查模块状态
                        var moduleRegistry = ModuleRegistrySettings.Instance;
                        if (moduleRegistry != null)
                        {
                            info.ModuleId = ExtractModuleId(assemblyName);
                            info.IsModuleDisabled = moduleRegistry.IsAssemblyDisabled(assemblyName);
                        }

                        _scannedSystems.Add(info);
                    }
                }
                catch
                {
                    // 跳过无法加载的程序集
                }
            }

            // 同步到配置（在收集接口实现前，确保所有系统都有配置）
            SyncToSettings();

            // 收集接口实现关系（会移除隐藏的冲突系统）
            CollectInterfaceImplementations();

            UpdateDependencyStatus();
        }

        private void CollectInterfaceImplementations()
        {
            // 收集接口实现关系
            var interfaceToTypes = new Dictionary<Type, List<Type>>();
            foreach (var system in _scannedSystems)
            {
                foreach (var iface in system.Type.GetInterfaces())
                {
                    if (iface != typeof(ISystem) && typeof(ISystem).IsAssignableFrom(iface))
                    {
                        if (!interfaceToTypes.TryGetValue(iface, out var list))
                        {
                            list = new List<Type>();
                            interfaceToTypes[iface] = list;
                        }
                        list.Add(system.Type);
                    }
                }
            }

            // 标记有冲突的系统，只保留选中的那个显示
            _hiddenConflictTypes.Clear();
            foreach (var kv in interfaceToTypes.Where(x => x.Value.Count > 1))
            {
                var iface = kv.Key;
                var implementations = kv.Value;

                // 获取或创建接口配置
                var interfaceEntry = _settings.interfaceSelections.FirstOrDefault(s => s.interfaceTypeName == iface.FullName);
                if (interfaceEntry == null)
                {
                    interfaceEntry = new InterfaceImplementationEntry { interfaceTypeName = iface.FullName };
                    _settings.interfaceSelections.Add(interfaceEntry);
                }

                // 获取当前选中的实现
                var selectedType = implementations.FirstOrDefault(t => t.FullName == interfaceEntry.selectedImplementation) ?? implementations[0];
                interfaceEntry.selectedImplementation = selectedType.FullName;

                // 标记选中的系统为冲突代表，使用接口的 enabled 状态
                var selectedSystem = _scannedSystems.First(s => s.Type == selectedType);
                selectedSystem.ConflictInterface = iface;
                selectedSystem.ConflictImplementations = implementations;
                selectedSystem.IsEnabled = interfaceEntry.enabled;

                // 隐藏其他实现
                foreach (var impl in implementations.Where(t => t != selectedType))
                    _hiddenConflictTypes.Add(impl.FullName);
            }

            // 移除被隐藏的系统（仅从显示列表移除）
            _scannedSystems.RemoveAll(s => _hiddenConflictTypes.Contains(s.Type.FullName));
        }

        private void SyncToSettings()
        {
            // 添加新扫描到的系统
            foreach (var system in _scannedSystems)
            {
                var existing = _settings.systems.FirstOrDefault(s => s.typeName == system.Type.FullName);
                if (existing == null)
                {
                    _settings.systems.Add(new SystemRegistryEntry
                    {
                        typeName = system.Type.FullName,
                        displayName = system.DisplayName,
                        enabled = true,
                        priority = system.Priority,
                        alias = system.Alias,
                        dependencies = system.Dependencies
                    });
                }
                else
                {
                    // 更新信息（保留 enabled 状态）
                    existing.displayName = system.DisplayName;
                    existing.priority = system.Priority;
                    existing.alias = system.Alias;
                    existing.dependencies = system.Dependencies;
                    // 从配置同步回扫描结果
                    system.IsEnabled = existing.enabled;
                }
            }

            // 移除不存在的系统（保留隐藏的冲突系统）
            var validTypes = _scannedSystems.Select(s => s.Type.FullName).ToHashSet();
            _settings.systems.RemoveAll(s => !validTypes.Contains(s.typeName) && !_hiddenConflictTypes.Contains(s.typeName));

            // 标记为脏以便保存
            EditorUtility.SetDirty(_settings);
        }

        private void UpdateDependencyStatus()
        {
            var disabledTypes = _scannedSystems
                .Where(s => !s.IsEnabled)
                .Select(s => s.Type.Name)
                .ToHashSet();

            foreach (var system in _scannedSystems)
            {
                system.HasDisabledDependency = system.Dependencies.Any(d => disabledTypes.Contains(d));
            }
        }

        private void SetAllEnabled(bool enabled)
        {
            foreach (var system in _scannedSystems)
            {
                system.IsEnabled = enabled;
            }
            UpdateDependencyStatus();
            SaveSettings();
        }

        private void SyncEnabledToSettings()
        {
            foreach (var system in _scannedSystems)
            {
                // 如果是冲突系统，同步接口状态到所有实现
                if (system.ConflictInterface != null)
                {
                    var interfaceEntry = _settings.interfaceSelections.FirstOrDefault(s => s.interfaceTypeName == system.ConflictInterface.FullName);
                    if (interfaceEntry != null)
                    {
                        foreach (var impl in system.ConflictImplementations)
                        {
                            var entry = _settings.systems.FirstOrDefault(s => s.typeName == impl.FullName);
                            if (entry != null) entry.enabled = interfaceEntry.enabled;
                        }
                    }
                }
                else
                {
                    var entry = _settings.systems.FirstOrDefault(s => s.typeName == system.Type.FullName);
                    if (entry != null) entry.enabled = system.IsEnabled;
                }
            }
        }

        private void SaveSettings()
        {
            SyncEnabledToSettings();
            _settings.ClearCache();
            EditorUtility.SetDirty(_settings);
            SystemRegistrySettings.NotifySettingsChanged();
        }

        /// <summary>
        /// 从程序集名称提取模块ID
        /// 例如: PuffinFramework.Config.Runtime -> Config
        /// </summary>
        private string ExtractModuleId(string assemblyName)
        {
            // 检查是否匹配模块程序集命名模式
            var moduleRegistry = ModuleRegistrySettings.Instance;
            if (moduleRegistry == null) return null;

            foreach (var module in moduleRegistry.modules)
            {
                if (assemblyName.Contains(module.moduleId))
                    return module.moduleId;
            }
            return null;
        }
    }
}
#endif
