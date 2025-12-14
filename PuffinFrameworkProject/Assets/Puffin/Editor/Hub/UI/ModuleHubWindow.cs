#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Hub.Data;
using Puffin.Editor.Hub.Services;
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

        private const float LeftPanelWidth = 220f;
        private const float RightPanelWidth = 280f;

        [MenuItem("Puffin Framework/Module Hub", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<ModuleHubWindow>("Module Hub");
            window.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
            _registryService = new RegistryService();
            _resolver = new ModuleResolver(_registryService);
            _installer = new ModuleInstaller(_registryService, _resolver);

            _installer.OnProgress += (id, p) => { _progress = p; Repaint(); };
            _installer.OnStatusChanged += s => { _statusMessage = s; Repaint(); };

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

                if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(22)))
                    AddRegistryWindow.Show(r => { HubSettings.Instance.registries.Add(r); EditorUtility.SetDirty(HubSettings.Instance); RefreshModulesAsync().Forget(); });

                if (GUILayout.Button("发布", EditorStyles.toolbarButton, GUILayout.Width(40)))
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
                        foreach (var module in _filteredModules)
                            DrawModuleItem(module);
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
            var bgColor = isSelected ? new Color(0.24f, 0.49f, 0.91f, 0.5f) : Color.clear;

            var rect = EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rect, bgColor);

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("📦", GUILayout.Width(20));
                    EditorGUILayout.LabelField(module.DisplayName ?? module.ModuleId, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    if (module.IsInstalled)
                    {
                        var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.green } };
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
                    EditorGUILayout.LabelField($"来源: {module.SourceRegistryName}", EditorStyles.miniLabel);
                }
                else if (module.IsLocal)
                {
                    EditorGUILayout.LabelField("来源: 本地", EditorStyles.miniLabel);
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
                        EditorGUILayout.LabelField(_selectedModule.DisplayName ?? _selectedModule.ModuleId, EditorStyles.boldLabel);
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

                        // 显示来源仓库
                        if (_selectedModule.IsInstalled)
                        {
                            var source = _selectedModule.IsLocal ? "本地" : (_selectedModule.SourceRegistryName ?? "未知");
                            EditorGUILayout.LabelField($"来源: {source}");
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
                                }
                                EditorGUILayout.EndHorizontal();

                                // 本地模块可以上传
                                if (_selectedModule.IsLocal && GUILayout.Button("上传到 Hub", GUILayout.Height(25)))
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
                                else
                                {
                                    var installVersion = !string.IsNullOrEmpty(_selectedVersion) ? _selectedVersion : _selectedModule.LatestVersion;
                                    if (GUILayout.Button($"安装 v{installVersion}", GUILayout.Height(30)))
                                    {
                                        InstallModuleAsync(_selectedModule, installVersion).Forget();
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
                EditorGUILayout.LabelField($"已安装: {_installedModules.Count} 个  |  可更新: {updates} 个");

                GUILayout.FlexibleSpace();

                if (_isLoading)
                {
                    var rect = EditorGUILayout.GetControlRect(GUILayout.Width(100));
                    EditorGUI.ProgressBar(rect, _progress, "");
                }

                if (!string.IsNullOrEmpty(_statusMessage))
                    EditorGUILayout.LabelField(_statusMessage, GUILayout.Width(200));
            }
            EditorGUILayout.EndHorizontal();
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
                    var modules = await _registryService.FetchRegistryModulesAsync(registry, installedMap);
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
            {
                module.Description = manifest.description;
                module.Author = manifest.author;
                module.Tags = manifest.tags;
                module.ReleaseNotes = manifest.releaseNotes;
                Repaint();
            }
        }

        private async UniTaskVoid LoadVersionDetailAsync(HubModuleInfo module, string version)
        {
            var registry = HubSettings.Instance.registries.Find(r => r.id == module.RegistryId);
            if (registry == null) return;

            var manifest = await _registryService.GetManifestAsync(registry, module.ModuleId, version);
            if (manifest != null)
            {
                module.Description = manifest.description;
                module.Author = manifest.author;
                module.Tags = manifest.tags;
                module.ReleaseNotes = manifest.releaseNotes;
                Repaint();
            }
        }

        private async UniTaskVoid InstallModuleAsync(HubModuleInfo module, string version = null)
        {
            var targetVersion = version ?? module.LatestVersion;
            _isLoading = true;
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
            }
            finally
            {
                _isLoading = false;
                _statusMessage = "";
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
            if (!EditorUtility.DisplayDialog("确认卸载", $"确定要卸载 {module.DisplayName ?? module.ModuleId} 吗？", "卸载", "取消"))
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
    }

    /// <summary>
    /// 添加仓库窗口
    /// </summary>
    public class AddRegistryWindow : EditorWindow
    {
        private Action<RegistrySource> _onAdd;
        private string _name = "";
        private string _url = "";
        private string _branch = "main";

        public static void Show(Action<RegistrySource> onAdd)
        {
            var window = GetWindow<AddRegistryWindow>(true, "添加仓库源");
            window._onAdd = onAdd;
            window.minSize = window.maxSize = new Vector2(350, 130);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            _name = EditorGUILayout.TextField("名称", _name);
            _url = EditorGUILayout.TextField("URL (owner/repo)", _url);
            _branch = EditorGUILayout.TextField("分支", _branch);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Width(80))) Close();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_name) || string.IsNullOrEmpty(_url));
            if (GUILayout.Button("添加", GUILayout.Width(80)))
            {
                _onAdd?.Invoke(new RegistrySource
                {
                    id = Guid.NewGuid().ToString("N").Substring(0, 8),
                    name = _name, url = _url, branch = _branch, enabled = true
                });
                Close();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>
    /// 编辑仓库窗口
    /// </summary>
    public class EditRegistryWindow : EditorWindow
    {
        private RegistrySource _registry;
        private Action _onSave;

        public static void Show(RegistrySource registry, Action onSave)
        {
            var window = GetWindow<EditRegistryWindow>(true, "编辑仓库源");
            window._registry = registry;
            window._onSave = onSave;
            window.minSize = window.maxSize = new Vector2(350, 150);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (_registry == null) { Close(); return; }

            EditorGUILayout.Space(5);
            _registry.name = EditorGUILayout.TextField("名称", _registry.name);
            _registry.url = EditorGUILayout.TextField("URL (owner/repo)", _registry.url);
            _registry.branch = EditorGUILayout.TextField("分支", _registry.branch);
            _registry.authToken = EditorGUILayout.PasswordField("Token (可选)", _registry.authToken ?? "");
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Width(80))) Close();
            if (GUILayout.Button("保存", GUILayout.Width(80))) { _onSave?.Invoke(); Close(); }
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>
    /// 发布模块窗口
    /// </summary>
    public class PublishModuleWindow : EditorWindow
    {
        private string _modulePath = "";
        private ValidationResult _validation;
        private string _packagePath;
        private Vector2 _scroll;
        private ModulePublisher _publisher;
        private int _selectedRegistryIndex;
        private string[] _registryNames;
        private bool _isUploading;
        private string _uploadStatus;
        private string _releaseNotes = "";
        private Vector2 _releaseNotesScroll;

        public static void Show() => ShowWithPath("");

        public static void ShowWithPath(string path)
        {
            var window = GetWindow<PublishModuleWindow>(true, "发布模块");
            window.minSize = new Vector2(450, 350);
            window._publisher = new ModulePublisher();
            window._modulePath = path;
            if (!string.IsNullOrEmpty(path))
                window._validation = window._publisher.ValidateModule(path);
        }

        private void OnEnable()
        {
            _publisher ??= new ModulePublisher();
            RefreshRegistryList();
        }

        private void RefreshRegistryList()
        {
            var registries = HubSettings.Instance.registries;
            _registryNames = new string[registries.Count];
            for (int i = 0; i < registries.Count; i++)
                _registryNames[i] = registries[i].name;
        }

        private void OnGUI()
        {
            _publisher ??= new ModulePublisher();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("发布模块", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 目标仓库选择
            if (_registryNames == null || _registryNames.Length == 0)
            {
                EditorGUILayout.HelpBox("没有配置仓库源，请先在 Module Hub 中添加仓库", MessageType.Warning);
                return;
            }
            _selectedRegistryIndex = EditorGUILayout.Popup("目标仓库", _selectedRegistryIndex, _registryNames);
            var selectedRegistry = HubSettings.Instance.registries[_selectedRegistryIndex];
            EditorGUILayout.LabelField($"  URL: {selectedRegistry.url}", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // 模块路径选择
            EditorGUILayout.BeginHorizontal();
            _modulePath = EditorGUILayout.TextField("模块目录", _modulePath);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                var path = EditorUtility.OpenFolderPanel("选择模块目录", Application.dataPath + "/Puffin/Modules", "");
                if (!string.IsNullOrEmpty(path)) _modulePath = path;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 验证按钮
            if (GUILayout.Button("验证模块", GUILayout.Height(25)))
            {
                _validation = _publisher.ValidateModule(_modulePath);
                _packagePath = null;
            }

            // 显示验证结果
            if (_validation != null)
            {
                EditorGUILayout.Space(10);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.Height(150));
                {
                    if (_validation.IsValid)
                    {
                        EditorGUILayout.HelpBox("✓ 验证通过", MessageType.Info);
                        if (_validation.Manifest != null)
                        {
                            EditorGUILayout.LabelField($"模块ID: {_validation.Manifest.moduleId}");
                            EditorGUILayout.LabelField($"版本: {_validation.Manifest.version}");
                            EditorGUILayout.LabelField($"名称: {_validation.Manifest.displayName}");
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("✗ 验证失败", MessageType.Error);
                    }

                    foreach (var error in _validation.Errors)
                        EditorGUILayout.LabelField($"❌ {error}", EditorStyles.wordWrappedLabel);
                    foreach (var warning in _validation.Warnings)
                        EditorGUILayout.LabelField($"⚠ {warning}", EditorStyles.wordWrappedLabel);
                }
                EditorGUILayout.EndScrollView();

                // 更新日志输入
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("更新日志:", EditorStyles.boldLabel);
                _releaseNotesScroll = EditorGUILayout.BeginScrollView(_releaseNotesScroll, GUILayout.Height(60));
                _releaseNotes = EditorGUILayout.TextArea(_releaseNotes, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                // 打包按钮
                EditorGUI.BeginDisabledGroup(!_validation.IsValid);
                if (GUILayout.Button("打包模块", GUILayout.Height(30)))
                {
                    // 将 releaseNotes 写入 manifest
                    if (_validation.Manifest != null)
                        _validation.Manifest.releaseNotes = _releaseNotes;
                    PackageAsync().Forget();
                }
                EditorGUI.EndDisabledGroup();
            }

            // 显示打包结果
            if (!string.IsNullOrEmpty(_packagePath) && _validation?.Manifest != null)
            {
                EditorGUILayout.Space(10);
                var manifest = _validation.Manifest;
                var registry = HubSettings.Instance.registries[_selectedRegistryIndex];

                EditorGUILayout.HelpBox($"打包完成!\n{_packagePath}", MessageType.Info);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("上传目标:", EditorStyles.boldLabel);
                var uploadPath = $"modules/{manifest.moduleId}/{manifest.version}/";
                EditorGUILayout.TextField("路径", uploadPath);
                EditorGUILayout.LabelField($"仓库: {registry.url} (分支: {registry.branch})", EditorStyles.miniLabel);

                // Token 检查
                var hasToken = !string.IsNullOrEmpty(registry.authToken);
                if (!hasToken)
                    EditorGUILayout.HelpBox("需要配置 GitHub Token 才能自动上传。请在仓库设置中添加 Token。", MessageType.Warning);

                EditorGUILayout.Space(5);

                // 上传状态
                if (!string.IsNullOrEmpty(_uploadStatus))
                    EditorGUILayout.LabelField(_uploadStatus, EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("打开输出目录"))
                    EditorUtility.RevealInFinder(_packagePath);

                EditorGUI.BeginDisabledGroup(!hasToken || _isUploading);
                if (GUILayout.Button(_isUploading ? "上传中..." : "上传到 GitHub", GUILayout.Height(25)))
                    UploadAsync().Forget();
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
        }

        private async UniTaskVoid UploadAsync()
        {
            _isUploading = true;
            _uploadStatus = "准备上传...";
            Repaint();

            var registry = HubSettings.Instance.registries[_selectedRegistryIndex];
            var success = await _publisher.UploadToGitHubAsync(_packagePath, _validation.Manifest, registry, s => { _uploadStatus = s; Repaint(); });

            _isUploading = false;
            _uploadStatus = success ? "✓ 上传成功!" : "✗ 上传失败，请查看控制台";
            Repaint();

            // 上传成功后刷新 Hub 窗口
            if (success)
            {
                var hubWindow = GetWindow<ModuleHubWindow>(false, null, false);
                if (hubWindow != null)
                    hubWindow.RefreshAfterPublish();
            }
        }

        private async UniTaskVoid PackageAsync()
        {
            _packagePath = await _publisher.PackageModuleAsync(_modulePath, null, _validation?.Manifest);
            Repaint();
        }
    }
}
#endif
