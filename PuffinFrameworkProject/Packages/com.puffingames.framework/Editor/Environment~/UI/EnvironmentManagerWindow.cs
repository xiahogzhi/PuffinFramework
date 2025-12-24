#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puffin.Editor.Environment.Core;
using Puffin.Editor.Hub.Data;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Environment.UI
{
    public class EnvironmentManagerWindow : EditorWindow
    {
        private static string ModulesDir => Path.Combine(Application.dataPath, "Puffin/Modules");

        private Dictionary<string, List<EnvDepInfo>> _moduleEnvDeps;
        private Dictionary<string, bool> _moduleFoldout;
        private Dictionary<string, bool> _installedStatus;
        private Dictionary<string, List<EnvDepInfo>> _envConflicts; // 配置冲突检测
        private DependencyManager _manager;
        private Vector2 _scrollPos;
        private string _filter;
        private int _animFrame;
        private double _lastAnimTime;

        private class EnvDepInfo
        {
            public DependencyDefinition Definition;
            public string ModuleId;
            public EnvironmentDependency Original; // 原始配置，用于冲突比较
        }

        [MenuItem("Puffin/Environment Manager")]
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
            if (_moduleEnvDeps == null) return false;
            foreach (var kvp in _moduleEnvDeps)
                foreach (var info in kvp.Value)
                    if (DownloadService.IsDownloading(info.Definition.id))
                        return true;
            return false;
        }

        private void ScanModules()
        {
            _moduleEnvDeps = new Dictionary<string, List<EnvDepInfo>>();
            _envConflicts = new Dictionary<string, List<EnvDepInfo>>();
            _installedStatus.Clear();

            if (!Directory.Exists(ModulesDir)) return;

            // 收集所有环境依赖
            var allEnvDeps = new Dictionary<string, List<EnvDepInfo>>();

            foreach (var moduleDir in Directory.GetDirectories(ModulesDir))
            {
                var moduleId = Path.GetFileName(moduleDir);
                var manifestPath = Path.Combine(moduleDir, "module.json");
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var manifest = JsonUtility.FromJson<HubModuleManifest>(json);
                    if (manifest?.envDependencies == null || manifest.envDependencies.Length == 0) continue;

                    var deps = new List<EnvDepInfo>();
                    foreach (var envDep in manifest.envDependencies)
                    {
                        var info = new EnvDepInfo
                        {
                            Definition = ConvertToDepDefinition(envDep),
                            ModuleId = moduleId,
                            Original = envDep
                        };
                        deps.Add(info);

                        // 收集用于冲突检测
                        if (!allEnvDeps.ContainsKey(envDep.id))
                            allEnvDeps[envDep.id] = new List<EnvDepInfo>();
                        allEnvDeps[envDep.id].Add(info);
                    }

                    _moduleEnvDeps[moduleId] = deps;
                    if (!_moduleFoldout.ContainsKey(moduleId)) _moduleFoldout[moduleId] = true;
                }
                catch { }
            }

            // 检测配置冲突
            foreach (var kvp in allEnvDeps)
            {
                if (kvp.Value.Count <= 1) continue;
                var first = kvp.Value[0].Original;
                foreach (var info in kvp.Value.Skip(1))
                {
                    if (HasConfigConflict(first, info.Original))
                    {
                        _envConflicts[kvp.Key] = kvp.Value;
                        break;
                    }
                }
            }

            RefreshInstalledStatus();
        }

        private bool HasConfigConflict(EnvironmentDependency a, EnvironmentDependency b)
        {
            // 检查关键配置是否不同
            if (a.source != b.source) return true;
            if (a.type != b.type) return true;
            if (!string.IsNullOrEmpty(a.version) && !string.IsNullOrEmpty(b.version) && a.version != b.version) return true;
            if (!string.IsNullOrEmpty(a.url) && !string.IsNullOrEmpty(b.url) && a.url != b.url) return true;
            return false;
        }

        private DependencyDefinition ConvertToDepDefinition(EnvironmentDependency envDep)
        {
            return new DependencyDefinition
            {
                id = envDep.id,
                displayName = envDep.id,
                source = (DependencySource)envDep.source,
                type = (DependencyType)envDep.type,
                url = envDep.url,
                version = envDep.version,
                installDir = envDep.installDir,
                extractPath = envDep.extractPath,
                requiredFiles = envDep.requiredFiles,
                targetFrameworks = envDep.targetFrameworks,
                requirement = envDep.optional ? DependencyRequirement.Optional : DependencyRequirement.Required
            };
        }

        private void RefreshInstalledStatus()
        {
            foreach (var kvp in _moduleEnvDeps)
                foreach (var info in kvp.Value)
                    _installedStatus[info.Definition.id] = _manager.IsInstalled(info.Definition);
        }

        /// <summary>
        /// 获取依赖指定环境的所有模块
        /// </summary>
        public List<string> GetModulesRequiringEnv(string envId)
        {
            var result = new List<string>();
            foreach (var kvp in _moduleEnvDeps)
            {
                if (kvp.Value.Any(info => info.Definition.id == envId))
                    result.Add(kvp.Key);
            }
            return result;
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

            if (_moduleEnvDeps == null || _moduleEnvDeps.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到任何模块环境依赖配置\n\n环境依赖现在在模块的 module.json 中配置", MessageType.Info);
                return;
            }

            // 显示配置冲突警告
            if (_envConflicts != null && _envConflicts.Count > 0)
            {
                foreach (var kvp in _envConflicts)
                {
                    var modules = string.Join(", ", kvp.Value.Select(v => v.ModuleId));
                    var configs = string.Join("\n", kvp.Value.Select(v =>
                    {
                        var o = v.Original;
                        var src = new[] { "NuGet", "GitHub", "URL", "Release", "UPM" }[o.source];
                        return $"  • {v.ModuleId}: [{src}] v{o.version}";
                    }));
                    EditorGUILayout.HelpBox($"⚠ 环境依赖 \"{kvp.Key}\" 配置冲突:\n{configs}", MessageType.Warning);
                }
                EditorGUILayout.Space(5);
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var kvp in _moduleEnvDeps)
            {
                if (!string.IsNullOrEmpty(_filter) && !kvp.Key.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    continue;
                DrawModuleSection(kvp.Key, kvp.Value);
            }
            EditorGUILayout.Space(5);

            EditorGUILayout.EndScrollView();
        }

        private void DrawModuleSection(string moduleId, List<EnvDepInfo> deps)
        {
            var foldout = _moduleFoldout.TryGetValue(moduleId, out var f) && f;
            var installedCount = deps.Count(d => _installedStatus.TryGetValue(d.Definition.id, out var v) && v);
            var totalCount = deps.Count;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldout = EditorGUILayout.Foldout(foldout, $"{moduleId} ({installedCount}/{totalCount})", true);
            _moduleFoldout[moduleId] = foldout;

            if (foldout)
            {
                foreach (var info in deps)
                    DrawDepItem(info.Definition);
            }
            EditorGUILayout.EndVertical();
        } 

        private void DrawDepItem(DependencyDefinition dep)
        {
            var installed = _installedStatus.TryGetValue(dep.id, out var v) && v;
            var task = DownloadService.GetTask(dep.id);
            var hasCache = !installed && DownloadService.HasCache(dep);
            var isRequired = dep.requirement == DependencyRequirement.Required;
            var reqLabel = isRequired ? "[必须]" : "[可选]";

            EditorGUILayout.BeginHorizontal();

            // 状态图标
            string icon;
            Color color;
            if (installed)
            {
                icon = "✓";
                color = new Color(0.2f, 0.9f, 0.3f);
            }
            else if (task != null)
            {
                switch (task.State)
                {
                    case TaskState.Downloading:
                        var anim = new[] { "●", "◐", "◑", "◒" };
                        icon = anim[_animFrame % 4];
                        color = new Color(1f, 0.7f, 0.2f);
                        break;
                    case TaskState.Downloaded:
                        icon = "◉";
                        color = new Color(0.4f, 0.7f, 1f);
                        break;
                    case TaskState.Installing:
                        var installAnim = new[] { "◐", "◑", "◒", "◓" };
                        icon = installAnim[_animFrame % 4];
                        color = new Color(0.5f, 0.9f, 0.5f);
                        break;
                    case TaskState.Failed:
                        icon = "✗";
                        color = new Color(1f, 0.3f, 0.3f);
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
                GUI.backgroundColor = new Color(1f, 0.7f, 0.2f);
                if (GUILayout.Button("取消", GUILayout.Width(40)))
                    DownloadService.CancelDownload(dep.id);
            }
            else if (installed)
            {
                // 检查是否有模块依赖此环境（仅必须依赖不可卸载）
                var dependentModules = GetModulesRequiringEnv(dep.id);
                var moduleExists = dependentModules.Any(m => Directory.Exists(Path.Combine(ModulesDir, m)));
                var canUninstall = !isRequired || !moduleExists;

                if (!canUninstall)
                {
                    GUI.backgroundColor = Color.gray;
                    GUI.enabled = false;
                    GUILayout.Button("必需", GUILayout.Width(40));
                    GUI.enabled = true;
                }
                else
                {
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
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
            }
            else if (task == null || task.State is TaskState.Downloaded or TaskState.Failed or TaskState.Cancelled or TaskState.Completed)
            {
                if (hasCache)
                {
                    GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
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
                    GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
                    if (GUILayout.Button("下载", GUILayout.Width(40)))
                        DownloadService.StartDownload(dep);
                }
            }
            GUI.backgroundColor = oldBg;

            EditorGUILayout.EndHorizontal();

            // 进度条
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
