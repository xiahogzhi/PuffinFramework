using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Hub;
using Puffin.Editor.Hub.Data;
using Puffin.Editor.Hub.Services;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.UI
{
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

        private List<RegistrySource> _registriesWithToken = new();

        private void RefreshRegistryList()
        {
            _registriesWithToken = HubSettings.Instance.GetRegistriesWithToken();
            _registryNames = new string[_registriesWithToken.Count];
            for (int i = 0; i < _registriesWithToken.Count; i++)
                _registryNames[i] = _registriesWithToken[i].name;
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
            var selectedRegistry = _registriesWithToken[_selectedRegistryIndex];
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
                var registry = _registriesWithToken[_selectedRegistryIndex];

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

            var registry = _registriesWithToken[_selectedRegistryIndex];
            var success = await _publisher.UploadToGitHubAsync(_packagePath, _validation.Manifest, registry, s => { _uploadStatus = s; Repaint(); });

            _isUploading = false;
            _uploadStatus = success ? "✓ 上传成功!" : "✗ 上传失败，请查看控制台";
            Repaint();

            // 上传成功后刷新 Hub 窗口
            if (success)
            {
                // 更新上传模块本身的锁定文件，标记为远程模块
                var lockEntry = new InstalledModuleLock
                {
                    moduleId = _validation.Manifest.moduleId,
                    version = _validation.Manifest.version,
                    registryId = registry.id,
                    checksum = _validation.Manifest.checksum,
                    installedAt = System.DateTime.Now.ToString("o"),
                    resolvedDependencies = _validation.Manifest.moduleDependencies?.ConvertAll(d => d.moduleId) ?? new System.Collections.Generic.List<string>()
                };
                InstalledModulesLock.Instance.AddOrUpdate(lockEntry);
                Debug.Log($"[Hub] 模块 {_validation.Manifest.moduleId} 已标记为来自 {registry.name}");

                // 更新依赖此模块的其他本地模块的依赖信息
                UpdateDependentModulesRegistryInfo(_validation.Manifest.moduleId, registry);

                var hubWindow = GetWindow<ModuleHubWindow>(false, null, false);
                if (hubWindow != null)
                    hubWindow.RefreshAfterPublish();
            }
        }

        /// <summary>
        /// 更新依赖此模块的其他本地模块的依赖信息
        /// </summary>
        private void UpdateDependentModulesRegistryInfo(string uploadedModuleId, RegistrySource registry)
        {
            var modulesPath = ManifestService.GetModulesPath();
            if (!System.IO.Directory.Exists(modulesPath)) return;

            foreach (var moduleDir in System.IO.Directory.GetDirectories(modulesPath))
            {
                var manifestPath = ManifestService.GetManifestPathFromDir(moduleDir);
                var manifest = ManifestService.Load(manifestPath);
                if (manifest?.moduleDependencies == null) continue;

                try
                {

                    var changed = false;
                    foreach (var dep in manifest.moduleDependencies)
                    {
                        if (dep.moduleId == uploadedModuleId && string.IsNullOrEmpty(dep.registryId))
                        {
                            dep.registryId = registry.id;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        ManifestService.Save(manifestPath, manifest);
                        Debug.Log($"[Hub] 已更新模块 {manifest.moduleId} 的依赖信息");
                    }
                }
                catch { }
            }
        }

        private async UniTaskVoid PackageAsync()
        {
            _packagePath = await _publisher.PackageModuleAsync(_modulePath, null, _validation?.Manifest);
            Repaint();
        }
    }
}