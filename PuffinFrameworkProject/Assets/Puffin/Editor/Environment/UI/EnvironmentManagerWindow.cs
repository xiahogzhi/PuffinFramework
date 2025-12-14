#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puffin.Editor.Environment.Core;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Environment.UI
{
    public class EnvironmentManagerWindow : EditorWindow
    {
        private static string ModulesDir => Path.Combine(Application.dataPath, "Puffin/Modules");
        private static string CoreDepsPath => Path.Combine(Application.dataPath, "Puffin/Editor/Environment/dependencies.json");

        private Dictionary<string, DependencyConfig> _moduleConfigs;
        private Dictionary<string, bool> _moduleFoldout;
        private Dictionary<string, bool> _installedStatus;
        private DependencyManager _manager;
        private Vector2 _scrollPos;
        private string _filter;
        private int _animFrame;
        private double _lastAnimTime;

        [MenuItem("Puffin Framework/Environment Manager")]
        public static void ShowWindow() => GetWindow<EnvironmentManagerWindow>("环境管理器");

        private void OnEnable()
        {
            _manager = new DependencyManager();
            _moduleFoldout = new Dictionary<string, bool>();
            _installedStatus = new Dictionary<string, bool>();
            ScanModules();
            DownloadService.OnTasksChanged += OnTasksChanged;
            EditorApplication.update += OnUpdate;
        }

        private void OnDisable()
        {
            DownloadService.OnTasksChanged -= OnTasksChanged;
            EditorApplication.update -= OnUpdate;
        }

        private void OnFocus()
        {
            if (_manager == null) return;
            ScanModules();
        }

        private void OnTasksChanged()
        {
            RefreshInstalledStatus();
            Repaint();
        }

        private void OnUpdate()
        {
            if (HasActiveDownloads() && EditorApplication.timeSinceStartup - _lastAnimTime > 0.2)
            {
                _animFrame++;
                _lastAnimTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private bool HasActiveDownloads()
        {
            foreach (var kvp in _moduleConfigs)
                foreach (var dep in kvp.Value.dependencies)
                    if (DownloadService.IsDownloading(dep.id))
                        return true;
            return false;
        }

        private void ScanModules()
        {
            _moduleConfigs = new Dictionary<string, DependencyConfig>();
            _installedStatus.Clear();

            if (File.Exists(CoreDepsPath))
            {
                var coreConfig = DependencyManager.LoadConfig(CoreDepsPath);
                if (coreConfig?.dependencies != null)
                {
                    _moduleConfigs["[Core]"] = coreConfig;
                    if (!_moduleFoldout.ContainsKey("[Core]")) _moduleFoldout["[Core]"] = true;
                }
            }

            if (Directory.Exists(ModulesDir))
            {
                foreach (var moduleDir in Directory.GetDirectories(ModulesDir))
                {
                    var moduleName = Path.GetFileName(moduleDir);
                    var files = Directory.GetFiles(moduleDir, "dependencies.json", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        var config = DependencyManager.LoadConfig(files[0]);
                        if (config?.dependencies != null)
                        {
                            _moduleConfigs[moduleName] = config;
                            if (!_moduleFoldout.ContainsKey(moduleName)) _moduleFoldout[moduleName] = true;
                        }
                    }
                }
            }

            RefreshInstalledStatus();
        }

        private void RefreshInstalledStatus()
        {
            foreach (var kvp in _moduleConfigs)
                foreach (var dep in kvp.Value.dependencies)
                    _installedStatus[dep.id] = _manager.IsInstalled(dep);
        }

        private void OnGUI()
        {
            // 工具栏
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(40)))
                ScanModules();
            if (GUILayout.Button("清理缓存", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                DependencyManager.ClearCache();
                ShowNotification(new GUIContent("缓存已清理"));
            }
            GUILayout.FlexibleSpace();
            _filter = EditorGUILayout.TextField(_filter ?? "", EditorStyles.toolbarSearchField, GUILayout.Width(150));
            if (GUILayout.Button("清除", EditorStyles.toolbarButton, GUILayout.Width(40)))
                _filter = null;
            EditorGUILayout.EndHorizontal();

            if (_moduleConfigs == null || _moduleConfigs.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到任何模块依赖配置", MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            var moduleIndex = 0;
            foreach (var kvp in _moduleConfigs)
            {
                if (!string.IsNullOrEmpty(_filter) && !kvp.Key.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    continue;
                DrawModuleSection(kvp.Key, kvp.Value, moduleIndex++);
            }
            EditorGUILayout.Space(5);

            EditorGUILayout.EndScrollView();
        }

        private void DrawModuleSection(string moduleName, DependencyConfig config, int moduleIndex)
        {
            var foldout = _moduleFoldout.TryGetValue(moduleName, out var f) && f;
            var installedCount = config.dependencies.Count(d => _installedStatus.TryGetValue(d.id, out var v) && v);
            var totalCount = config.dependencies.Count;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldout = EditorGUILayout.Foldout(foldout, $"{moduleName} ({installedCount}/{totalCount})", true);
            _moduleFoldout[moduleName] = foldout;

            if (foldout)
            {
                // 必须依赖
                var required = config.dependencies.Where(d => d.requirement == DependencyRequirement.Required).ToList();
                var optional = config.dependencies.Where(d => d.requirement != DependencyRequirement.Required).ToList();

                var rowIndex = 0;
                foreach (var dep in required)
                    DrawDepItem(dep, rowIndex++);

                // 分割线（如果两边都有内容）
                if (required.Count > 0 && optional.Count > 0)
                {
                    EditorGUILayout.Space(2);
                    var lineRect = EditorGUILayout.GetControlRect(false, 1);
                    EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                    EditorGUILayout.Space(2);
                }

                // 可选依赖
                foreach (var dep in optional)
                    DrawDepItem(dep, rowIndex++);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawDepItem(DependencyDefinition dep, int rowIndex = 0)
        {
            var installed = _installedStatus.TryGetValue(dep.id, out var v) && v;
            var task = DownloadService.GetTask(dep.id);
            var hasCache = !installed && DownloadService.HasCache(dep);
            var reqLabel = dep.requirement == DependencyRequirement.Required ? "[必须]" : "[可选]";

            EditorGUILayout.BeginHorizontal();

            // 状态图标
            string icon;
            Color color;
            if (installed)
            {
                icon = "✓";
                color = new Color(0.2f, 0.9f, 0.3f); // 绿色
            }
            else if (task != null)
            {
                switch (task.State)
                {
                    case TaskState.Downloading:
                        var anim = new[] { "●", "◐", "◑", "◒" };
                        icon = anim[_animFrame % 4];
                        color = new Color(1f, 0.7f, 0.2f); // 橙色
                        break;
                    case TaskState.Downloaded:
                        icon = "◉";
                        color = new Color(0.4f, 0.7f, 1f); // 蓝色
                        break;
                    case TaskState.Installing:
                        var installAnim = new[] { "◐", "◑", "◒", "◓" };
                        icon = installAnim[_animFrame % 4];
                        color = new Color(0.5f, 0.9f, 0.5f); // 浅绿
                        break;
                    case TaskState.Failed:
                        icon = "✗";
                        color = new Color(1f, 0.3f, 0.3f); // 红色
                        break;
                    default:
                        icon = hasCache ? "◉" : "○";
                        color = hasCache ? new Color(0.4f, 0.7f, 1f) : Color.gray;
                        break;
                }
            }
            else
            {
                icon = hasCache ? "◉" : "○";
                color = hasCache ? new Color(0.4f, 0.7f, 1f) : Color.gray;
            }

            var iconStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = color }, fontStyle = FontStyle.Bold };
            GUILayout.Label(icon, iconStyle, GUILayout.Width(20));

            GUILayout.Label($"{dep.displayName ?? dep.id} {reqLabel}", GUILayout.MinWidth(150));
            GUILayout.FlexibleSpace();

            // 状态文字
            if (task?.State == TaskState.Installing)
            {
                var stateStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
                GUILayout.Label("[安装中]", stateStyle, GUILayout.Width(60));
            }

            // 按钮
            var oldBg = GUI.backgroundColor;
            if (task != null && task.IsRunning)
            {
                GUI.backgroundColor = new Color(1f, 0.7f, 0.2f); // 橙色
                if (GUILayout.Button("取消", GUILayout.Width(40)))
                    DownloadService.CancelDownload(dep.id);
            }
            else if (installed)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); // 浅红
                if (GUILayout.Button("卸载", GUILayout.Width(40)))
                {
                    if (EditorUtility.DisplayDialog("确认", $"确定要卸载 {dep.displayName ?? dep.id} 吗？", "确定", "取消"))
                    {
                        _manager.Uninstall(dep);
                        AssetDatabase.Refresh();
                        RefreshInstalledStatus();
                    }
                }
            }
            else if (task == null || task.State is TaskState.Downloaded or TaskState.Failed or TaskState.Cancelled or TaskState.Completed)
            {
                if (hasCache)
                {
                    GUI.backgroundColor = new Color(0.5f, 1f, 0.5f); // 浅绿
                    if (GUILayout.Button("安装", GUILayout.Width(40)))
                        DownloadService.StartInstallFromCache(dep);
                    GUI.backgroundColor = oldBg;
                    if (GUILayout.Button("▼", GUILayout.Width(20)))
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("重新下载"), false, () => {
                            DownloadService.DeleteCache(dep);
                            DownloadService.StartDownload(dep);
                        });
                        menu.AddItem(new GUIContent("删除缓存"), false, () => {
                            DownloadService.DeleteCache(dep);
                            Repaint();
                        });
                        menu.ShowAsContext();
                    }
                }
                else
                {
                    GUI.backgroundColor = new Color(0.5f, 0.8f, 1f); // 浅蓝
                    if (GUILayout.Button("下载", GUILayout.Width(40)))
                        DownloadService.StartDownload(dep);
                }
            }
            GUI.backgroundColor = oldBg;

            EditorGUILayout.EndHorizontal();

            // 进度条在条目下方（仅下载中显示）
            if (task?.State == TaskState.Downloading)
            {
                var progRect = EditorGUILayout.GetControlRect(false, 16);
                progRect.x += 20;
                progRect.width -= 20;
                string sizeText;
                if (task.Downloaded > 0)
                {
                    var dlStr = FormatSize(task.Downloaded);
                    var speedStr = task.Speed > 0 ? $" - {FormatSize(task.Speed)}/s" : "";
                    sizeText = task.Total > 0
                        ? $"{dlStr} / {FormatSize(task.Total)}{speedStr}"
                        : $"{dlStr}{speedStr}";
                }
                else
                    sizeText = "正在连接...";
                EditorGUI.ProgressBar(progRect, task.Progress, sizeText);
            }

            // 错误信息
            if (task?.State == TaskState.Failed)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                var errorStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red }, fontSize = 10 };
                GUILayout.Label(task.Error, errorStyle);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1048576) return $"{bytes / 1048576f:F2} MB";
            if (bytes >= 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }
    }
}
#endif
