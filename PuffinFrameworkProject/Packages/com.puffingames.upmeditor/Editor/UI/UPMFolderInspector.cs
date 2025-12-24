#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PuffinGames.UPMEditor
{
    /// <summary>
    /// UPM 包目录的 Inspector 扩展
    /// 当选中 Assets 或本地 Packages 下的 UPM 目录时显示编辑界面
    /// </summary>
    [CustomEditor(typeof(DefaultAsset))]
    public class UPMFolderInspector : Editor
    {
        private UPMPackageData _packageData;
        private string _folderPath;
        private bool _isUPMFolder;
        private bool _isEditable;

        // Foldout states
        private bool _foldBasicInfo = true;
        private bool _foldAuthor = false;
        private bool _foldDependencies = false;
        private bool _foldKeywords = false;
        private bool _foldFiles = false;

        // Editing
        private string _newDepName = "";
        private string _newDepVersion = "1.0.0";
        private string _newKeyword = "";

        // File lists
        private List<string> _existingDirs = new List<string>();
        private List<string> _existingFiles = new List<string>();

        private void OnEnable()
        {
            CheckFolder();
        }

        private void CheckFolder()
        {
            _isUPMFolder = false;
            _isEditable = false;
            _packageData = null;

            if (target == null) return;

            _folderPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(_folderPath)) return;

            // 检查是否是文件夹
            if (!AssetDatabase.IsValidFolder(_folderPath))
            {
                // 检查 Packages 目录
                if (_folderPath.StartsWith("Packages/"))
                {
                    var fullPath = Path.GetFullPath(_folderPath);
                    if (!Directory.Exists(fullPath)) return;
                }
                else
                {
                    return;
                }
            }

            // 检查是否有 package.json
            if (!UPMPackageValidator.HasValidPackageJson(_folderPath)) return;

            _isUPMFolder = true;

            // 检查是否可编辑（在 Assets 或本地 Packages 下）
            _isEditable = UPMPackageValidator.IsInAssetsFolder(_folderPath) ||
                          UPMPackageValidator.IsLocalPackage(_folderPath);

            // 加载包数据
            _packageData = PackageJsonService.ReadPackageJson(_folderPath);

            // 刷新文件列表
            RefreshFileList();
        }

        private void RefreshFileList()
        {
            _existingDirs.Clear();
            _existingFiles.Clear();

            if (string.IsNullOrEmpty(_folderPath)) return;

            var fullPath = Path.GetFullPath(_folderPath);
            if (!Directory.Exists(fullPath)) return;

            foreach (var dir in Directory.GetDirectories(fullPath))
            {
                var name = Path.GetFileName(dir);
                if (!name.StartsWith("."))
                    _existingDirs.Add(name);
            }

            foreach (var file in Directory.GetFiles(fullPath))
            {
                var name = Path.GetFileName(file);
                if (!name.EndsWith(".meta") && name != "package.json")
                    _existingFiles.Add(name);
            }
        }

        public override void OnInspectorGUI()
        {
            // 如果不是 UPM 目录，显示默认 Inspector
            if (!_isUPMFolder || _packageData == null)
            {
                DrawDefaultInspector();
                return;
            }

            // 显示 UPM 编辑界面
            EditorGUILayout.Space(5);

            // 标题
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("UPM 包", EditorStyles.boldLabel);
            if (_isEditable)
            {
                GUI.enabled = true;
                if (GUILayout.Button(new GUIContent("发布", "打开发布窗口"), GUILayout.Width(50)))
                {
                    UPMPublishWindow.ShowWindow(_folderPath);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!_isEditable)
            {
                EditorGUILayout.HelpBox("此包不可编辑（非本地包）", MessageType.Info);
            }

            GUI.enabled = _isEditable;

            DrawBasicInfo();
            DrawAuthor();
            DrawDependencies();
            DrawKeywords();

            if (_isEditable)
            {
                DrawFiles();
            }

            GUI.enabled = true;

            EditorGUILayout.Space(10);

            // 保存按钮
            if (_isEditable)
            {
                if (GUILayout.Button("保存", GUILayout.Height(25)))
                {
                    SavePackage();
                }
            }
        }

        private void DrawBasicInfo()
        {
            _foldBasicInfo = EditorGUILayout.BeginFoldoutHeaderGroup(_foldBasicInfo, "基本信息");
            if (_foldBasicInfo)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                GUI.enabled = false;
                EditorGUILayout.TextField(new GUIContent("包名", "UPM 包名"), _packageData.name);
                GUI.enabled = _isEditable;

                _packageData.displayName = EditorGUILayout.TextField(
                    new GUIContent("显示名称", "在 Package Manager 中显示的名称"),
                    _packageData.displayName);

                _packageData.version = EditorGUILayout.TextField(
                    new GUIContent("版本", "语义化版本号"),
                    _packageData.version);

                _packageData.unity = EditorGUILayout.TextField(
                    new GUIContent("Unity 版本", "最低支持的 Unity 版本"),
                    _packageData.unity);

                EditorGUILayout.LabelField(new GUIContent("描述", "包的详细描述"));
                _packageData.description = EditorGUILayout.TextArea(_packageData.description, GUILayout.MinHeight(200));

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawAuthor()
        {
            _foldAuthor = EditorGUILayout.BeginFoldoutHeaderGroup(_foldAuthor, "作者信息");
            if (_foldAuthor)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                _packageData.author.name = EditorGUILayout.TextField("姓名", _packageData.author.name);
                _packageData.author.email = EditorGUILayout.TextField("邮箱", _packageData.author.email);
                _packageData.author.url = EditorGUILayout.TextField("网址", _packageData.author.url);

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDependencies()
        {
            _foldDependencies = EditorGUILayout.BeginFoldoutHeaderGroup(_foldDependencies,
                $"依赖项 ({_packageData.dependencies.Count})");
            if (_foldDependencies)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                var toRemove = new List<string>();
                foreach (var dep in _packageData.dependencies)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(dep.Key, GUILayout.MinWidth(100));
                    EditorGUILayout.LabelField(dep.Value, GUILayout.Width(50));
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                        toRemove.Add(dep.Key);
                    EditorGUILayout.EndHorizontal();
                }
                foreach (var key in toRemove)
                    _packageData.dependencies.Remove(key);

                EditorGUILayout.Space(3);
                EditorGUILayout.BeginHorizontal();
                _newDepName = EditorGUILayout.TextField(_newDepName, GUILayout.MinWidth(100));
                _newDepVersion = EditorGUILayout.TextField(_newDepVersion, GUILayout.Width(50));
                GUI.enabled = _isEditable && !string.IsNullOrEmpty(_newDepName) && !_packageData.dependencies.ContainsKey(_newDepName);
                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    _packageData.dependencies[_newDepName] = _newDepVersion;
                    _newDepName = "";
                    _newDepVersion = "1.0.0";
                }
                GUI.enabled = _isEditable;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawKeywords()
        {
            _foldKeywords = EditorGUILayout.BeginFoldoutHeaderGroup(_foldKeywords,
                $"关键词 ({_packageData.keywords.Count})");
            if (_foldKeywords)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                var toRemoveIdx = -1;
                for (int i = 0; i < _packageData.keywords.Count; i++)
                {
                    if (GUILayout.Button($"{_packageData.keywords[i]} ×", EditorStyles.miniButton))
                        toRemoveIdx = i;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                if (toRemoveIdx >= 0)
                    _packageData.keywords.RemoveAt(toRemoveIdx);

                EditorGUILayout.BeginHorizontal();
                _newKeyword = EditorGUILayout.TextField(_newKeyword);
                GUI.enabled = _isEditable && !string.IsNullOrEmpty(_newKeyword) && !_packageData.keywords.Contains(_newKeyword);
                if (GUILayout.Button("添加", GUILayout.Width(40)))
                {
                    _packageData.keywords.Add(_newKeyword);
                    _newKeyword = "";
                }
                GUI.enabled = _isEditable;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawFiles()
        {
            _foldFiles = EditorGUILayout.BeginFoldoutHeaderGroup(_foldFiles, "目录与文件");
            if (_foldFiles)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 目录
                EditorGUILayout.LabelField("目录", EditorStyles.miniBoldLabel);
                foreach (var dir in _existingDirs)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"📁 {dir}");
                    if (GUILayout.Button("删除", GUILayout.Width(40)))
                    {
                        if (EditorUtility.DisplayDialog("确认", $"删除目录 {dir}？", "删除", "取消"))
                        {
                            DeleteDirectory(dir);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Runtime", EditorStyles.miniButton))
                    CreateDirectory("Runtime");
                if (GUILayout.Button("+ Editor", EditorStyles.miniButton))
                    CreateDirectory("Editor");
                if (GUILayout.Button("+ Tests", EditorStyles.miniButton))
                    CreateDirectory("Tests");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                // 文件
                EditorGUILayout.LabelField("文件", EditorStyles.miniBoldLabel);
                foreach (var file in _existingFiles)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"📄 {file}");
                    if (GUILayout.Button("删除", GUILayout.Width(40)))
                    {
                        if (EditorUtility.DisplayDialog("确认", $"删除文件 {file}？", "删除", "取消"))
                        {
                            DeleteFile(file);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ README", EditorStyles.miniButton))
                    CreateFile("README.md");
                if (GUILayout.Button("+ CHANGELOG", EditorStyles.miniButton))
                    CreateFile("CHANGELOG.md");
                if (GUILayout.Button("+ LICENSE", EditorStyles.miniButton))
                    CreateFile("LICENSE.md");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void SavePackage()
        {
            var result = UPMPackageValidator.ValidatePackageData(_packageData);
            if (!result.IsValid)
            {
                EditorUtility.DisplayDialog("验证错误", string.Join("\n", result.Errors), "确定");
                return;
            }

            try
            {
                PackageJsonService.WritePackageJson(_folderPath, _packageData);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("成功", "保存成功", "确定");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", e.Message, "确定");
            }
        }

        private void CreateDirectory(string dirName)
        {
            var fullPath = Path.Combine(Path.GetFullPath(_folderPath), dirName);
            if (Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("提示", $"目录 {dirName} 已存在", "确定");
                return;
            }

            try
            {
                if (dirName == "Runtime")
                    AsmdefGeneratorService.CreateRuntimeAsmdef(_folderPath, _packageData.name);
                else if (dirName == "Editor")
                    AsmdefGeneratorService.CreateEditorAsmdef(_folderPath, _packageData.name);
                else if (dirName == "Tests")
                {
                    AsmdefGeneratorService.CreateTestsAsmdef(_folderPath, _packageData.name, false);
                    AsmdefGeneratorService.CreateTestsAsmdef(_folderPath, _packageData.name, true);
                }
                else
                    Directory.CreateDirectory(fullPath);

                AssetDatabase.Refresh();
                RefreshFileList();
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", e.Message, "确定");
            }
        }

        private void CreateFile(string fileName)
        {
            var fullPath = Path.Combine(Path.GetFullPath(_folderPath), fileName);
            if (File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("提示", $"文件 {fileName} 已存在", "确定");
                return;
            }

            try
            {
                if (fileName == "README.md")
                    AsmdefGeneratorService.CreateReadme(_folderPath, _packageData);
                else if (fileName == "CHANGELOG.md")
                    AsmdefGeneratorService.CreateChangelog(_folderPath, _packageData);
                else if (fileName == "LICENSE.md")
                    AsmdefGeneratorService.CreateLicense(_folderPath, _packageData);

                AssetDatabase.Refresh();
                RefreshFileList();
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", e.Message, "确定");
            }
        }

        private void DeleteDirectory(string dirName)
        {
            var fullPath = Path.Combine(Path.GetFullPath(_folderPath), dirName);
            try
            {
                Directory.Delete(fullPath, true);
                var metaPath = fullPath + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
                AssetDatabase.Refresh();
                RefreshFileList();
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", e.Message, "确定");
            }
        }

        private void DeleteFile(string fileName)
        {
            var fullPath = Path.Combine(Path.GetFullPath(_folderPath), fileName);
            try
            {
                File.Delete(fullPath);
                var metaPath = fullPath + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
                AssetDatabase.Refresh();
                RefreshFileList();
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", e.Message, "确定");
            }
        }
    }
}
#endif
