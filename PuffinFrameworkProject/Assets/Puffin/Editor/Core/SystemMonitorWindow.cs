#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Puffin.Runtime.Core;
using UnityEditor;
using UnityEngine;
using L = Puffin.Editor.Localization.EditorLocalization;

namespace Puffin.Editor.Core
{
    public class SystemMonitorWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private string _searchFilter = "";
        private bool _showDisabled = true;
        private SortMode _sortMode = SortMode.Priority;
        private List<SystemStatus> _cachedStatus = new();
        private double _lastRefreshTime;
        private const double RefreshInterval = 0.1;

        // 列宽（可拖拽调整）
        private float _colStatus = 40f;
        private float _colName = 200f;
        private float _colPriority = 50f;
        private float _colLastMs = 70f;
        private float _colAvgMs = 70f;
        private float _colAction = 60f;
        private const float MinColWidth = 30f;
        private const float SplitterWidth = 4f;

        // 拖拽状态
        private int _draggingCol = -1;
        private float _dragStartX;
        private float _dragStartWidth;

        // 分组折叠状态
        private Dictionary<string, bool> _groupFoldouts = new();
        private const string CoreGroupName = "Core";

        private enum SortMode { Priority, Name, UpdateTime }

        [MenuItem("Puffin/System Monitor")]
        public static void ShowWindow()
        {
            GetWindow<SystemMonitorWindow>("System Monitor");
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
 
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            if (Runtime.Core.PuffinFramework.EffectiveRuntime == null)
            {
                EditorGUILayout.HelpBox(L.L("monitor.not_initialized"), MessageType.Info);
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(L.L("monitor.editor_mode"), MessageType.Info);
            }

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

            GUILayout.Label(L.L("common.sort") + ":", GUILayout.Width(35));
            _sortMode = (SortMode)EditorGUILayout.EnumPopup(_sortMode, EditorStyles.toolbarPopup, GUILayout.Width(80));

            GUILayout.Space(10);

            _showDisabled = GUILayout.Toggle(_showDisabled, L.L("monitor.show_disabled"), EditorStyles.toolbarButton, GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            var profiling = Runtime.Core.PuffinFramework.EffectiveRuntime.EnableProfiling;
            var newProfiling = GUILayout.Toggle(profiling, L.L("monitor.profiling"), EditorStyles.toolbarButton, GUILayout.Width(70));
            if (newProfiling != profiling)
                Runtime.Core.PuffinFramework.EffectiveRuntime.EnableProfiling = newProfiling;

            var pauseText = Runtime.Core.PuffinFramework.EffectiveRuntime.IsPaused ? "▶ " + L.L("monitor.resume") : "⏸ " + L.L("monitor.pause");
            if (GUILayout.Button(pauseText, EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                if (Runtime.Core.PuffinFramework.EffectiveRuntime.IsPaused)
                    Runtime.Core.PuffinFramework.Resume();
                else
                    Runtime.Core.PuffinFramework.Pause();
            }

            EditorGUILayout.EndHorizontal();
        }


        private void DrawSystemList()
        {
            if (EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                _cachedStatus = Runtime.Core.PuffinFramework.EffectiveRuntime.GetAllSystemStatus();
                _lastRefreshTime = EditorApplication.timeSinceStartup;
            }

            var filtered = _cachedStatus
                .Where(s => string.IsNullOrEmpty(_searchFilter) || s.Name.ToLower().Contains(_searchFilter.ToLower()))
                .Where(s => _showDisabled || s.IsEnabled);

            filtered = _sortMode switch
            {
                SortMode.Name => filtered.OrderBy(s => s.Name),
                SortMode.UpdateTime => filtered.OrderByDescending(s => s.AverageUpdateMs),
                _ => filtered.OrderBy(s => s.Priority)
            };

            var list = filtered.ToList();

            // 统计信息
            var totalMs = list.Where(s => s.IsEnabled).Sum(s => s.AverageUpdateMs);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{L.L("monitor.system_count")}: {list.Count}", GUILayout.Width(80));
            EditorGUILayout.LabelField($"{L.L("common.enabled")}: {list.Count(s => s.IsEnabled)}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"{L.L("monitor.total_time")}: {totalMs:F2}ms", GUILayout.Width(110));
            EditorGUILayout.EndHorizontal();

            // 列表
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // 按模块分组
            var groups = list
                .GroupBy(s => string.IsNullOrEmpty(s.ModuleId) ? CoreGroupName : s.ModuleId)
                .OrderBy(g => g.Key == CoreGroupName ? 0 : 1)
                .ThenBy(g => g.Key);

            foreach (var group in groups)
            {
                DrawModuleGroup(group.Key, group.ToList());
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawModuleGroup(string groupName, List<SystemStatus> systems)
        {
            if (!_groupFoldouts.ContainsKey(groupName))
                _groupFoldouts[groupName] = true;

            var enabledCount = systems.Count(s => s.IsEnabled);
            var totalMs = systems.Where(s => s.IsEnabled).Sum(s => s.AverageUpdateMs);

            // 分组头
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _groupFoldouts[groupName] = EditorGUILayout.Foldout(_groupFoldouts[groupName],
                $"{groupName} ({enabledCount}/{systems.Count}) - {totalMs:F2}ms", true);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!_groupFoldouts[groupName]) return;

            // 表头
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            DrawHeaderCell(L.L("monitor.status"), _colStatus, 0);
            DrawHeaderCell(L.L("monitor.system_name"), _colName, 1);
            DrawHeaderCell(L.L("monitor.priority"), _colPriority, 2);
            DrawHeaderCell(L.L("monitor.last_ms"), _colLastMs, 3);
            DrawHeaderCell(L.L("monitor.avg_ms"), _colAvgMs, 4);
            GUILayout.Label(L.L("common.operation"), GUILayout.Width(_colAction));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 系统列表
            foreach (var status in systems)
                DrawSystemRow(status);
        }

        private void DrawHeaderCell(string label, float width, int colIndex)
        {
            GUILayout.Label(label, GUILayout.Width(width));

            // 绘制分隔条（可拖拽）
            if (colIndex >= 0)
            {
                var splitterRect = GUILayoutUtility.GetRect(SplitterWidth, 18f, GUILayout.Width(SplitterWidth));

                // 绘制分隔线
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
                0 => _colStatus,
                1 => _colName,
                2 => _colPriority,
                3 => _colLastMs,
                4 => _colAvgMs,
                _ => 0
            };
        }

        private void SetColumnWidth(int colIndex, float width)
        {
            width = Mathf.Max(MinColWidth, width);
            switch (colIndex)
            {
                case 0: _colStatus = width; break;
                case 1: _colName = width; break;
                case 2: _colPriority = width; break;
                case 3: _colLastMs = width; break;
                case 4: _colAvgMs = width; break;
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

        private void DrawSystemRow(SystemStatus status)
        {
            var bgColor = status.IsEnabled ? Color.white : new Color(0.7f, 0.7f, 0.7f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16); // 缩进

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            // 状态图标
            var icon = status.IsEnabled ? "✓" : "✗";
            var iconColor = status.IsEnabled ? Color.green : Color.red;
            var style = new GUIStyle(EditorStyles.label) { normal = { textColor = iconColor } };
            GUILayout.Label(icon, style, GUILayout.Width(_colStatus + SplitterWidth));

            // 名称（有别名时显示别名）
            var displayName = string.IsNullOrEmpty(status.Alias) ? status.Name : $"{status.Name} ({status.Alias})";
            GUILayout.Label(displayName, GUILayout.Width(_colName + SplitterWidth));

            // 优先级
            GUILayout.Label(status.Priority.ToString(), GUILayout.Width(_colPriority + SplitterWidth));

            // 性能数据
            var lastMs = status.LastUpdateMs;
            var avgMs = status.AverageUpdateMs;
            var timeStyle = new GUIStyle(EditorStyles.label);
            if (avgMs > 1) timeStyle.normal.textColor = Color.yellow;
            if (avgMs > 5) timeStyle.normal.textColor = Color.red;

            GUILayout.Label(lastMs > 0 ? $"{lastMs:F3}" : "-", timeStyle, GUILayout.Width(_colLastMs + SplitterWidth));
            GUILayout.Label(avgMs > 0 ? $"{avgMs:F3}" : "-", timeStyle, GUILayout.Width(_colAvgMs + SplitterWidth));

            // 启用/禁用按钮
            if (status.CanToggle)
            {
                var btnText = status.IsEnabled ? L.L("common.disable") : L.L("common.enable");
                if (GUILayout.Button(btnText, GUILayout.Width(_colAction)))
                {
                    Runtime.Core.PuffinFramework.EffectiveRuntime.SetSystemEnabled(status.Type, !status.IsEnabled);
                }
            }
            else
            {
                GUILayout.Label("-", GUILayout.Width(_colAction));
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
