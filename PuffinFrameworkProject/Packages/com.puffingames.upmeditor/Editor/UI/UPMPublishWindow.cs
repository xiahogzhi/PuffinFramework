#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PuffinGames.UPMEditor
{
    /// <summary>
    /// UPM 包发布窗口
    /// </summary>
    public class UPMPublishWindow : EditorWindow
    {
        private string _packagePath = "";
        private string _registry;
        private UPMPackageData _packageData;
        private Vector2 _scrollPosition;

        // Unity Signing
        private string _unityUsername;
        private string _unityPassword;
        private string _cloudOrgId;
        private bool _foldUnitySigning = true;

        [MenuItem(UPMConstants.ToolsMenuRoot + "发布 UPM")]
        public static void ShowWindow()
        {
            var window = GetWindow<UPMPublishWindow>("发布 UPM");
            window.minSize = new Vector2(400, 350);
            window.AutoDetectPackage();
        }

        /// <summary>
        /// 打开发布窗口并设置包路径
        /// </summary>
        public static void ShowWindow(string packagePath)
        {
            var window = GetWindow<UPMPublishWindow>("发布 UPM");
            window.minSize = new Vector2(400, 350);
            window._packagePath = packagePath;
            window.LoadPackageData();
        }

        private void OnEnable()
        {
            _registry = PublishService.GetRegistry();
            _unityUsername = PublishService.GetUnityUsername();
            _cloudOrgId = PublishService.GetCloudOrgId();
            AutoDetectPackage();
        }

        /// <summary>
        /// 自动检测当前选中的目录是否是UPM目录
        /// </summary>
        private void AutoDetectPackage()
        {
            // 如果已有路径且有效，不重新检测
            if (!string.IsNullOrEmpty(_packagePath) && _packageData != null) return;

            // 检测当前选中的目录
            if (Selection.activeObject != null)
            {
                var path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path))
                {
                    // 检查是否是文件夹
                    if (AssetDatabase.IsValidFolder(path) || System.IO.Directory.Exists(System.IO.Path.GetFullPath(path)))
                    {
                        if (UPMPackageValidator.HasValidPackageJson(path))
                        {
                            _packagePath = path;
                            LoadPackageData();
                            return;
                        }
                    }
                }
            }
        }

        private void OnSelectionChange()
        {
            // 选择变化时自动检测
            if (Selection.activeObject != null)
            {
                var path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path))
                {
                    if (AssetDatabase.IsValidFolder(path) || System.IO.Directory.Exists(System.IO.Path.GetFullPath(path)))
                    {
                        if (UPMPackageValidator.HasValidPackageJson(path))
                        {
                            _packagePath = path;
                            LoadPackageData();
                            Repaint();
                        }
                    }
                }
            }
        }

        private void LoadPackageData()
        {
            if (string.IsNullOrEmpty(_packagePath)) return;
            _packageData = PackageJsonService.ReadPackageJson(_packagePath);
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UPM 包发布", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawPackageSelection();
            EditorGUILayout.Space(10);

            if (_packageData != null)
            {
                DrawPackageInfo();
                EditorGUILayout.Space(10);
                DrawUnitySigning();
                EditorGUILayout.Space(10);
                DrawRegistrySettings();
                EditorGUILayout.Space(10);
                DrawActions();
            }

            EditorGUILayout.Space(10);
            DrawHelp();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPackageSelection()
        {
            EditorGUILayout.LabelField("包路径", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _packagePath = EditorGUILayout.TextField(
                new GUIContent("路径", "选择要发布的 UPM 包目录"),
                _packagePath);

            if (GUILayout.Button(new GUIContent("...", "浏览选择包目录"), GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("选择 UPM 包", "Packages", "");
                if (!string.IsNullOrEmpty(path))
                {
                    path = ConvertToRelativePath(path);
                    _packagePath = path;
                    LoadPackageData();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_packagePath) && _packageData == null)
            {
                EditorGUILayout.HelpBox("未找到 package.json 文件", MessageType.Warning);
            }
        }

        private void DrawPackageInfo()
        {
            EditorGUILayout.LabelField("包信息", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("名称", _packageData.name);
            EditorGUILayout.LabelField("显示名称", _packageData.displayName);
            EditorGUILayout.LabelField("版本", _packageData.version);
            EditorGUILayout.LabelField("Unity 版本", _packageData.unity);
            EditorGUILayout.EndVertical();
        }

        private void DrawUnitySigning()
        {
            _foldUnitySigning = EditorGUILayout.BeginFoldoutHeaderGroup(_foldUnitySigning, "Unity 签名打包 (Unity 6.3+)");
            if (_foldUnitySigning)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                _unityUsername = EditorGUILayout.TextField(
                    new GUIContent("Unity ID 邮箱", "Unity 账号邮箱"),
                    _unityUsername);
                if (EditorGUI.EndChangeCheck())
                {
                    PublishService.SetUnityUsername(_unityUsername);
                }

                _unityPassword = EditorGUILayout.PasswordField(
                    new GUIContent("密码", "Unity 账号密码（不会保存）"),
                    _unityPassword);

                EditorGUI.BeginChangeCheck();
                _cloudOrgId = EditorGUILayout.TextField(
                    new GUIContent("Organization ID", "Unity Cloud Organization ID"),
                    _cloudOrgId);
                if (EditorGUI.EndChangeCheck())
                {
                    PublishService.SetCloudOrgId(_cloudOrgId);
                }

                EditorGUILayout.Space(5);

                var canSign = _packageData != null &&
                              !string.IsNullOrEmpty(_unityUsername) &&
                              !string.IsNullOrEmpty(_unityPassword) &&
                              !string.IsNullOrEmpty(_cloudOrgId);

                GUI.enabled = canSign;
                if (GUILayout.Button(new GUIContent("打包签名包 (.tgz)", "使用 Unity -upmPack 命令打包带签名的 tgz"), GUILayout.Height(28)))
                {
                    PackWithSignature();
                }

                if (GUILayout.Button(new GUIContent("打包签名并发布", "打包签名后发布到 Registry"), GUILayout.Height(28)))
                {
                    PackAndPublishWithSignature();
                }
                GUI.enabled = true;

                EditorGUILayout.HelpBox(
                    "Unity 6.3+ 要求包必须有签名才能安装。\n" +
                    "Organization ID 可在 Unity Cloud Dashboard 获取。",
                    MessageType.Info);

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRegistrySettings()
        {
            EditorGUILayout.LabelField("Registry 设置", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _registry = EditorGUILayout.TextField(
                new GUIContent("Registry URL", "npm registry 地址，如 Verdaccio 或 GitHub Packages"),
                _registry);
            if (EditorGUI.EndChangeCheck())
            {
                PublishService.SetRegistry(_registry);
            }

            if (!PublishService.IsNpmAvailable())
            {
                EditorGUILayout.HelpBox("未找到 npm，请安装 Node.js 并重启 Unity", MessageType.Error);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

            GUI.enabled = PublishService.IsNpmAvailable() && _packageData != null;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Pack
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("打包 (.tgz)", "使用 npm pack 打包成 tgz 文件"), GUILayout.Height(28)))
            {
                PackPackage();
            }
            EditorGUILayout.EndHorizontal();

            // Publish
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("发布到 Registry", "使用 npm publish 发布到指定 registry"), GUILayout.Height(28)))
            {
                PublishPackage();
            }
            EditorGUILayout.EndHorizontal();

            // Unpublish
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("撤销发布此版本", "从 registry 删除此版本"), GUILayout.Height(25)))
            {
                UnpublishPackage();
            }
            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;

            EditorGUILayout.Space(5);

            // Copy config
            if (GUILayout.Button(new GUIContent("复制 Registry 配置", "复制 scopedRegistries 配置到剪贴板"), GUILayout.Height(25)))
            {
                CopyRegistryConfig();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHelp()
        {
            EditorGUILayout.HelpBox(
                "使用说明:\n" +
                "• npm 发布: npm adduser --registry <url>\n" +
                "• Unity 签名: 需要 Unity 6.3+ 和 Organization ID\n" +
                "• 发布前请确保 package.json 中的版本号已更新",
                MessageType.Info);
        }

        private void PackWithSignature()
        {
            var outputPath = EditorUtility.SaveFolderPanel("选择输出目录", _packagePath, "");
            if (string.IsNullOrEmpty(outputPath)) return;

            EditorUtility.DisplayProgressBar("打包签名包", "正在调用 Unity 打包...", 0.5f);

            try
            {
                var result = PublishService.PackWithSignature(_packagePath, outputPath, _unityUsername, _unityPassword, _cloudOrgId);
                EditorUtility.ClearProgressBar();

                if (result.Success)
                {
                    EditorUtility.DisplayDialog("成功", $"签名包已生成:\n{result.TgzPath}", "确定");
                    EditorUtility.RevealInFinder(result.TgzPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", result.ErrorMessage, "确定");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void PackAndPublishWithSignature()
        {
            if (!EditorUtility.DisplayDialog("发布确认",
                $"打包签名并发布 {_packageData.name}@{_packageData.version} 到:\n{_registry}",
                "发布", "取消"))
            {
                return;
            }

            // 使用临时目录
            var tempDir = Path.Combine(Path.GetTempPath(), "UPMEditor_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);

            EditorUtility.DisplayProgressBar("打包签名包", "正在调用 Unity 打包...", 0.3f);

            try
            {
                var packResult = PublishService.PackWithSignature(_packagePath, tempDir, _unityUsername, _unityPassword, _cloudOrgId);

                if (!packResult.Success)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("打包失败", packResult.ErrorMessage, "确定");
                    return;
                }

                EditorUtility.DisplayProgressBar("发布", "正在发布到 Registry...", 0.7f);

                var publishResult = PublishService.PublishTgz(packResult.TgzPath, _registry);

                EditorUtility.ClearProgressBar();

                if (publishResult.Success)
                {
                    EditorUtility.DisplayDialog("成功", $"签名包已发布到 {_registry}", "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("发布失败", publishResult.ErrorMessage, "确定");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                // 清理临时目录
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        private void PackPackage()
        {
            var outputPath = EditorUtility.SaveFolderPanel("选择输出目录", _packagePath, "");
            if (string.IsNullOrEmpty(outputPath)) return;

            var result = PublishService.Pack(_packagePath, outputPath);
            if (result.Success)
            {
                EditorUtility.DisplayDialog("成功", $"包已打包到:\n{result.TgzPath}", "确定");
                EditorUtility.RevealInFinder(result.TgzPath);
            }
            else
            {
                EditorUtility.DisplayDialog("错误", result.ErrorMessage, "确定");
            }
        }

        private void PublishPackage()
        {
            if (!EditorUtility.DisplayDialog("发布确认",
                $"发布 {_packageData.name}@{_packageData.version} 到:\n{_registry}",
                "发布", "取消"))
            {
                return;
            }

            var result = PublishService.Publish(_packagePath, _registry);
            if (result.Success)
            {
                EditorUtility.DisplayDialog("成功", $"包已发布到 {_registry}", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("错误", result.ErrorMessage, "确定");
            }
        }

        private void UnpublishPackage()
        {
            if (!EditorUtility.DisplayDialog("撤销发布确认",
                $"从 {_registry} 删除:\n{_packageData.name}@{_packageData.version}\n\n此操作不可撤销!",
                "撤销发布", "取消"))
            {
                return;
            }

            var result = PublishService.Unpublish(_packageData.name, _packageData.version, _registry);
            if (result.Success)
            {
                EditorUtility.DisplayDialog("成功", "包已撤销发布", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("错误", result.ErrorMessage, "确定");
            }
        }

        private void CopyRegistryConfig()
        {
            var config = PublishService.GenerateScopedRegistryConfig(_packageData.name, _registry);
            GUIUtility.systemCopyBuffer = config;
            Debug.Log("Registry 配置已复制:\n" + config);
            EditorUtility.DisplayDialog("已复制", "scopedRegistries 配置已复制到剪贴板\n请添加到 Packages/manifest.json", "确定");
        }

        private string ConvertToRelativePath(string absolutePath)
        {
            var dataPath = Application.dataPath.Replace("\\", "/");
            var projectPath = Path.GetDirectoryName(dataPath).Replace("\\", "/");
            absolutePath = absolutePath.Replace("\\", "/");
            if (absolutePath.StartsWith(projectPath))
            {
                return absolutePath.Substring(projectPath.Length + 1);
            }
            return absolutePath;
        }
    }
}
#endif
