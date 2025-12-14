#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Hub.Data;
using Puffin.Editor.Hub.Services;
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.UI
{
    /// <summary>
    /// 模块 Hub 主窗口
    /// </summary>
    public class ModuleHubWindow : EditorWindow
    {
        private RegistryService _registryService;
        private ModuleResolver _resolver;
        private ModuleInstaller _installer;

        private List<HubModuleInfo> _installedModules = new();
        private Dictionary<string, List<HubModuleInfo>> _registryModules = new();
        private List<HubModuleInfo> _filteredModules = new();
        private HubModuleInfo _selectedModule;
        private int _selectedVersionIndex;
        private string _selectedVersion;

        private string _searchKeyword = "";
        private int _filterIndex;
        private readonly string[] _filterOptions = { "全部", "可更新", "未安装" };
        private string _selectedRegistryId; // null = 全部, "installed" = 已安装

        private const string PrefKeySelectedRegistry = "PuffinHub_SelectedRegistry";

        private Vector2 _registryScroll;
        private Vector2 _moduleListScroll;
        private Vector2 _detailScroll;

        private bool _isLoading;
        private string _statusMessage = "";
        private float _progress;
        private long _downloadedBytes;
        private long _totalBytes;
        private long _downloadSpeed;

        private const float LeftPanelWidth = 220f;
        private const float RightPanelWidth = 280f;

        [MenuItem("Puffin Framework/Module Manager", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<ModuleHubWindow>("Module Manager");
            window.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
            _registryService = new RegistryService();
            _resolver = new ModuleResolver(_registryService);
            _installer = new ModuleInstaller(_registryService, _resolver);

            _installer.OnProgress += (id, p) => { _progress = p; Repaint(); };
            _installer.OnStatusChanged += s => { _statusMessage = s; Repaint(); };
            _installer.OnDownloadProgress += (p, dl, total, speed) => { _progress = p; _downloadedBytes = dl; _totalBytes = total; _downloadSpeed = speed; Repaint(); };

            // 恢复选择的仓库源
            var saved = EditorPrefs.GetString(PrefKeySelectedRegistry, "");
            _selectedRegistryId = string.IsNullOrEmpty(saved) ? null : saved;

            RefreshModulesAsync().Forget();
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            {
                DrawRegistryPanel();
                DrawModuleListPanel();
                DrawDetailPanel();
            }
            EditorGUILayout.EndHorizontal();

            DrawStatusBar();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    RefreshModulesAsync(true).Forget();

                GUILayout.Space(10);
                GUILayout.Label("搜索:", GUILayout.Width(35));
                var newSearch = EditorGUILayout.TextField(_searchKeyword, EditorStyles.toolbarSearchField, GUILayout.Width(150));
                if (newSearch != _searchKeyword)
                {
                    _searchKeyword = newSearch;
                    ApplyFilter();
                }

                GUILayout.Space(10);
                GUILayout.Label("筛选:", GUILayout.Width(35));
                var newFilter = EditorGUILayout.Popup(_filterIndex, _filterOptions, EditorStyles.toolbarPopup, GUILayout.Width(80));
                if (newFilter != _filterIndex)
                {
                    _filterIndex = newFilter;
                    ApplyFilter();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("添加仓库", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    AddRegistryWindow.Show(r => { HubSettings.Instance.registries.Add(r); EditorUtility.SetDirty(HubSettings.Instance); RefreshModulesAsync().Forget(); });

                if (GUILayout.Button("创建模块", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    CreateModuleWindow.Show(() => RefreshModulesAsync().Forget(), GetAllAvailableModules());

                // 只有存在有 token 的仓库时才显示发布按钮
                if (HubSettings.Instance.HasAnyToken() && GUILayout.Button("发布", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    PublishModuleWindow.Show();

                if (GUILayout.Button("设置", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    Selection.activeObject = HubSettings.Instance;
                    EditorGUIUtility.PingObject(HubSettings.Instance);
                    EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRegistryPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth));
            {
                EditorGUILayout.LabelField("仓库源", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                _registryScroll = EditorGUILayout.BeginScrollView(_registryScroll, GUI.skin.box);
                {
                    // 全部选项
                    var allSelected = _selectedRegistryId == null;
                    var allRect = EditorGUILayout.BeginHorizontal();
                    {
                        if (allSelected && Event.current.type == EventType.Repaint)
                            EditorGUI.DrawRect(allRect, new Color(0.24f, 0.49f, 0.91f, 0.3f));
                        GUILayout.Space(24);
                        if (GUILayout.Button("全部", EditorStyles.label) && !allSelected)
                        {
                            _selectedRegistryId = null;
                            EditorPrefs.SetString(PrefKeySelectedRegistry, "");
                            ApplyFilter();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    // 已安装选项
                    var installedSelected = _selectedRegistryId == "installed";
                    var installedRect = EditorGUILayout.BeginHorizontal();
                    {
                        if (installedSelected && Event.current.type == EventType.Repaint)
                            EditorGUI.DrawRect(installedRect, new Color(0.24f, 0.49f, 0.91f, 0.3f));
                        GUILayout.Space(24);
                        if (GUILayout.Button($"已安装 ({_installedModules.Count})", EditorStyles.label) && !installedSelected)
                        {
                            _selectedRegistryId = "installed";
                            EditorPrefs.SetString(PrefKeySelectedRegistry, "installed");
                            ApplyFilter();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(5);

                    RegistrySource toRemove = null;
                    RegistrySource toEdit = null;
                    foreach (var registry in HubSettings.Instance.registries)
                    {
                        var isSelected = _selectedRegistryId == registry.id;
                        var rect = EditorGUILayout.BeginHorizontal();
                        {
                            if (isSelected && Event.current.type == EventType.Repaint)
                                EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.91f, 0.3f));

                            var newEnabled = EditorGUILayout.Toggle(registry.enabled, GUILayout.Width(20));
                            if (newEnabled != registry.enabled)
                            {
                                registry.enabled = newEnabled;
                                EditorUtility.SetDirty(HubSettings.Instance);
                                RefreshModulesAsync().Forget();
                            }
                            if (GUILayout.Button(registry.name, EditorStyles.label, GUILayout.MaxWidth(120)))
                            {
                                _selectedRegistryId = isSelected ? null : registry.id;
                                EditorPrefs.SetString(PrefKeySelectedRegistry, _selectedRegistryId ?? "");
                                ApplyFilter();
                            }
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("✎", GUILayout.Width(20), GUILayout.Height(18)))
                                toEdit = registry;
                            if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(18)))
                                toRemove = registry;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    if (toRemove != null && EditorUtility.DisplayDialog("删除仓库", $"确定删除 {toRemove.name}？", "删除", "取消"))
                    {
                        HubSettings.Instance.registries.Remove(toRemove);
                        EditorUtility.SetDirty(HubSettings.Instance);
                        if (_selectedRegistryId == toRemove.id) _selectedRegistryId = null;
                        RefreshModulesAsync().Forget();
                    }
                    if (toEdit != null)
                        EditRegistryWindow.Show(toEdit, () => { EditorUtility.SetDirty(HubSettings.Instance); RefreshModulesAsync().Forget(); });
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawModuleListPanel()
        {
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField($"模块 ({_filteredModules.Count})", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                _moduleListScroll = EditorGUILayout.BeginScrollView(_moduleListScroll, GUI.skin.box);
                {
                    if (_isLoading)
                    {
                        EditorGUILayout.HelpBox("加载中...", MessageType.Info);
                    }
                    else if (_filteredModules.Count == 0)
                    {
                        EditorGUILayout.HelpBox("没有找到模块", MessageType.Info);
                    }
                    else if (_selectedRegistryId == null)
                    {
                        // 全部视图：分组显示
                        DrawModuleGroup("已安装", _filteredModules.FindAll(m => m.IsInstalled));
                        foreach (var registry in HubSettings.Instance.GetEnabledRegistries())
                        {
                            if (_registryModules.TryGetValue(registry.id, out var modules))
                            {
                                var filtered = modules.FindAll(m => !m.IsInstalled && MatchFilter(m));
                                if (filtered.Count > 0)
                                    DrawModuleGroup(registry.name, filtered);
                            }
                        }
                    }
                    else
                    {
                        // 特定仓库/已安装视图：分组显示
                        DrawModuleGroup("已安装", _filteredModules.FindAll(m => m.IsInstalled));
                        DrawModuleGroup("未安装", _filteredModules.FindAll(m => !m.IsInstalled));
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawModuleGroup(string title, List<HubModuleInfo> modules)
        {
            if (modules.Count == 0) return;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"── {title} ({modules.Count}) ──", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(2);

            foreach (var module in modules)
                DrawModuleItem(module);
        }

        private bool MatchFilter(HubModuleInfo m)
        {
            // 搜索过滤
            if (!string.IsNullOrEmpty(_searchKeyword))
            {
                var keyword = _searchKeyword.ToLower();
                if (!m.ModuleId.ToLower().Contains(keyword) &&
                    !(m.DisplayName?.ToLower().Contains(keyword) ?? false) &&
                    !(m.Description?.ToLower().Contains(keyword) ?? false))
                    return false;
            }

            // 状态过滤
            return _filterIndex switch
            {
                1 => m.HasUpdate,
                2 => !m.IsInstalled,
                _ => true
            };
        }

        private void DrawModuleItem(HubModuleInfo module)
        {
            var isSelected = _selectedModule == module;
            var isEnabled = !module.IsInstalled || ModuleRegistrySettings.Instance.IsModuleEnabled(module.ModuleId);
            var bgColor = isSelected ? new Color(0.24f, 0.49f, 0.91f, 0.5f) : Color.clear;

            var rect = EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rect, bgColor);

                EditorGUILayout.BeginHorizontal();
                {
                    // 禁用的模块显示灰色图标
                    var icon = isEnabled ? "📦" : "📦";
                    var iconStyle = new GUIStyle(EditorStyles.label);
                    if (!isEnabled) iconStyle.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField(icon, iconStyle, GUILayout.Width(20));

                    var displayText = GetModuleDisplayText(module);
                    var nameStyle = new GUIStyle(EditorStyles.boldLabel);
                    if (!isEnabled) nameStyle.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField(displayText, nameStyle);
                    GUILayout.FlexibleSpace();

                    if (module.IsInstalled)
                    {
                        var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = isEnabled ? Color.green : Color.gray } };
                        EditorGUILayout.LabelField(module.HasUpdate ? $"v{module.InstalledVersion} → {module.LatestVersion}" : $"v{module.InstalledVersion}", style);
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"v{module.LatestVersion}", EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.EndHorizontal();

                // 显示来源仓库（已安装的模块）
                if (module.IsInstalled && !string.IsNullOrEmpty(module.SourceRegistryName))
                {
                    var sourceText = isEnabled ? $"来源: {module.SourceRegistryName}" : $"来源: {module.SourceRegistryName} [已禁用]";
                    var sourceStyle = new GUIStyle(EditorStyles.miniLabel);
                    if (!isEnabled) sourceStyle.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField(sourceText, sourceStyle);
                }
                else if (module.IsLocal)
                {
                    var sourceText = isEnabled ? "来源: 本地" : "来源: 本地 [已禁用]";
                    var sourceStyle = new GUIStyle(EditorStyles.miniLabel);
                    if (!isEnabled) sourceStyle.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField(sourceText, sourceStyle);
                }
            }
            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedModule = module;
                LoadModuleDetailAsync(module).Forget();
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(RightPanelWidth));
            {
                if (_selectedModule == null)
                {
                    EditorGUILayout.HelpBox("选择一个模块查看详情", MessageType.Info);
                }
                else
                {
                    _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                    {
                        EditorGUILayout.LabelField(GetModuleDisplayText(_selectedModule), EditorStyles.boldLabel);
                        EditorGUILayout.Space(5);

                        EditorGUILayout.LabelField($"ID: {_selectedModule.ModuleId}");

                        // 版本选择
                        if (_selectedModule.Versions != null && _selectedModule.Versions.Count > 1 && !_selectedModule.IsInstalled)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("版本:", GUILayout.Width(40));
                            var versions = _selectedModule.Versions.ToArray();
                            var newIndex = EditorGUILayout.Popup(_selectedVersionIndex, versions);
                            if (newIndex != _selectedVersionIndex)
                            {
                                _selectedVersionIndex = newIndex;
                                _selectedVersion = versions[newIndex];
                                LoadVersionDetailAsync(_selectedModule, _selectedVersion).Forget();
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"版本: {(_selectedModule.IsInstalled ? _selectedModule.InstalledVersion : _selectedModule.LatestVersion)}");
                        }

                        if (!string.IsNullOrEmpty(_selectedModule.Author))
                            EditorGUILayout.LabelField($"作者: {_selectedModule.Author}");
                        if (_selectedModule.Tags != null && _selectedModule.Tags.Length > 0)
                            EditorGUILayout.LabelField($"标签: {string.Join(", ", _selectedModule.Tags)}");
                        if (!string.IsNullOrEmpty(_selectedModule.UpdatedAt))
                            EditorGUILayout.LabelField($"更新时间: {FormatDateTime(_selectedModule.UpdatedAt)}");

                        // 显示来源仓库
                        if (_selectedModule.IsInstalled)
                        {
                            var source = _selectedModule.IsLocal ? "本地" : (_selectedModule.SourceRegistryName ?? "未知");
                            EditorGUILayout.LabelField($"来源: {source}");

                            // 启用/禁用模块
                            EditorGUILayout.Space(5);
                            var moduleEntry = GetOrCreateModuleEntry(_selectedModule.ModuleId);
                            var newEnabled = EditorGUILayout.Toggle("启用模块", moduleEntry.enabled);
                            if (newEnabled != moduleEntry.enabled)
                            {
                                moduleEntry.enabled = newEnabled;
                                SaveModuleRegistrySettings();
                            }
                        }

                        EditorGUILayout.Space(10);

                        if (!string.IsNullOrEmpty(_selectedModule.Description))
                        {
                            EditorGUILayout.LabelField("描述:", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(_selectedModule.Description, EditorStyles.wordWrappedLabel);
                        }

                        // 显示更新日志
                        if (!string.IsNullOrEmpty(_selectedModule.ReleaseNotes))
                        {
                            EditorGUILayout.Space(5);
                            EditorGUILayout.LabelField("更新日志:", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(_selectedModule.ReleaseNotes, EditorStyles.wordWrappedLabel);
                        }

                        // 显示依赖
                        if (_selectedModule.Dependencies != null && _selectedModule.Dependencies.Count > 0)
                        {
                            EditorGUILayout.Space(5);
                            EditorGUILayout.LabelField("依赖模块:", EditorStyles.boldLabel);
                            foreach (var dep in _selectedModule.Dependencies)
                                EditorGUILayout.LabelField($"  • {dep}", EditorStyles.miniLabel);
                        }

                        // 显示环境依赖
                        var envDeps = _selectedModule.Manifest?.envDependencies;
                        if (envDeps != null && envDeps.Length > 0)
                        {
                            EditorGUILayout.Space(5);
                            EditorGUILayout.LabelField("环境依赖:", EditorStyles.boldLabel);
                            var sourceNames = new[] { "NuGet", "GitHub", "URL", "Release" };
                            foreach (var env in envDeps)
                            {
                                var opt = env.optional ? " (可选)" : "";
                                var ver = !string.IsNullOrEmpty(env.version) ? $" v{env.version}" : "";
                                EditorGUILayout.LabelField($"  • {env.id}{ver} [{sourceNames[env.source]}]{opt}", EditorStyles.miniLabel);
                            }
                        }

                        EditorGUILayout.Space(10);

                        // 操作按钮
                        EditorGUI.BeginDisabledGroup(_isLoading);
                        {
                            if (_selectedModule.IsInstalled)
                            {
                                // 已安装模块
                                EditorGUILayout.BeginHorizontal();
                                {
                                    if (_selectedModule.HasUpdate && GUILayout.Button("更新", GUILayout.Height(30)))
                                        UpdateModuleAsync(_selectedModule).Forget();
                                    if (GUILayout.Button("卸载", GUILayout.Height(30)))
                                        UninstallModuleAsync(_selectedModule).Forget();
                                    // 本地模块可以编辑
                                    if (_selectedModule.IsLocal && GUILayout.Button("编辑", GUILayout.Height(30)))
                                    {
                                        var modulePath = System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{_selectedModule.ModuleId}");
                                        EditModuleWindow.Show(modulePath, GetAllAvailableModules(), () => RefreshModulesAsync().Forget());
                                    }
                                    // 定位目录（编辑器内）
                                    if (GUILayout.Button("定位", GUILayout.Height(30)))
                                    {
                                        var assetPath = $"Assets/Puffin/Modules/{_selectedModule.ModuleId}";
                                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                                        if (obj != null)
                                        {
                                            Selection.activeObject = obj;
                                            EditorGUIUtility.PingObject(obj);
                                        }
                                    }
                                }
                                EditorGUILayout.EndHorizontal();

                                // 非本地模块：开发者模式可以转换为本地
                                if (!_selectedModule.IsLocal && HubSettings.Instance.HasAnyToken())
                                {
                                    if (GUILayout.Button("转换为本地模块", GUILayout.Height(25)))
                                    {
                                        InstalledModulesLock.Instance.Remove(_selectedModule.ModuleId);
                                        _selectedModule.IsLocal = true;
                                        _selectedModule.SourceRegistryId = null;
                                        _selectedModule.SourceRegistryName = null;
                                        Repaint();
                                    }
                                }

                                // 本地模块有远程版本：可以还原为远程
                                if (_selectedModule.IsLocal && _selectedModule.HasRemote)
                                {
                                    if (GUILayout.Button("还原为远程模块", GUILayout.Height(25)))
                                    {
                                        if (EditorUtility.DisplayDialog("还原为远程模块",
                                            $"此操作将删除本地修改，从远程重新安装 {_selectedModule.ModuleId}。\n\n确定继续吗？",
                                            "还原", "取消"))
                                        {
                                            RestoreToRemoteAsync(_selectedModule).Forget();
                                        }
                                    }
                                }

                                // 本地模块可以上传到 Hub（需要有 token）
                                if (_selectedModule.IsLocal && HubSettings.Instance.HasAnyToken() && GUILayout.Button("上传到 Hub", GUILayout.Height(25)))
                                {
                                    var modulePath = System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{_selectedModule.ModuleId}");
                                    PublishModuleWindow.ShowWithPath(modulePath);
                                }
                            }
                            else
                            {
                                // 未安装模块 - 检查是否有冲突
                                var conflict = CheckInstallConflict(_selectedModule);
                                if (!string.IsNullOrEmpty(conflict))
                                {
                                    EditorGUILayout.HelpBox(conflict, MessageType.Warning);
                                }

                                var installVersion = !string.IsNullOrEmpty(_selectedVersion) ? _selectedVersion : _selectedModule.LatestVersion;
                                if (string.IsNullOrEmpty(installVersion))
                                {
                                    EditorGUILayout.HelpBox("无法获取版本信息", MessageType.Warning);
                                }
                                else if (string.IsNullOrEmpty(conflict))
                                {
                                    if (GUILayout.Button($"安装 v{installVersion}", GUILayout.Height(30)))
                                    {
                                        InstallModuleAsync(_selectedModule, installVersion).Forget();
                                    }
                                }

                                // 开发者模式：删除远程版本
                                if (HubSettings.Instance.HasToken(_selectedModule.RegistryId))
                                {
                                    EditorGUILayout.Space(5);
                                    var deleteVersion = !string.IsNullOrEmpty(_selectedVersion) ? _selectedVersion : _selectedModule.LatestVersion;
                                    if (GUILayout.Button($"删除远程 v{deleteVersion}", GUILayout.Height(25)))
                                    {
                                        if (EditorUtility.DisplayDialog("确认删除", $"确定要从远程仓库删除 {_selectedModule.ModuleId}@{deleteVersion} 吗？\n此操作不可恢复！", "删除", "取消"))
                                            DeleteRemoteVersionAsync(_selectedModule, deleteVersion).Forget();
                                    }
                                }
                            }
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                var updates = _installedModules.FindAll(m => m.HasUpdate).Count;
                EditorGUILayout.LabelField($"已安装: {_installedModules.Count} 个  |  可更新: {updates} 个", GUILayout.Width(180));

                if (_isLoading)
                {
                    // 状态信息
                    if (!string.IsNullOrEmpty(_statusMessage))
                        EditorGUILayout.LabelField(_statusMessage, GUILayout.Width(180));

                    // 进度条
                    var progressText = $"{_progress * 100:F0}%";
                    if (_downloadedBytes > 0)
                    {
                        var dlStr = FormatSize(_downloadedBytes);
                        var totalStr = _totalBytes > 0 ? $"/{FormatSize(_totalBytes)}" : "";
                        var speedStr = _downloadSpeed > 0 ? $" {FormatSize(_downloadSpeed)}/s" : "";
                        progressText = $"{dlStr}{totalStr}{speedStr}";
                    }
                    var rect = EditorGUILayout.GetControlRect(GUILayout.Width(200));
                    EditorGUI.ProgressBar(rect, _progress, progressText);
                }

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1048576) return $"{bytes / 1048576f:F2} MB";
            if (bytes >= 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }

        private static string FormatDateTime(string isoDateTime)
        {
            if (DateTime.TryParse(isoDateTime, out var dt))
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return isoDateTime;
        }

        private static string GetModuleDisplayText(HubModuleInfo module)
        {
            return !string.IsNullOrEmpty(module.DisplayName) ? module.DisplayName : module.ModuleId;
        }

        private void ApplyFilter()
        {
            var allModules = new List<HubModuleInfo>();

            if (_selectedRegistryId == null)
            {
                // 全部视图：已安装 + 所有远程
                allModules.AddRange(_installedModules);
                foreach (var kvp in _registryModules)
                    allModules.AddRange(kvp.Value.FindAll(m => !m.IsInstalled));
            }
            else if (_selectedRegistryId == "installed")
            {
                // 已安装视图
                allModules.AddRange(_installedModules);
            }
            else if (_registryModules.TryGetValue(_selectedRegistryId, out var modules))
            {
                // 特定仓库视图
                allModules.AddRange(modules);
            }

            // 应用搜索和状态过滤
            _filteredModules = allModules.FindAll(MatchFilter);
        }

        private string CheckInstallConflict(HubModuleInfo module)
        {
            // 检查是否已从其他仓库安装
            var installed = _installedModules.Find(m => m.ModuleId == module.ModuleId);
            if (installed != null && installed.SourceRegistryId != module.RegistryId)
            {
                var source = installed.IsLocal ? "本地" : (installed.SourceRegistryName ?? "其他仓库");
                return $"此模块已从 {source} 安装，请先卸载";
            }
            return null;
        }

        private async UniTaskVoid RefreshModulesAsync(bool force = false)
        {
            _isLoading = true;
            _statusMessage = "正在刷新...";
            Repaint();

            try
            {
                if (force)
                    _registryService.ClearCache();

                // 获取已安装模块
                _installedModules = _registryService.GetInstalledModules();
                var installedMap = _installedModules.ToDictionary(m => m.ModuleId);

                // 获取各仓库的远程模块
                _registryModules.Clear();
                foreach (var registry in HubSettings.Instance.GetEnabledRegistries())
                {
                    var modules = await _registryService.FetchRegistryModulesAsync(registry, installedMap, force);
                    _registryModules[registry.id] = modules;

                    // 更新已安装模块的远程版本信息
                    foreach (var remote in modules)
                    {
                        if (installedMap.TryGetValue(remote.ModuleId, out var installed) &&
                            installed.SourceRegistryId == registry.id)
                        {
                            installed.LatestVersion = remote.LatestVersion;
                            installed.HasUpdate = remote.HasUpdate;
                            installed.HasRemote = true;
                        }
                    }
                }

                ApplyFilter();
                var totalRemote = 0;
                foreach (var kvp in _registryModules) totalRemote += kvp.Value.Count;
                _statusMessage = $"已安装 {_installedModules.Count} 个，远程 {totalRemote} 个";
            }
            catch (Exception e)
            {
                _statusMessage = $"刷新失败: {e.Message}";
                Debug.LogError($"[Hub] {e}");
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }

        private async UniTaskVoid LoadModuleDetailAsync(HubModuleInfo module)
        {
            // 重置版本选择
            _selectedVersionIndex = 0;
            _selectedVersion = module.LatestVersion;

            var registry = HubSettings.Instance.registries.Find(r => r.id == module.RegistryId);
            if (registry == null) return;

            var manifest = await _registryService.GetManifestAsync(registry, module.ModuleId, module.LatestVersion);
            if (manifest != null)
                ApplyManifestToModule(module, manifest);
        }

        private async UniTaskVoid LoadVersionDetailAsync(HubModuleInfo module, string version)
        {
            var registry = HubSettings.Instance.registries.Find(r => r.id == module.RegistryId);
            if (registry == null) return;

            var manifest = await _registryService.GetManifestAsync(registry, module.ModuleId, version);
            if (manifest != null)
                ApplyManifestToModule(module, manifest);
        }

        private void ApplyManifestToModule(HubModuleInfo module, HubModuleManifest manifest)
        {
            module.Description = manifest.description;
            module.Author = manifest.author;
            module.Tags = manifest.tags;
            module.ReleaseNotes = manifest.releaseNotes;
            module.Dependencies = manifest.dependencies;
            module.Manifest = manifest;
            Repaint();
        }

        private async UniTaskVoid InstallModuleAsync(HubModuleInfo module, string version = null)
        {
            var targetVersion = version ?? module.LatestVersion;
            _isLoading = true;
            _statusMessage = $"正在安装 {module.ModuleId}...";
            Repaint();

            try
            {
                var success = await _installer.InstallAsync(module.ModuleId, targetVersion, module.RegistryId);
                if (success)
                {
                    module.IsInstalled = true;
                    module.InstalledVersion = targetVersion;
                    module.HasUpdate = false;
                    RefreshModulesAsync().Forget();
                }
                else
                {
                    _statusMessage = "安装失败，请查看控制台";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 安装异常: {e}");
                _statusMessage = $"安装异常: {e.Message}";
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }

        private async UniTaskVoid UpdateModuleAsync(HubModuleInfo module)
        {
            _isLoading = true;
            try
            {
                var success = await _installer.UpdateAsync(module.ModuleId, module.LatestVersion);
                if (success)
                {
                    module.InstalledVersion = module.LatestVersion;
                    module.HasUpdate = false;
                }
            }
            finally
            {
                _isLoading = false;
                _statusMessage = "";
                Repaint();
            }
        }

        private async UniTaskVoid UninstallModuleAsync(HubModuleInfo module)
        {
            // 先检查是否有模块依赖此模块
            var dependents = _installer.GetDependents(module.ModuleId);
            if (dependents.Count > 0)
            {
                EditorUtility.DisplayDialog("无法卸载",
                    $"以下模块依赖 {GetModuleDisplayText(module)}，请先卸载它们：\n\n• {string.Join("\n• ", dependents)}",
                    "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认卸载", $"确定要卸载 {GetModuleDisplayText(module)} 吗？", "卸载", "取消"))
                return;

            _isLoading = true;
            try
            {
                var success = await _installer.UninstallAsync(module.ModuleId);
                if (success)
                {
                    module.IsInstalled = false;
                    module.InstalledVersion = null;
                    module.HasUpdate = false;
                }
            }
            finally
            {
                _isLoading = false;
                _statusMessage = "";
                Repaint();
            }
        }

        private async UniTaskVoid DeleteRemoteVersionAsync(HubModuleInfo module, string version)
        {
            _isLoading = true;
            _statusMessage = "正在删除...";
            Repaint();

            try
            {
                var registry = HubSettings.Instance.registries.Find(r => r.id == module.RegistryId);
                if (registry != null)
                {
                    var publisher = new ModulePublisher();
                    var success = await publisher.DeleteVersionAsync(registry, module.ModuleId, version, s => { _statusMessage = s; Repaint(); });
                    if (success)
                    {
                        _selectedModule = null;
                        RefreshModulesAsync(true).Forget();
                    }
                }
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }

        private async UniTaskVoid RestoreToRemoteAsync(HubModuleInfo module)
        {
            _isLoading = true;
            _statusMessage = "正在还原...";
            Repaint();

            try
            {
                // 找到远程版本信息
                string registryId = null;
                string latestVersion = null;
                foreach (var kvp in _registryModules)
                {
                    var remote = kvp.Value.Find(m => m.ModuleId == module.ModuleId);
                    if (remote != null)
                    {
                        registryId = kvp.Key;
                        latestVersion = remote.LatestVersion;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(registryId) || string.IsNullOrEmpty(latestVersion))
                {
                    _statusMessage = "找不到远程版本";
                    return;
                }

                // 卸载当前模块
                var uninstalled = await _installer.UninstallAsync(module.ModuleId);
                if (!uninstalled)
                {
                    _statusMessage = "卸载失败";
                    return;
                }

                // 从远程重新安装
                var installed = await _installer.InstallAsync(module.ModuleId, latestVersion, registryId);
                if (installed)
                {
                    _statusMessage = "还原成功";
                    RefreshModulesAsync().Forget();
                }
                else
                {
                    _statusMessage = "安装失败";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 还原失败: {e}");
                _statusMessage = $"还原失败: {e.Message}";
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }

        private static int CompareVersions(string v1, string v2)
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

        /// <summary>
        /// 发布后刷新（清除缓存）
        /// </summary>
        public void RefreshAfterPublish()
        {
            RefreshModulesAsync(true).Forget();
        }

        /// <summary>
        /// 获取所有可用模块（已安装 + 远程，去重）
        /// </summary>
        private List<HubModuleInfo> GetAllAvailableModules()
        {
            var result = new List<HubModuleInfo>();
            var added = new HashSet<string>();

            // 添加已安装模块
            foreach (var m in _installedModules)
            {
                if (added.Add(m.ModuleId))
                    result.Add(m);
            }

            // 添加远程模块（合并版本信息）
            foreach (var kvp in _registryModules)
            {
                foreach (var m in kvp.Value)
                {
                    if (added.Contains(m.ModuleId))
                    {
                        // 合并版本信息到已存在的模块
                        var existing = result.Find(e => e.ModuleId == m.ModuleId);
                        if (existing != null && m.Versions != null)
                        {
                            existing.Versions ??= new List<string>();
                            foreach (var v in m.Versions)
                                if (!existing.Versions.Contains(v))
                                    existing.Versions.Add(v);
                        }
                    }
                    else
                    {
                        added.Add(m.ModuleId);
                        result.Add(m);
                    }
                }
            }

            return result;
        }

        private ModuleEntry GetOrCreateModuleEntry(string moduleId)
        {
            var settings = ModuleRegistrySettings.Instance;
            var entry = settings.modules.Find(m => m.moduleId == moduleId);
            if (entry == null)
            {
                entry = new ModuleEntry { moduleId = moduleId, enabled = true };
                settings.modules.Add(entry);
            }
            return entry;
        }

        private void SaveModuleRegistrySettings()
        {
            var settings = ModuleRegistrySettings.Instance;
            settings.ClearCache();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            ModuleRegistrySettings.NotifySettingsChanged();
        }
    }
}
#endif
