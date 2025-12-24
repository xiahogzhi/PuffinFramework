#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Core
{
    /// <summary>
    /// Puffin 配置浏览窗口（类似 Project Settings）
    /// </summary>
    public class PuffinSettingsWindow : EditorWindow
    {
        private List<SettingsEntry> _settings = new();
        private int _selectedIndex = -1;
        private Vector2 _listScroll;
        private Vector2 _inspectorScroll;
        private UnityEditor.Editor _cachedEditor;
        private float _splitterPos;
        private bool _isDragging;
        private const string SplitterPosKey = "PuffinSettingsWindow_SplitterPos";
        private string _searchText = "";

        // 颜色
        private static readonly Color LeftPanelBg = new(0.2f, 0.2f, 0.2f);
        private static readonly Color RightPanelBg = new(0.22f, 0.22f, 0.22f);
        private static readonly Color SplitterColor = new(0.12f, 0.12f, 0.12f);
        private static readonly Color SelectedBg = new(0.17f, 0.36f, 0.53f);
        private static readonly Color HoverBg = new(0.3f, 0.3f, 0.3f);

        private class SettingsEntry
        {
            public string Name;
            public Type Type;
            public ScriptableObject Instance;
        }

        [MenuItem("Puffin Games/设置", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<PuffinSettingsWindow>("设置");
            window.minSize = new Vector2(700, 450);
        }

        /// <summary>
        /// 显示窗口并选中指定配置
        /// </summary>
        public static void ShowAndSelect<T>() where T : ScriptableObject
        {
            var window = GetWindow<PuffinSettingsWindow>("设置");
            window.minSize = new Vector2(700, 450);
            window.SelectByType(typeof(T));
        }

        private void SelectByType(Type type)
        {
            for (int i = 0; i < _settings.Count; i++)
            {
                if (_settings[i].Type == type)
                {
                    _selectedIndex = i;
                    UpdateCachedEditor();
                    Repaint();
                    return;
                }
            }
        }

        private void OnEnable()
        {
            _splitterPos = EditorPrefs.GetFloat(SplitterPosKey, 220f);
            CollectSettings();
            if (_settings.Count > 0 && _selectedIndex < 0)
            {
                _selectedIndex = 0;
                UpdateCachedEditor();
            }
        }

        private void OnDisable()
        {
            if (_cachedEditor != null)
                DestroyImmediate(_cachedEditor);
        }

        private void CollectSettings()
        {
            _settings.Clear();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsAbstract || type.IsGenericType) continue;

                        var settingAttr = type.GetCustomAttribute<PuffinSettingAttribute>();
                        if (settingAttr == null) continue;

                        var instance = GetSettingsInstance(type);
                        if (instance == null) continue;

                        var name = !string.IsNullOrEmpty(settingAttr.DisplayName)
                            ? settingAttr.DisplayName
                            : GetDisplayName(type.Name);

                        _settings.Add(new SettingsEntry
                        {
                            Name = name,
                            Type = type,
                            Instance = instance
                        });
                    }
                }
                catch { }
            }

            _settings = _settings.OrderBy(s => s.Name).ToList();
        }

        private ScriptableObject GetSettingsInstance(Type type)
        {
            try
            {
                var prop = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                return prop?.GetValue(null) as ScriptableObject;
            }
            catch { return null; }
        }

        private string GetDisplayName(string name)
        {
            if (name.EndsWith("Settings")) name = name[..^8];
            else if (name.EndsWith("Setting")) name = name[..^7];
            return System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        }

        private void OnGUI()
        {
            // 处理拖拽（优先处理）
            HandleSplitterDrag();

            // 绘制背景（使用完整窗口区域）
            EditorGUI.DrawRect(new Rect(0, 0, _splitterPos, position.height), LeftPanelBg);
            EditorGUI.DrawRect(new Rect(_splitterPos, 0, 1, position.height), SplitterColor);
            EditorGUI.DrawRect(new Rect(_splitterPos + 1, 0, position.width - _splitterPos - 1, position.height), RightPanelBg);

            // 左侧列表
            var leftRect = new Rect(0, 0, _splitterPos, position.height);
            GUILayout.BeginArea(leftRect);
            DrawLeftPanel();
            GUILayout.EndArea();

            // 右侧 Inspector
            var rightRect = new Rect(_splitterPos + 1, 0, position.width - _splitterPos - 1, position.height);
            GUILayout.BeginArea(rightRect);
            DrawRightPanel();
            GUILayout.EndArea();

            // 分隔条光标（拖拽区域比视觉宽度大）
            var splitterRect = new Rect(_splitterPos - 4, 0, 10, position.height);
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
        }

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical();

            // 工具栏
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(4);
            _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(EditorGUIUtility.IconContent("Refresh"), EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                CollectSettings();
                if (_selectedIndex >= _settings.Count) _selectedIndex = _settings.Count - 1;
                UpdateCachedEditor();
            }
            GUILayout.Space(2);
            EditorGUILayout.EndHorizontal();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUIStyle.none, GUI.skin.verticalScrollbar);

            for (int i = 0; i < _settings.Count; i++)
            {
                var entry = _settings[i];

                // 搜索过滤
                if (!string.IsNullOrEmpty(_searchText) &&
                    !entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                    continue;

                DrawSettingsItem(i, entry);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSettingsItem(int index, SettingsEntry entry)
        {
            var rect = EditorGUILayout.GetControlRect(false, 24);
            var isSelected = _selectedIndex == index;
            var isHover = rect.Contains(Event.current.mousePosition);

            // 背景
            if (isSelected)
                EditorGUI.DrawRect(rect, SelectedBg);
            else if (isHover)
                EditorGUI.DrawRect(rect, HoverBg);

            // 名称
            var labelRect = new Rect(rect.x + 12, rect.y, rect.width - 16, rect.height);
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = isSelected ? Color.white : Color.white * 0.9f }
            };
            EditorGUI.LabelField(labelRect, entry.Name, style);

            // 点击选择
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedIndex = index;
                UpdateCachedEditor();
                Event.current.Use();
                Repaint();
            }

            if (isHover)
                Repaint();
        }

        private void HandleSplitterDrag()
        {
            var splitterRect = new Rect(_splitterPos - 4, 0, 10, position.height);
            var e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(e.mousePosition))
                    {
                        _isDragging = true;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (_isDragging)
                    {
                        _splitterPos = Mathf.Clamp(e.mousePosition.x, 180, 350);
                        EditorPrefs.SetFloat(SplitterPosKey, _splitterPos);
                        Repaint();
                    }
                    break;
                case EventType.MouseUp:
                    if (_isDragging)
                    {
                        _isDragging = false;
                        e.Use();
                    }
                    break;
            }
        }

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical();

            if (_selectedIndex >= 0 && _selectedIndex < _settings.Count)
            {
                var entry = _settings[_selectedIndex];

                // 标题栏
                EditorGUILayout.BeginHorizontal(GUILayout.Height(28));
                GUILayout.Space(10);
                var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
                EditorGUILayout.LabelField(entry.Name, titleStyle, GUILayout.Height(24));
                GUILayout.FlexibleSpace();
                GUILayout.Space(10);
                EditorGUILayout.EndHorizontal();

                // 分隔线
                GUILayout.Space(2);
                EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), SplitterColor);
                GUILayout.Space(8);

                // Inspector
                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
                if (_cachedEditor != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    EditorGUILayout.BeginVertical();
                    _cachedEditor.OnInspectorGUI();
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(10);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            else
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Select a setting from the left panel", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndVertical();
        }

        private void UpdateCachedEditor()
        {
            if (_cachedEditor != null)
                DestroyImmediate(_cachedEditor);

            if (_selectedIndex >= 0 && _selectedIndex < _settings.Count)
            {
                var entry = _settings[_selectedIndex];
                if (entry.Instance != null)
                    _cachedEditor = UnityEditor.Editor.CreateEditor(entry.Instance);
            }
        }
    }
}
#endif
