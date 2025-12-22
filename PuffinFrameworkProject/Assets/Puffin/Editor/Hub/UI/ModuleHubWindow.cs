#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO.Compression;
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
        private const string PrefKeyLeftPanelWidth = "PuffinHub_LeftPanelWidth";
        private const string PrefKeyRightPanelWidth = "PuffinHub_RightPanelWidth";

        private Vector2 _registryScroll;
        private Vector2 _moduleListScroll;
        private Vector2 _detailScroll;

        private bool _isLoading;
        private bool _isInstalling; // 安装中（阻塞操作）
        private string _statusMessage = "";
        private float _progress;
        private long _downloadedBytes;
        private long _totalBytes;
        private long _downloadSpeed;

        // 环境依赖冲突检测
        private Dictionary<string, List<(string moduleId, EnvironmentDependency env)>> _envConflicts = new();

        // 可拖动面板
        private float _leftPanelWidth = 180f;
        private float _rightPanelWidth = 280f;
        private bool _isDraggingLeft;
        private bool _isDraggingRight;
        private const float MinPanelWidth = 120f;
        private const float MaxLeftPanelWidth = 300f;
        private const float MaxRightPanelWidth = 400f;
        private const float SplitterWidth = 1f;

        // 深色背景颜色
        private static readonly Color DarkBgColor = new(0.18f, 0.18f, 0.18f);
        private static readonly Color PanelBgColor = new(0.22f, 0.22f, 0.22f);
        private static readonly Color SplitterColor = new(0.12f, 0.12f, 0.12f);

        [MenuItem("Puffin/Module Manager", false, 10)]
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

            _installer.OnProgress += (id, p) =>
            {
                _progress = p;
                Repaint();
            };
            _installer.OnStatusChanged += s =>
            {
                _statusMessage = s;
                Repaint();
            };
            _installer.OnDownloadProgress += (p, dl, total, speed) =>
            {
                _progress = p;
                _downloadedBytes = dl;
                _totalBytes = total;
                _downloadSpeed = speed;
                Repaint();
            };

            // 定时刷新以更新下载进度显示
            EditorApplication.update += OnEditorUpdate;

            // 恢复选择的仓库源
            var saved = EditorPrefs.GetString(PrefKeySelectedRegistry, "");
            _selectedRegistryId = string.IsNullOrEmpty(saved) ? null : saved;

            // 恢复面板宽度
            _leftPanelWidth = EditorPrefs.GetFloat(PrefKeyLeftPanelWidth, 180f);
            _rightPanelWidth = EditorPrefs.GetFloat(PrefKeyRightPanelWidth, 280f);

            RefreshModulesAsync().Forget();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private double _lastRepaintTime;
        private void OnEditorUpdate()
        {
            // 每0.2秒刷新一次，避免过于频繁
            if (EditorApplication.timeSinceStartup - _lastRepaintTime < 0.2) return;
            _lastRepaintTime = EditorApplication.timeSinceStartup;

            // 检查是否有正在下载的任务
            var hasDownloading = false;
            foreach (var module in _filteredModules)
            {
                var task = _installer.GetDownloadTask(module.ModuleId);
                if (task != null && !task.IsCompleted && !task.IsFailed)
                {
                    hasDownloading = true;
                    break;
                }
            }

            if (hasDownloading)
                Repaint();
        }

        private void OnGUI()
        {
            // 绘制深色背景
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), DarkBgColor);

            DrawToolbar();

            var toolbarHeight = EditorStyles.toolbar.fixedHeight;
            var statusBarHeight = EditorStyles.toolbar.fixedHeight;
            var contentRect = new Rect(0, toolbarHeight, position.width,
                position.height - toolbarHeight - statusBarHeight);

            // 处理拖动
            HandleSplitterDrag(contentRect);

            // 计算面板区域
            var leftRect = new Rect(contentRect.x, contentRect.y, _leftPanelWidth, contentRect.height);
            var leftSplitterRect = new Rect(leftRect.xMax, contentRect.y, SplitterWidth, contentRect.height);

            float middleX = leftSplitterRect.xMax;
            var rightRect = new Rect(contentRect.xMax - _rightPanelWidth, contentRect.y, _rightPanelWidth,
                contentRect.height);
            var rightSplitterRect =
                new Rect(rightRect.x - SplitterWidth, contentRect.y, SplitterWidth, contentRect.height);
            float middleWidth = rightSplitterRect.x - middleX;
            var middleRect = new Rect(middleX, contentRect.y, middleWidth, contentRect.height);

            // 绘制面板
            DrawRegistryPanel(leftRect);
            DrawSplitter(leftSplitterRect);
            DrawModuleListPanel(middleRect);
            DrawSplitter(rightSplitterRect);
            DrawDetailPanel(rightRect);

            // 状态栏放在最下面
            var statusBarRect = new Rect(0, position.height - statusBarHeight, position.width, statusBarHeight);
            GUILayout.BeginArea(statusBarRect);
            DrawStatusBar();
            GUILayout.EndArea();

      
        }

        private void HandleSplitterDrag(Rect contentRect)
        {
            var e = Event.current;
            var leftSplitterRect = new Rect(_leftPanelWidth - 2, contentRect.y, SplitterWidth +5, contentRect.height);
            var rightSplitterRect = new Rect(contentRect.xMax - _rightPanelWidth - SplitterWidth - 2, contentRect.y,
                SplitterWidth + 5, contentRect.height);
            // 设置拖动光标
            EditorGUIUtility.AddCursorRect(leftSplitterRect, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(rightSplitterRect, MouseCursor.ResizeHorizontal);
            if (e.type == EventType.MouseDown)
            {
                if (leftSplitterRect.Contains(e.mousePosition))
                {
                    _isDraggingLeft = true;
                    e.Use();
                }
                else if (rightSplitterRect.Contains(e.mousePosition))
                {
                    _isDraggingRight = true;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp)
            {
                _isDraggingLeft = false;
                _isDraggingRight = false;
            }
            else if (e.type == EventType.MouseDrag)
            {
                if (_isDraggingLeft)
                {
                    _leftPanelWidth = Mathf.Clamp(e.mousePosition.x, MinPanelWidth, MaxLeftPanelWidth);
                    EditorPrefs.SetFloat(PrefKeyLeftPanelWidth, _leftPanelWidth);
                    Repaint();
                }
                else if (_isDraggingRight)
                {
                    _rightPanelWidth = Mathf.Clamp(contentRect.xMax - e.mousePosition.x, MinPanelWidth,
                        MaxRightPanelWidth);
                    EditorPrefs.SetFloat(PrefKeyRightPanelWidth, _rightPanelWidth);
                    Repaint();
                }
            }
        }

        private void DrawSplitter(Rect rect)
        {
            EditorGUI.DrawRect(rect, SplitterColor);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                // 导入按钮放最前面
                if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24)))
                    ImportPackage();

                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    RefreshModulesAsync(true).Forget();

                GUILayout.Space(10);
                GUILayout.Label("搜索:", GUILayout.Width(35));
                var newSearch = EditorGUILayout.TextField(_searchKeyword, EditorStyles.toolbarSearchField,
                    GUILayout.Width(150));
                if (newSearch != _searchKeyword)
                {
                    _searchKeyword = newSearch;
                    ApplyFilter();
                }

                GUILayout.Space(10);
                GUILayout.Label("筛选:", GUILayout.Width(35));
                var newFilter = EditorGUILayout.Popup(_filterIndex, _filterOptions, EditorStyles.toolbarPopup,
                    GUILayout.Width(80));
                if (newFilter != _filterIndex)
                {
                    _filterIndex = newFilter;
                    ApplyFilter();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("添加仓库", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    AddRegistryWindow.Show(r =>
                    {
                        HubSettings.Instance.registries.Add(r);
                        EditorUtility.SetDirty(HubSettings.Instance);
                        RefreshModulesAsync().Forget();
                    });

                if (GUILayout.Button("创建模块", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    CreateModuleWindow.Show(() => RefreshModulesAsync().Forget(), GetAllAvailableModules());

                // 只有存在有 token 的仓库时才显示发布按钮
                if (HubSettings.Instance.HasAnyToken() &&
                    GUILayout.Button("发布", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    PublishModuleWindow.Show();

                if (GUILayout.Button("设置", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    Core.PuffinSettingsWindow.ShowAndSelect<HubSettings>();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRegistryPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBgColor);
            GUILayout.BeginArea(rect);
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("仓库源", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                _registryScroll = EditorGUILayout.BeginScrollView(_registryScroll);
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
                        if (GUILayout.Button($"已安装 ({_installedModules.Count})", EditorStyles.label) &&
                            !installedSelected)
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
                        var rect2 = EditorGUILayout.BeginHorizontal();
                        {
                            if (isSelected && Event.current.type == EventType.Repaint)
                                EditorGUI.DrawRect(rect2, new Color(0.24f, 0.49f, 0.91f, 0.3f));

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

                    if (toRemove != null && EditorDialog.DisplayDecisionDialog("删除仓库", $"确定删除 {toRemove.name}？", "删除", "取消", DialogIconType.Warning))
                    {
                        HubSettings.Instance.registries.Remove(toRemove);
                        EditorUtility.SetDirty(HubSettings.Instance);
                        if (_selectedRegistryId == toRemove.id) _selectedRegistryId = null;
                        RefreshModulesAsync().Forget();
                    }

                    if (toEdit != null)
                        EditRegistryWindow.Show(toEdit, () =>
                        {
                            EditorUtility.SetDirty(HubSettings.Instance);
                            RefreshModulesAsync().Forget();
                        });
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawModuleListPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBgColor);
            GUILayout.BeginArea(rect);
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"模块 ({_filteredModules.Count})", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                // 显示环境依赖冲突警告
                if (_envConflicts.Count > 0)
                {
                    var sourceNames = new[] { "NuGet", "GitHub", "URL", "Release", "UPM" };
                    var details = string.Join("\n", _envConflicts.Select(kvp =>
                    {
                        var configs = string.Join(", ", kvp.Value.Select(v =>
                        {
                            var src = sourceNames[v.env.source];
                            return $"{v.moduleId}:[{src}]v{v.env.version}";
                        }));
                        return $"• {kvp.Key}: {configs}";
                    }));
                    EditorGUILayout.HelpBox($"⚠ 环境依赖配置冲突:\n{details}", MessageType.Warning);
                    EditorGUILayout.Space(2);
                }

                _moduleListScroll = EditorGUILayout.BeginScrollView(_moduleListScroll);
                {
                    if (_isLoading)
                    {
                        DrawCenteredMessage("加载中...");
                    }
                    else if (_filteredModules.Count == 0)
                    {
                        DrawCenteredMessage("没有模块数据");
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
            GUILayout.EndArea();
        }

        private void DrawCenteredMessage(string message)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(message, EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
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

                // 检查是否在可见区域内，触发懒加载
                if (Event.current.type == EventType.Repaint && IsRectVisible(rect) && !module.IsInstalled)
                    TryLoadModuleManifest(module);

                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.Space(20);

                    // 根据加载状态显示不同图标
                    var icon = module.LoadState == ModuleLoadState.Loading ? "⏳" :
                        module.LoadState == ModuleLoadState.Failed ? "⚠" : "📦";
                    EditorGUILayout.LabelField(icon, GUILayout.Width(18));

                    var displayText = GetModuleDisplayText(module);
                    EditorGUILayout.LabelField(displayText, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    // 加载失败时显示重试按钮
                    if (module.LoadState == ModuleLoadState.Failed)
                    {
                        if (GUILayout.Button("↻", GUILayout.Width(20), GUILayout.Height(18)))
                        {
                            module.LoadState = ModuleLoadState.NotLoaded;
                            TryLoadModuleManifest(module);
                        }
                    }

                    // 检查下载状态
                    var downloadTask = _installer.GetDownloadTask(module.ModuleId);
                    var isModuleDownloading = downloadTask != null && !downloadTask.IsCompleted && !downloadTask.IsFailed;

                    if (isModuleDownloading)
                    {
                        // 显示下载进度
                        var progressText = downloadTask.Total > 0
                            ? $"{downloadTask.Progress * 100:F0}%"
                            : "下载中...";
                        var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.cyan } };
                        EditorGUILayout.LabelField(progressText, style, GUILayout.Width(50));
                    }
                    else if (module.IsInstalled)
                    {
                        var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.green } };

                        // 检查是否是当前选中的模块且选择了不同版本
                        var showVersionChange = _selectedModule == module &&
                                                !string.IsNullOrEmpty(_selectedVersion) &&
                                                _selectedVersion != module.InstalledVersion;

                        if (showVersionChange)
                        {
                            // 显示版本切换：当前版本 -> 选中版本
                            style.normal.textColor = Color.yellow;
                            EditorGUILayout.LabelField($"v{module.InstalledVersion} → {_selectedVersion}", style);
                        }
                        else
                        {
                            // 只显示当前安装版本
                            EditorGUILayout.LabelField($"v{module.InstalledVersion}", style);
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"v{module.LatestVersion}", EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.EndHorizontal();

                // 显示来源仓库（已安装的模块）
                if (module.IsInstalled)
                {
                    var sourceName = !string.IsNullOrEmpty(module.SourceRegistryName) ? module.SourceRegistryName : "本地";
                    EditorGUILayout.LabelField($"来源: {sourceName}", EditorStyles.miniLabel);
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

        private bool IsRectVisible(Rect rect)
        {
            // 检查 rect 是否在当前滚动视图的可见区域内
            var scrollViewRect = new Rect(0, 0, position.width - _leftPanelWidth - _rightPanelWidth,
                position.height - 60);
            var adjustedRect = new Rect(rect.x, rect.y - _moduleListScroll.y, rect.width, rect.height);
            return adjustedRect.yMax > 0 && adjustedRect.yMin < scrollViewRect.height;
        }

        private void TryLoadModuleManifest(HubModuleInfo module)
        {
            if (module.LoadState != ModuleLoadState.NotLoaded) return;
            LoadModuleManifestAsync(module).Forget();
        }

        private async UniTaskVoid LoadModuleManifestAsync(HubModuleInfo module)
        {
            await _registryService.LoadModuleManifestAsync(module);
            Repaint();
        }

        private void DrawDetailPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBgColor);
            GUILayout.BeginArea(rect);
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.Space(4);
                if (_selectedModule == null)
                {
                    // 没有选中模块时显示空状态
                    DrawCenteredMessage("选择一个模块查看详情");
                }
                else
                {
                    _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                    {
                        // 显示加载状态
                        if (_selectedModule.LoadState == ModuleLoadState.Loading)
                        {
                            EditorGUILayout.HelpBox("正在加载模块信息...", MessageType.Info);
                            EditorGUILayout.Space(5);
                        }
                        else if (_selectedModule.LoadState == ModuleLoadState.Failed)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.HelpBox("加载失败", MessageType.Warning);
                            if (GUILayout.Button("重试", GUILayout.Width(50), GUILayout.Height(38)))
                            {
                                _selectedModule.LoadState = ModuleLoadState.NotLoaded;
                                LoadModuleManifestAsync(_selectedModule).Forget();
                            }

                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.Space(5);
                        }

                        // 标题栏 + 快捷图标按钮
                        EditorGUILayout.BeginHorizontal();
                        {
                            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = false };
                            EditorGUILayout.LabelField(GetModuleDisplayText(_selectedModule), titleStyle, GUILayout.MaxWidth(100));
                            GUILayout.FlexibleSpace();
                            if (_selectedModule.IsInstalled)
                            {
                                var modulePath = System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{_selectedModule.ModuleId}");

                                // 定位
                                if (GUILayout.Button("📍", GUILayout.Width(22), GUILayout.Height(18)))
                                {
                                    var assetPath = $"Assets/Puffin/Modules/{_selectedModule.ModuleId}";
                                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                                    if (obj != null)
                                    {
                                        Selection.activeObject = obj;
                                        EditorGUIUtility.PingObject(obj);
                                    }
                                }
                                // 编辑（本地模块或有token的远程模块）
                                var registryId = !string.IsNullOrEmpty(_selectedModule.SourceRegistryId) ? _selectedModule.SourceRegistryId : _selectedModule.RegistryId;
                                var isLocal = registryId == "local" || string.IsNullOrEmpty(registryId);
                                if ((isLocal || HubSettings.Instance.HasToken(registryId)) && GUILayout.Button("✏", GUILayout.Width(22), GUILayout.Height(18)))
                                {
                                    EditModuleWindow.Show(modulePath, GetAllAvailableModules(), () => RefreshModulesAsync().Forget());
                                }
                                // 上传（本地模块或有token的远程模块）
                                if ((isLocal || HubSettings.Instance.HasToken(registryId)) && GUILayout.Button("⬆", GUILayout.Width(22), GUILayout.Height(18)))
                                {
                                    PublishModuleWindow.ShowWithPath(modulePath);
                                }
                                // 导出
                                if (GUILayout.Button("📦", GUILayout.Width(22), GUILayout.Height(18)))
                                    ExportPackage(_selectedModule);
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space(3);

                        EditorGUILayout.LabelField($"ID: {_selectedModule.ModuleId}");

                        // 版本选择 + 操作按钮
                        if (_selectedModule.Versions != null && _selectedModule.Versions.Count > 0)
                        {
                            // 版本排序：从新到旧
                            var versions = _selectedModule.Versions.OrderByDescending(v => v, new VersionComparer()).ToArray();

                            // 已安装模块：默认选中当前安装的版本
                            if (_selectedModule.IsInstalled && !string.IsNullOrEmpty(_selectedModule.InstalledVersion))
                            {
                                var installedIdx = Array.IndexOf(versions, _selectedModule.InstalledVersion);
                                if (installedIdx >= 0 && _selectedVersionIndex != installedIdx && string.IsNullOrEmpty(_selectedVersion))
                                {
                                    _selectedVersionIndex = installedIdx;
                                    _selectedVersion = _selectedModule.InstalledVersion;
                                }
                            }

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("版本:", GUILayout.Width(40));
                            var newIndex = EditorGUILayout.Popup(_selectedVersionIndex, versions);
                            if (newIndex != _selectedVersionIndex)
                            {
                                _selectedVersionIndex = newIndex;
                                _selectedVersion = versions[newIndex];
                                LoadVersionDetailAsync(_selectedModule, _selectedVersion).Forget();
                            }

                            // 当前选中的版本
                            var currentVer = !string.IsNullOrEmpty(_selectedVersion) ? _selectedVersion : versions[0];
                            var isCurrentVersion = _selectedModule.IsInstalled && _selectedModule.InstalledVersion == currentVer;
                            var hasCache = _installer.HasCache(_selectedModule.ModuleId, currentVer);

                            // 操作按钮（不包括卸载，卸载在下面）
                            EditorGUI.BeginDisabledGroup(_isInstalling);
                            var oldColor = GUI.backgroundColor;
                            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
                            if (_selectedModule.IsInstalled)
                            {
                                // 已安装：选择不同版本时显示切换或下载
                                if (!isCurrentVersion)
                                {
                                    if (hasCache)
                                    {
                                        if (GUILayout.Button("切换", GUILayout.Width(50), GUILayout.Height(18)))
                                            SwitchVersionAsync(_selectedModule, currentVer).Forget();
                                    }
                                    else
                                    {
                                        if (GUILayout.Button("下载", GUILayout.Width(50), GUILayout.Height(18)))
                                            DownloadModuleAsync(_selectedModule, currentVer).Forget();
                                    }
                                }
                            }
                            else
                            {
                                // 未安装：显示安装或下载
                                if (hasCache)
                                {
                                    if (GUILayout.Button("安装", GUILayout.Width(50), GUILayout.Height(18)))
                                        InstallFromCacheAsync(_selectedModule, currentVer).Forget();
                                }
                                else
                                {
                                    if (GUILayout.Button("下载", GUILayout.Width(50), GUILayout.Height(18)))
                                        DownloadModuleAsync(_selectedModule, currentVer).Forget();
                                }
                            }
                            GUI.backgroundColor = oldColor;

                            // 选项菜单
                            if (GUILayout.Button("选项", EditorStyles.miniButton, GUILayout.Width(35), GUILayout.Height(18)))
                            {
                                var menu = new GenericMenu();
                                if (hasCache)
                                {
                                    menu.AddItem(new GUIContent("重新下载"), false, () =>
                                    {
                                        _installer.DeleteCache(_selectedModule.ModuleId, currentVer);
                                        DownloadModuleAsync(_selectedModule, currentVer).Forget();
                                    });
                                    menu.AddItem(new GUIContent("删除缓存"), false, () =>
                                    {
                                        _installer.DeleteCache(_selectedModule.ModuleId, currentVer);
                                        Repaint();
                                    });
                                }
                                else
                                {
                                    menu.AddDisabledItem(new GUIContent("无缓存"));
                                }

                                // 删除远程版本（需要 token）
                                var registryId = _selectedModule.SourceRegistryId ?? _selectedModule.RegistryId;
                                var registry = HubSettings.Instance.registries.Find(r => r.id == registryId);
                                if (registry != null && !string.IsNullOrEmpty(registry.authToken))
                                {
                                    menu.AddSeparator("");
                                    var verToDelete = currentVer;
                                    menu.AddItem(new GUIContent($"删除远程版本 ({verToDelete})"), false, () =>
                                    {
                                        DeleteRemoteVersionAsync(_selectedModule, verToDelete, registry).Forget();
                                    });
                                }

                                menu.ShowAsContext();
                            }
                            EditorGUI.EndDisabledGroup();

                            EditorGUILayout.EndHorizontal();
                        }
                        else if (_selectedModule.IsInstalled)
                        {
                            EditorGUILayout.LabelField($"版本: {_selectedModule.InstalledVersion}");
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"版本: {_selectedModule.LatestVersion}");
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
                            var source = _selectedModule.SourceRegistryName ?? "未知";
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

                        // 显示依赖模块
                        var allDeps = _selectedModule.Manifest?.moduleDependencies ?? _selectedModule.Dependencies;
                        if (allDeps != null && allDeps.Count > 0)
                        {
                            EditorGUILayout.Space(5);
                            EditorGUILayout.LabelField("依赖模块:", EditorStyles.boldLabel);
                            foreach (var dep in allDeps)
                            {
                                var modulesDir = System.IO.Path.Combine(Application.dataPath, "Puffin/Modules");
                                var depPath = System.IO.Path.Combine(modulesDir, dep.moduleId);
                                var isDepInstalled = System.IO.Directory.Exists(depPath);

                                // 显示格式: hub|模块@版本
                                var registryName = GetDependencyRegistryName(dep.registryId);
                                var versionText = string.IsNullOrEmpty(dep.version) ? "最新" : dep.version;
                                var displayText = $"{registryName}|{dep.moduleId}@{versionText}";
                                var optText = dep.optional ? " (可选)" : "";

                                EditorGUILayout.BeginHorizontal();
                                if (isDepInstalled)
                                {
                                    EditorGUILayout.LabelField($"  • {displayText}{optText} ✓", EditorStyles.miniLabel);
                                }
                                else
                                {
                                    var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.yellow } };
                                    EditorGUILayout.LabelField($"  • {displayText}{optText}", style);
                                    EditorGUI.BeginDisabledGroup(_isInstalling);
                                    if (GUILayout.Button("安装", EditorStyles.miniButton, GUILayout.Width(40)))
                                    {
                                        InstallDependency(dep.moduleId, dep.registryId);
                                    }
                                    EditorGUI.EndDisabledGroup();
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                        }

                        // 显示环境依赖
                        var envDeps = _selectedModule.Manifest?.envDependencies;
                        if (envDeps != null && envDeps.Length > 0)
                        {
                            EditorGUILayout.Space(5);
                            EditorGUILayout.LabelField("环境依赖:", EditorStyles.boldLabel);
                            var sourceNames = new[] { "NuGet", "GitHub", "URL", "Release", "UPM" };
                            var typeNames = new[] { "DLL", "Source", "Tool" };
                            foreach (var env in envDeps)
                            {
                                var opt = env.optional ? " (可选)" : "";
                                var ver = !string.IsNullOrEmpty(env.version) ? $" v{env.version}" : "";
                                var typeOrSource = sourceNames[env.source];

                                // 已安装模块显示环境依赖安装状态
                                if (_selectedModule.IsInstalled)
                                {
                                    var isEnvInstalled = IsEnvDependencyInstalled(env);
                                    var status = isEnvInstalled ? "✓" : (env.optional ? "○" : "⚠");
                                    var style = (isEnvInstalled || env.optional) ? EditorStyles.miniLabel : new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.yellow } };
                                    EditorGUILayout.LabelField($"  {status} {env.id}{ver} [{typeOrSource}]{opt}", style);
                                }
                                else
                                {
                                    EditorGUILayout.LabelField($"  • {env.id}{ver} [{typeOrSource}]{opt}", EditorStyles.miniLabel);
                                }
                            }
                        }

                        // 显示程序集引用
                        var refsText = _selectedModule.Manifest?.GetReferences() ?? "";
                        if (!string.IsNullOrWhiteSpace(refsText))
                        {
                            EditorGUILayout.Space(5);
                            EditorGUILayout.LabelField("程序集引用:", EditorStyles.boldLabel);
                            // 排序：必须的在前，可选的在后
                            var refs = refsText.Split(';')
                                .Select(r => r.Trim())
                                .Where(r => !string.IsNullOrEmpty(r))
                                .OrderBy(r => r.StartsWith("#") ? 1 : 0)
                                .ToList();
                            foreach (var trimmed in refs)
                            {
                                var isOptional = trimmed.StartsWith("#");
                                var actualName = isOptional ? trimmed.Substring(1) : trimmed;
                                var optText = isOptional ? " (可选)" : "";
                                var isDll = actualName.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase);
                                if (_selectedModule.IsInstalled)
                                {
                                    var found = isDll ? IsDllAvailable(actualName) : IsAsmdefAvailable(actualName.Replace(".asmdef", ""));
                                    var status = found ? "✓" : (isOptional ? "○" : "⚠");
                                    var style = (found || isOptional) ? EditorStyles.miniLabel : new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.yellow } };
                                    EditorGUILayout.LabelField($"  {status} {actualName}{optText}", style);
                                }
                                else
                                {
                                    EditorGUILayout.LabelField($"  • {actualName}{optText}", EditorStyles.miniLabel);
                                }
                            }
                        }

                        EditorGUILayout.Space(10);

                        // 下载进度显示
                        var isDownloading = _installer.IsDownloading(_selectedModule.ModuleId);
                        if (isDownloading)
                        {
                            var task = _installer.GetDownloadTask(_selectedModule.ModuleId);
                            var progressText = task?.Total > 0
                                ? $"下载中 {task.Progress * 100:F0}% ({FormatSize(task.Downloaded)}/{FormatSize(task.Total)})"
                                : "下载中...";
                            EditorGUILayout.HelpBox(progressText, MessageType.Info);
                        }

                        // 卸载按钮（已安装模块）
                        if (_selectedModule.IsInstalled)
                        {
                            EditorGUILayout.Space(5);
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();
                            EditorGUI.BeginDisabledGroup(_isInstalling);
                            var oldColor2 = GUI.backgroundColor;
                            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                            if (GUILayout.Button("卸载", GUILayout.Height(24), GUILayout.Width(80)))
                                UninstallModuleAsync(_selectedModule).Forget();
                            GUI.backgroundColor = oldColor2;
                            EditorGUI.EndDisabledGroup();
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                var updates = _installedModules.FindAll(m => m.HasUpdate).Count;
                EditorGUILayout.LabelField($"已安装: {_installedModules.Count} 个  |  可更新: {updates} 个",
                    GUILayout.Width(180));

                // 显示状态信息
                if (!string.IsNullOrEmpty(_statusMessage))
                    EditorGUILayout.LabelField(_statusMessage, GUILayout.Width(180));

                // 显示下载/安装进度
                if (_isLoading || _isInstalling)
                {
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
                var source = installed.SourceRegistryName ?? "其他仓库";
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
                        }
                    }
                }

                ApplyFilter();
                ScanEnvConflicts();

                // 更新选中的模块引用（指向新的对象）
                if (_selectedModule != null)
                {
                    var selectedId = _selectedModule.ModuleId;
                    _selectedModule = _installedModules.Find(m => m.ModuleId == selectedId)
                                      ?? _filteredModules.Find(m => m.ModuleId == selectedId);
                }

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

            // 获取正确的仓库ID（已安装模块优先使用 SourceRegistryId）
            var registryId = !string.IsNullOrEmpty(module.SourceRegistryId) ? module.SourceRegistryId : module.RegistryId;
            var registry = HubSettings.Instance.registries.Find(r => r.id == registryId);

            // 如果没有版本列表，尝试从远程获取
            if ((module.Versions == null || module.Versions.Count == 0) && registry != null)
            {
                var versions = await _registryService.GetVersionsAsync(registry, module.ModuleId);
                if (versions.Count > 0)
                    module.Versions = versions;
            }

            if (module.IsInstalled && !string.IsNullOrEmpty(module.InstalledVersion))
            {
                // 已安装模块：默认选择当前安装的版本
                _selectedVersion = module.InstalledVersion;
                if (module.Versions != null && module.Versions.Count > 0)
                {
                    var sortedVersions = module.Versions.OrderByDescending(v => v, new VersionComparer()).ToList();
                    var idx = sortedVersions.IndexOf(module.InstalledVersion);
                    if (idx >= 0) _selectedVersionIndex = idx;
                }
            }
            else if (module.Versions != null && module.Versions.Count > 0)
            {
                // 未安装模块：选择最新版本
                var sortedVersions = module.Versions.OrderByDescending(v => v, new VersionComparer()).ToList();
                _selectedVersion = sortedVersions[0];
            }
            else
            {
                _selectedVersion = module.LatestVersion;
            }

            // 已安装模块使用本地信息，不从远程加载
            if (module.IsInstalled) return;

            if (registry == null) return;

            var manifest = await _registryService.GetManifestAsync(registry, module.ModuleId, module.LatestVersion);
            if (manifest != null)
                ApplyManifestToModule(module, manifest);
        }

        private async UniTaskVoid LoadVersionDetailAsync(HubModuleInfo module, string version)
        {
            // 已安装模块且查看当前安装版本时，使用本地信息
            if (module.IsInstalled && version == module.InstalledVersion) return;

            var registryId = !string.IsNullOrEmpty(module.SourceRegistryId) ? module.SourceRegistryId : module.RegistryId;
            var registry = HubSettings.Instance.registries.Find(r => r.id == registryId);
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
            module.Dependencies = manifest.moduleDependencies;
            module.Manifest = manifest;
            Repaint();
        }

        /// <summary>
        /// 仅下载模块（不安装）
        /// </summary>
        private async UniTaskVoid DownloadModuleAsync(HubModuleInfo module, string version)
        {
            // 获取正确的仓库ID（已安装模块使用 SourceRegistryId）
            var registryId = !string.IsNullOrEmpty(module.SourceRegistryId) ? module.SourceRegistryId : module.RegistryId;
            if (string.IsNullOrEmpty(registryId))
            {
                _statusMessage = "无法确定模块来源仓库";
                Debug.LogError($"[Hub] 模块 {module.ModuleId} 没有有效的仓库ID");
                Repaint();
                return;
            }

            _statusMessage = $"正在下载 {module.ModuleId}@{version}...";
            Repaint();

            var success = await _installer.DownloadAsync(module.ModuleId, version, registryId);
            if (success)
            {
                _statusMessage = "下载完成";
            }
            else
            {
                var task = _installer.GetDownloadTask(module.ModuleId);
                _statusMessage = task?.Error ?? "下载失败";
            }
            Repaint();
        }

        /// <summary>
        /// 切换已安装模块的版本
        /// </summary>
        private async UniTaskVoid SwitchVersionAsync(HubModuleInfo module, string targetVersion)
        {
            _isInstalling = true;
            _statusMessage = $"正在切换版本: {module.ModuleId} -> v{targetVersion}";
            Repaint();

            try
            {
                // 先卸载当前版本
                var uninstalled = await _installer.UninstallAsync(module.ModuleId);
                if (!uninstalled)
                {
                    _statusMessage = "卸载失败";
                    return;
                }

                // 从缓存安装目标版本
                var success = await _installer.InstallFromCacheAsync(module.ModuleId, targetVersion, module.SourceRegistryId ?? module.RegistryId);
                if (success)
                {
                    module.InstalledVersion = targetVersion;
                    _statusMessage = "版本切换成功";
                    RefreshModulesAsync().Forget();
                }
                else
                {
                    _statusMessage = "安装失败";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 切换版本异常: {e}");
                _statusMessage = $"切换失败: {e.Message}";
            }
            finally
            {
                _isInstalling = false;
                Repaint();
            }
        }

        /// <summary>
        /// 从缓存安装模块
        /// </summary>
        private async UniTaskVoid InstallFromCacheAsync(HubModuleInfo module, string version)
        {
            var registryId = !string.IsNullOrEmpty(module.SourceRegistryId) ? module.SourceRegistryId : module.RegistryId;
            _isInstalling = true;
            _statusMessage = $"正在安装 {module.ModuleId}...";
            Repaint();

            try
            {
                var success = await _installer.InstallFromCacheAsync(module.ModuleId, version, registryId);
                if (success)
                {
                    module.IsInstalled = true;
                    module.InstalledVersion = version;
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
                _isInstalling = false;
                Repaint();
            }
        }

        /// <summary>
        /// 获取依赖的仓库源名称
        /// </summary>
        private string GetDependencyRegistryName(string registryId)
        {
            if (string.IsNullOrEmpty(registryId)) return "自动";
            var registry = HubSettings.Instance.registries.Find(r => r.id == registryId);
            return registry?.name ?? registryId;
        }

        /// <summary>
        /// 检查环境依赖是否已安装
        /// </summary>
        private bool IsEnvDependencyInstalled(EnvironmentDependency env)
        {
            var depDef = new Puffin.Editor.Environment.DependencyDefinition
            {
                id = env.id,
                source = (Puffin.Editor.Environment.DependencySource)env.source,
                type = (Puffin.Editor.Environment.DependencyType)env.type,
                url = env.url,
                version = env.version,
                installDir = env.installDir,
                extractPath = env.extractPath,
                requiredFiles = env.requiredFiles
            };
            var depManager = new Puffin.Editor.Environment.DependencyManager();
            return depManager.IsInstalled(depDef);
        }

        /// <summary>
        /// 检查 asmdef 引用是否可用
        /// </summary>
        private bool IsAsmdefAvailable(string asmdefName)
        {
            var guids = AssetDatabase.FindAssets($"t:asmdef {asmdefName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == asmdefName)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查 DLL 引用是否可用
        /// </summary>
        private bool IsDllAvailable(string dllName)
        {
            var searchName = dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? System.IO.Path.GetFileNameWithoutExtension(dllName)
                : dllName;
            var guids = AssetDatabase.FindAssets($"t:DefaultAsset {searchName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                    System.IO.Path.GetFileNameWithoutExtension(path).Equals(searchName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 安装依赖模块
        /// </summary>
        private void InstallDependency(string moduleId, string registryId = null)
        {
            // 优先在指定仓库中查找
            HubModuleInfo targetModule = null;
            if (!string.IsNullOrEmpty(registryId) && _registryModules.TryGetValue(registryId, out var modules))
            {
                targetModule = modules.Find(m => m.ModuleId == moduleId);
            }

            // 如果指定仓库没找到，在所有仓库中查找
            if (targetModule == null)
            {
                foreach (var kvp in _registryModules)
                {
                    targetModule = kvp.Value.Find(m => m.ModuleId == moduleId);
                    if (targetModule != null) break;
                }
            }

            if (targetModule == null)
            {
                EditorDialog.DisplayAlertDialog("安装失败", $"未找到模块: {moduleId}", "确定", DialogIconType.Error);
                return;
            }

            InstallModuleAsync(targetModule, targetModule.LatestVersion).Forget();
        }

        /// <summary>
        /// 下载并安装模块（旧方法，保留兼容）
        /// </summary>
        private async UniTaskVoid InstallModuleAsync(HubModuleInfo module, string version = null)
        {
            var targetVersion = version ?? module.LatestVersion;
            var registryId = !string.IsNullOrEmpty(module.SourceRegistryId) ? module.SourceRegistryId : module.RegistryId;

            // 1. 后台下载（不阻塞UI）
            _statusMessage = $"正在下载 {module.ModuleId}...";
            Repaint();

            var downloadSuccess = await _installer.DownloadAsync(module.ModuleId, targetVersion, registryId);
            if (!downloadSuccess)
            {
                var task = _installer.GetDownloadTask(module.ModuleId);
                _statusMessage = task?.Error ?? "下载失败";
                Repaint();
                return;
            }

            // 2. 安装（阻塞UI）
            _isInstalling = true;
            _statusMessage = $"正在安装 {module.ModuleId}...";
            Repaint();

            try
            {
                var success = await _installer.InstallFromCacheAsync(module.ModuleId, targetVersion, registryId);
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
                _isInstalling = false;
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
            // 检查是否有模块依赖此模块
            var dependents = _installer.GetDependents(module.ModuleId);
            string message;
            if (dependents.Count > 0)
            {
                message = $"以下模块依赖 {GetModuleDisplayText(module)}，卸载后它们将丢失依赖：\n\n• {string.Join("\n• ", dependents)}\n\n确定要卸载吗？";
            }
            else
            {
                message = $"确定要卸载 {GetModuleDisplayText(module)} 吗？";
            }

            if (!EditorDialog.DisplayDecisionDialog("确认卸载", message, "卸载", "取消", DialogIconType.Warning))
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
                    // 刷新模块列表以更新禁用状态
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

        private async UniTaskVoid DeleteRemoteVersionAsync(HubModuleInfo module, string version, RegistrySource registry)
        {
            if (!EditorDialog.DisplayDecisionDialog("确认删除", $"确定要从远程仓库删除 {module.ModuleId}@{version} 吗？\n\n此操作不可撤销！", "删除", "取消", DialogIconType.Warning))
                return;

            _isLoading = true;
            _statusMessage = "正在删除...";
            Repaint();

            try
            {
                var publisher = new ModulePublisher();
                var success = await publisher.DeleteVersionAsync(registry, module.ModuleId, version, s =>
                {
                    _statusMessage = s;
                    Repaint();
                });
                if (success)
                {
                    _statusMessage = "删除成功";
                    RefreshModulesAsync(true).Forget();
                }
                else
                {
                    _statusMessage = "删除失败";
                }
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

        private void ExportPackage(HubModuleInfo module)
        {
            if (module == null || !module.IsInstalled) return;

            var modulePath = System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{module.ModuleId}");
            if (!System.IO.Directory.Exists(modulePath))
            {
                EditorDialog.DisplayAlertDialog("导出失败", $"模块目录不存在: {module.ModuleId}", "确定", DialogIconType.Error);
                return;
            }

            var defaultName = $"{module.ModuleId}_{module.InstalledVersion ?? "1.0.0"}.pd";
            var savePath = EditorUtility.SaveFilePanel("导出模块包", "", defaultName, "pd");
            if (string.IsNullOrEmpty(savePath)) return;

            try
            {
                // 创建临时目录
                var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PuffinExport_{Guid.NewGuid():N}");
                System.IO.Directory.CreateDirectory(tempDir);

                // 复制模块文件
                CopyDirectory(modulePath, System.IO.Path.Combine(tempDir, module.ModuleId));

                // 创建 zip
                if (System.IO.File.Exists(savePath))
                    System.IO.File.Delete(savePath);
                System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, savePath);

                // 清理临时目录
                System.IO.Directory.Delete(tempDir, true);

                EditorDialog.DisplayAlertDialog("导出成功", $"模块已导出到:\n{savePath}", "确定", DialogIconType.Info);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 导出失败: {e}");
                EditorDialog.DisplayAlertDialog("导出失败", e.Message, "确定", DialogIconType.Error);
            }
        }

        private void ImportPackage()
        {
            var openPath = EditorUtility.OpenFilePanel("导入模块包", "", "pd");
            if (string.IsNullOrEmpty(openPath)) return;

            try
            {
                // 创建临时目录解压
                var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PuffinImport_{Guid.NewGuid():N}");
                System.IO.Compression.ZipFile.ExtractToDirectory(openPath, tempDir);

                // 查找模块目录
                var dirs = System.IO.Directory.GetDirectories(tempDir);
                if (dirs.Length == 0)
                {
                    System.IO.Directory.Delete(tempDir, true);
                    EditorDialog.DisplayAlertDialog("导入失败", "包中没有找到模块目录", "确定", DialogIconType.Error);
                    return;
                }

                var moduleDir = dirs[0];
                var moduleId = System.IO.Path.GetFileName(moduleDir);
                var targetPath = System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");

                // 检查冲突
                if (System.IO.Directory.Exists(targetPath))
                {
                    var choice = EditorUtility.DisplayDialogComplex("模块已存在",
                        $"模块 {moduleId} 已存在，是否覆盖？",
                        "覆盖", "取消", "保留两者");

                    if (choice == 1) // 取消
                    {
                        System.IO.Directory.Delete(tempDir, true);
                        return;
                    }
                    if (choice == 2) // 保留两者
                    {
                        var i = 1;
                        while (System.IO.Directory.Exists(targetPath + $"_{i}")) i++;
                        moduleId = $"{moduleId}_{i}";
                        targetPath = System.IO.Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
                    }
                    else // 覆盖
                    {
                        System.IO.Directory.Delete(targetPath, true);
                    }
                }

                // 确保父目录存在
                var parentDir = System.IO.Path.GetDirectoryName(targetPath);
                if (!System.IO.Directory.Exists(parentDir))
                    System.IO.Directory.CreateDirectory(parentDir);

                // 复制模块（支持跨卷）
                CopyDirectory(moduleDir, targetPath);

                // 清理临时目录
                System.IO.Directory.Delete(tempDir, true);

                AssetDatabase.Refresh();
                RefreshModulesAsync().Forget();

                EditorDialog.DisplayAlertDialog("导入成功", $"模块 {moduleId} 已导入", "确定", DialogIconType.Info);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 导入失败: {e}");
                EditorDialog.DisplayAlertDialog("导入失败", e.Message, "确定", DialogIconType.Error);
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            System.IO.Directory.CreateDirectory(destDir);

            foreach (var file in System.IO.Directory.GetFiles(sourceDir))
            {
                var destFile = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));
                System.IO.File.Copy(file, destFile, true);
            }

            foreach (var dir in System.IO.Directory.GetDirectories(sourceDir))
            {
                var destSubDir = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        /// <summary>
        /// 扫描环境依赖冲突
        /// </summary>
        private void ScanEnvConflicts()
        {
            _envConflicts.Clear();
            var allEnvDeps = new Dictionary<string, List<(string moduleId, EnvironmentDependency env)>>();

            foreach (var module in _installedModules)
            {
                var envDeps = module.Manifest?.envDependencies;
                if (envDeps == null) continue;

                foreach (var env in envDeps)
                {
                    if (!allEnvDeps.ContainsKey(env.id))
                        allEnvDeps[env.id] = new List<(string, EnvironmentDependency)>();
                    allEnvDeps[env.id].Add((module.ModuleId, env));
                }
            }

            // 检测冲突
            foreach (var kvp in allEnvDeps)
            {
                if (kvp.Value.Count <= 1) continue;
                var first = kvp.Value[0].env;
                foreach (var item in kvp.Value.Skip(1))
                {
                    if (HasEnvConfigConflict(first, item.env))
                    {
                        _envConflicts[kvp.Key] = kvp.Value;
                        break;
                    }
                }
            }
        }

        private bool HasEnvConfigConflict(EnvironmentDependency a, EnvironmentDependency b)
        {
            if (a.source != b.source) return true;
            if (a.type != b.type) return true;
            if (!string.IsNullOrEmpty(a.version) && !string.IsNullOrEmpty(b.version) && a.version != b.version) return true;
            if (!string.IsNullOrEmpty(a.url) && !string.IsNullOrEmpty(b.url) && a.url != b.url) return true;
            return false;
        }

        /// <summary>
        /// 版本比较器（语义化版本排序）
        /// </summary>
        private class VersionComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                var partsX = x?.Split('.') ?? Array.Empty<string>();
                var partsY = y?.Split('.') ?? Array.Empty<string>();
                var maxLen = Math.Max(partsX.Length, partsY.Length);

                for (var i = 0; i < maxLen; i++)
                {
                    var px = i < partsX.Length && int.TryParse(partsX[i], out var nx) ? nx : 0;
                    var py = i < partsY.Length && int.TryParse(partsY[i], out var ny) ? ny : 0;
                    if (px != py) return px.CompareTo(py);
                }
                return 0;
            }
        }
    }
}
#endif