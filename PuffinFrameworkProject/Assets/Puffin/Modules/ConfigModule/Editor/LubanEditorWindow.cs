#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Puffin.Modules.ConfigModule.Editor
{
    public class LubanEditorWindow : EditorWindow
    {
        private static string SettingsJsonPath => Path.Combine(Application.dataPath, "Puffin/Modules/ConfigModule/Editor/settings.json");
        private static string LubanDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/Luban"));
        private static string SevenZipDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/7z"));
        private static string LubanExe => Path.Combine(LubanDir, "Luban", "Luban.dll");
        private static string SevenZipExe => Path.Combine(SevenZipDir, "7zr.exe");
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private bool _isInstalled;
        private bool _is7zInstalled;
        private bool _hasTemplate;
        private bool _isProcessing;
        private string _status = "";
        private float _downloadProgress;
        private long _downloadedBytes;
        private long _totalBytes;

        private Vector2 _scrollPos;
        private bool _showDirConfig = true;
        private bool _showGenConfig = true;
        private bool _showAdvConfig;
        private SerializedObject _settingsSO;
        private LubanEditorSettings _editorSettings;

        [Serializable]
        private class LubanEditorSettings
        {
            public string lubanVersion = "4.5.0";
            public string lubanDownloadUrl = "https://github.com/focus-creative-games/luban/releases/download/v{0}/Luban.7z";
            public string templateRepoUrl = "https://gitee.com/focus-creative-games/luban_examples.git";
            public string sevenZipDownloadUrl = "https://www.7-zip.org/a/7zr.exe";
        }

        [MenuItem("Puffin Framework/Config/Luban Editor")]
        public static void ShowWindow() => GetWindow<LubanEditorWindow>("Luban Editor");

        private void OnEnable()
        {
            LoadEditorSettings();
            CheckInstallation();
            _settingsSO = new SerializedObject(LubanSettings.Instance);
        }

        private void LoadEditorSettings()
        {
            _editorSettings = new LubanEditorSettings();
            if (File.Exists(SettingsJsonPath))
            {
                try { _editorSettings = JsonUtility.FromJson<LubanEditorSettings>(File.ReadAllText(SettingsJsonPath)); }
                catch { /* use defaults */ }
            }
        }

        private void CheckInstallation()
        {
            _isInstalled = File.Exists(LubanExe);
            _is7zInstalled = File.Exists(SevenZipExe) || Check7zInPath();
            _hasTemplate = Directory.Exists(GetLubanConfigDir()) && File.Exists(Path.Combine(GetLubanConfigDir(), "luban.conf"));
        }

        private static bool Check7zInPath()
        {
            var paths = new[] { "7z", "7za", "7zr", @"C:\Program Files\7-Zip\7z.exe", @"C:\Program Files (x86)\7-Zip\7z.exe" };
            foreach (var exe in paths)
                if (RunCommand(exe, "", out _)) return true;
            return false;
        }

        private void OnGUI()
        {
            var allReady = _is7zInstalled && _isInstalled && _hasTemplate;

            // 顶部工具栏（仅在全部就绪时显示）
            if (allReady)
                DrawToolbar();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // 未就绪时显示安装界面
            if (!allReady)
                DrawInstallSection();
            else
                DrawConfigSection();

            // 状态和进度
            DrawStatusSection();

            EditorGUILayout.EndScrollView();

            // 底部生成按钮（仅在全部就绪时显示）
            if (allReady)
                DrawBottomSection();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("打开 Datas 目录", EditorStyles.toolbarButton))
            {
                var datasDir = Path.Combine(GetLubanConfigDir(), LubanSettings.Instance.dataDir);
                if (!Directory.Exists(datasDir)) Directory.CreateDirectory(datasDir);
                OpenFolder(datasDir);
            }

            if (GUILayout.Button("打开 Luban 目录", EditorStyles.toolbarButton))
                OpenFolder(GetLubanConfigDir());

            if (GUILayout.Button("保存配置", EditorStyles.toolbarButton))
                SaveLubanConf();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("重新下载模板", EditorStyles.toolbarButton))
            {
                if (EditorUtility.DisplayDialog("确认", "将删除现有 Luban 配置目录并重新下载模板，是否继续？", "确定", "取消"))
                    CreateTemplate(true);
            }

            if (GUILayout.Button("卸载 Luban", EditorStyles.toolbarButton))
                UninstallLuban();

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
                CheckInstallation();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawInstallSection()
        {
            EditorGUILayout.Space(20);
            GUILayout.FlexibleSpace();

            // 状态显示
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawStatusLabel("7-Zip", _is7zInstalled);
            DrawStatusLabel("Luban", _isInstalled);
            DrawStatusLabel("模板", _hasTemplate);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            EditorGUI.BeginDisabledGroup(_isProcessing);

            // 居中显示安装按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (!_is7zInstalled)
            {
                if (GUILayout.Button("安装 7-Zip", GUILayout.Width(200), GUILayout.Height(40)))
                    _ = Install7zAsync();
            }
            else if (!_isInstalled)
            {
                if (GUILayout.Button($"安装 Luban v{_editorSettings.lubanVersion}", GUILayout.Width(200), GUILayout.Height(40)))
                    _ = InstallLubanAsync();
            }
            else if (!_hasTemplate)
            {
                if (GUILayout.Button("下载模板", GUILayout.Width(200), GUILayout.Height(40)))
                    CreateTemplate(false);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
        }

        private static void DrawStatusLabel(string label, bool ok)
        {
            var color = ok ? Color.green : Color.gray;
            var icon = ok ? "✓" : "○";
            var style = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
            GUILayout.Label($"{icon} {label}", style, GUILayout.Width(80));
        }

        private void DrawConfigSection()
        {
            _settingsSO.Update();

            EditorGUILayout.Space(5);
            _showDirConfig = EditorGUILayout.Foldout(_showDirConfig, "目录配置", true);
            if (_showDirConfig)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("dataDir"), new GUIContent("数据源目录"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("defineDir"), new GUIContent("Schema 目录"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("outputCodeDir"), new GUIContent("代码输出目录"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("outputDataDir"), new GUIContent("数据输出目录"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            _showGenConfig = EditorGUILayout.Foldout(_showGenConfig, "生成配置", true);
            if (_showGenConfig)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("codeTarget"), new GUIContent("代码目标"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("dataTarget"), new GUIContent("数据格式"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("topModule"), new GUIContent("命名空间"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("managerName"), new GUIContent("管理类名"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("defaultGroup"), new GUIContent("默认分组"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("inputDataType"), new GUIContent("数据源类型"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("enableAutoImport"), new GUIContent("启用自动导入 Table", "文件名以 # 开头的 excel 自动识别为表"));

            // 检测是否需要运行时
            if (LubanSettings.NeedsRuntime(LubanSettings.Instance.codeTarget))
            {
                var runtimeDir = Path.Combine(Application.dataPath, "Puffin/Modules/ConfigModule/Runtime/Luban");
                if (!Directory.Exists(runtimeDir) || Directory.GetFiles(runtimeDir, "*.cs").Length == 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox("cs-bin 格式需要 Luban 运行时库", MessageType.Warning);
                    if (GUILayout.Button("安装 Luban 运行时"))
                        InstallLubanRuntime();
                }
            }

            EditorGUILayout.Space(5);
            _showAdvConfig = EditorGUILayout.Foldout(_showAdvConfig, "高级配置", true);
            if (_showAdvConfig)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("timeZone"), new GUIContent("时区"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("customTemplateDir"), new GUIContent("自定义模板目录"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("extraArgs"), new GUIContent("额外参数"));
                EditorGUI.indentLevel--;
            }

            if (_settingsSO.ApplyModifiedProperties())
                LubanSettings.Instance.Save();
        }

        private void DrawStatusSection()
        {
            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            if (_isProcessing && _totalBytes > 0)
            {
                var rect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(rect, _downloadProgress, $"{_downloadedBytes / 1024f:F0} KB / {_totalBytes / 1024f:F0} KB ({_downloadProgress * 100:F0}%)");
            }
        }

        private void DrawBottomSection()
        {
            EditorGUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(_isProcessing);
            if (GUILayout.Button("生成配置", GUILayout.Height(40)))
                GenerateConfig();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(5);
        }

        private static string GetLubanConfigDir() => Path.Combine(ProjectRoot, "Luban");
        private static string LubanRuntimeDir => Path.Combine(Application.dataPath, "Puffin/Modules/ConfigModule/Runtime/Luban");

        private void InstallLubanRuntime()
        {
            _isProcessing = true;
            _status = "正在安装 Luban 运行时...";
            Repaint();

            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "luban_examples_" + Guid.NewGuid());
                if (!RunCommand("git", $"clone --depth 1 {_editorSettings.templateRepoUrl} \"{tempDir}\"", out var output))
                {
                    _status = "克隆失败";
                    Debug.LogError($"[Luban] Git clone 失败: {output}");
                    _isProcessing = false;
                    Repaint();
                    return;
                }

                var srcDir = Path.Combine(tempDir, "Projects", "Csharp_DotNet_bin", "LubanLib");
                if (Directory.Exists(srcDir))
                {
                    Directory.CreateDirectory(LubanRuntimeDir);
                    CopyDirectory(srcDir, LubanRuntimeDir);
                    _status = "Luban 运行时安装完成";
                    Debug.Log("[Luban] 运行时安装完成");
                    AssetDatabase.Refresh();
                }
                else
                {
                    _status = "未找到运行时库";
                }

                try { Directory.Delete(tempDir, true); } catch { /* ignore cleanup errors */ }
            }
            catch (Exception e)
            {
                _status = $"安装失败: {e.Message}";
                Debug.LogError($"[Luban] 运行时安装失败: {e}");
            }

            _isProcessing = false;
            Repaint();
        }

        private static void OpenFolder(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        private void SaveLubanConf()
        {
            var confDir = GetLubanConfigDir();
            if (!Directory.Exists(confDir)) Directory.CreateDirectory(confDir);

            var confPath = Path.Combine(confDir, "luban.conf");
            File.WriteAllText(confPath, LubanSettings.Instance.GenerateLubanConf());
            AssetDatabase.Refresh();
            _status = "luban.conf 已保存";
            Debug.Log($"[Luban] 配置已保存: {confPath}");
        }

        private void CreateTemplate(bool forceOverwrite)
        {
            var confDir = GetLubanConfigDir();
            if (!forceOverwrite && Directory.Exists(confDir) && Directory.GetFileSystemEntries(confDir).Length > 0)
            {
                if (!EditorUtility.DisplayDialog("确认", "Luban 目录已存在，是否覆盖？", "覆盖", "取消"))
                    return;
            }

            if (Directory.Exists(confDir))
                Directory.Delete(confDir, true);

            _ = DownloadMiniTemplateAsync(confDir);
        }

        private async Task DownloadMiniTemplateAsync(string destDir)
        {
            _isProcessing = true;
            _status = "正在克隆 MiniTemplate...";
            Repaint();

            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "luban_examples_" + Guid.NewGuid());

                if (!RunCommand("git", $"clone --depth 1 {_editorSettings.templateRepoUrl} \"{tempDir}\"", out var output))
                {
                    _status = "克隆失败，请确保已安装 Git";
                    Debug.LogError($"[Luban] Git clone 失败: {output}");
                    _isProcessing = false;
                    Repaint();
                    return;
                }

                await Task.Yield();

                var miniDir = Path.Combine(tempDir, "MiniTemplate");
                if (Directory.Exists(miniDir))
                {
                    CopyDirectory(miniDir, destDir);
                    var confPath = Path.Combine(destDir, "luban.conf");
                    File.WriteAllText(confPath, LubanSettings.Instance.GenerateLubanConf());
                    _status = "模板创建完成";
                    _hasTemplate = true;
                    Debug.Log("[Luban] MiniTemplate 安装完成");
                }
                else
                {
                    _status = "未找到 MiniTemplate";
                }

                Directory.Delete(tempDir, true);
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                _status = $"下载失败: {e.Message}";
                Debug.LogError($"[Luban] 下载失败: {e}");
            }

            _isProcessing = false;
            Repaint();
        }

        private static void CopyDirectory(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }

        private async Task Install7zAsync()
        {
            _isProcessing = true;
            _status = "正在下载 7-Zip...";
            _downloadProgress = 0;
            _downloadedBytes = 0;
            _totalBytes = 0;
            Repaint();

            try
            {
                Directory.CreateDirectory(SevenZipDir);
                var exePath = Path.Combine(SevenZipDir, "7zr.exe");

                await DownloadFileAsync(_editorSettings.sevenZipDownloadUrl, exePath);

                _status = "7-Zip 安装完成";
                _is7zInstalled = true;
                Debug.Log($"[7-Zip] 安装完成: {exePath}");
            }
            catch (Exception e)
            {
                _status = $"安装失败: {e.Message}";
                Debug.LogError($"[7-Zip] 安装失败: {e}");
            }

            _isProcessing = false;
            Repaint();
        }

        private async Task DownloadFileAsync(string url, string destPath)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                Proxy = WebRequest.GetSystemWebProxy(),
                UseProxy = true
            };
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Add("User-Agent", "Unity");

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            _totalBytes = response.Content.Headers.ContentLength ?? 0;
            _downloadedBytes = 0;
            Repaint();

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                _downloadedBytes += bytesRead;
                _downloadProgress = _totalBytes > 0 ? (float)_downloadedBytes / _totalBytes : 0;
                Repaint();
            }
        }

        private async Task InstallLubanAsync()
        {
            _isProcessing = true;
            _status = "正在下载 Luban...";
            _downloadProgress = 0;
            _downloadedBytes = 0;
            _totalBytes = 0;
            Repaint();

            try
            {
                Directory.CreateDirectory(LubanDir);
                var archivePath = Path.Combine(LubanDir, "Luban.7z");
                var url = string.Format(_editorSettings.lubanDownloadUrl, _editorSettings.lubanVersion);

                await DownloadFileAsync(url, archivePath);

                _status = "正在解压 (需要7z)...";
                Repaint();

                if (!Extract7z(archivePath, LubanDir))
                {
                    _status = "解压失败，请安装7-Zip并添加到PATH";
                    _isProcessing = false;
                    Repaint();
                    return;
                }

                File.Delete(archivePath);
                _status = "安装完成";
                _isInstalled = true;
                Debug.Log("[Luban] 安装完成");
            }
            catch (Exception e)
            {
                _status = $"安装失败: {e.Message}";
                Debug.LogError($"[Luban] 安装失败: {e}");
            }

            _isProcessing = false;
            Repaint();
        }

        private static bool Extract7z(string archivePath, string destDir)
        {
            var paths = new[] { SevenZipExe, "7z", "7za", "7zr", @"C:\Program Files\7-Zip\7z.exe", @"C:\Program Files (x86)\7-Zip\7z.exe" };
            foreach (var exe in paths)
                if (RunCommand(exe, $"x \"{archivePath}\" -o\"{destDir}\" -y", out _)) return true;
            return false;
        }

        private void UninstallLuban()
        {
            if (!EditorUtility.DisplayDialog("确认卸载", "确定要卸载 Luban 吗？这将删除 Tools/Luban 目录。", "确定", "取消"))
                return;

            try
            {
                if (Directory.Exists(LubanDir))
                    Directory.Delete(LubanDir, true);

                _isInstalled = false;
                _status = "已卸载";
                Debug.Log("[Luban] 已卸载");
            }
            catch (Exception e)
            {
                _status = $"卸载失败: {e.Message}";
                Debug.LogError($"[Luban] 卸载失败: {e}");
            }

            Repaint();
        }

        private void GenerateConfig()
        {
            _isProcessing = true;
            _status = "正在生成配置...";
            Repaint();

            try
            {
                var confDir = GetLubanConfigDir();
                var confPath = Path.Combine(confDir, "luban.conf");
                if (!File.Exists(confPath))
                {
                    _status = "未找到 luban.conf，请先保存配置";
                    _isProcessing = false;
                    Repaint();
                    return;
                }

                var settings = LubanSettings.Instance;
                var codeTarget = LubanSettings.GetCodeTargetString(settings.codeTarget);
                var dataTarget = LubanSettings.GetDataTargetString(settings.dataTarget);

                var codeDir = Path.GetFullPath(Path.Combine(confDir, settings.outputCodeDir));
                var dataDir = Path.GetFullPath(Path.Combine(confDir, settings.outputDataDir));
                Directory.CreateDirectory(codeDir);
                Directory.CreateDirectory(dataDir);

                var args = $"\"{LubanExe}\" --conf \"{confPath}\" -t client -c {codeTarget} -d {dataTarget} -x outputCodeDir=\"{codeDir}\" -x outputDataDir=\"{dataDir}\"";

                if (!string.IsNullOrEmpty(settings.timeZone))
                    args += $" --timeZone \"{settings.timeZone}\"";
                if (!string.IsNullOrEmpty(settings.customTemplateDir))
                    args += $" --customTemplateDir \"{settings.customTemplateDir}\"";
                if (!string.IsNullOrEmpty(settings.extraArgs))
                    args += $" {settings.extraArgs}";

                if (RunCommand("dotnet", args, out var output, confDir))
                {
                    GenerateConfigLoader(settings, codeDir);
                    _status = "生成完成";
                    Debug.Log($"[Luban] 生成完成\n{output}");
                    AssetDatabase.Refresh();
                }
                else
                {
                    _status = "生成失败";
                    Debug.LogError($"[Luban] 生成失败\n{output}");
                }
            }
            catch (Exception e)
            {
                _status = $"生成失败: {e.Message}";
                Debug.LogError($"[Luban] 生成失败: {e}");
            }

            _isProcessing = false;
            Repaint();
        }

        private static bool RunCommand(string fileName, string args, out string output, string workDir = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    WorkingDirectory = workDir ?? LubanDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                output = string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}";
                return process.ExitCode == 0;
            }
            catch
            {
                output = "";
                return false;
            }
        }

        private static void GenerateConfigLoader(LubanSettings settings, string codeDir)
        {
            var isBin = settings.dataTarget == DataTarget.Bin;
            var loaderParam = isBin ? "Func<string, byte[]> loader" : "Func<string, string> loader";
            var wrapLoader = isBin ? "name => new ByteBuf(loader(name))" : "loader";
            var usingLuban = isBin ? "using Luban;\n" : "";

            var code = $@"// Auto-generated by Luban Editor
using System;
{usingLuban}
namespace Puffin.Modules.ConfigModule.Runtime
{{
    public static class ConfigLoader
    {{
        public static Type TablesType => typeof({settings.topModule}.{settings.managerName});

        public static object CreateTables({loaderParam}) => new {settings.topModule}.{settings.managerName}({wrapLoader});
    }}
}}
";
            var loaderPath = Path.Combine(codeDir, "ConfigLoader.cs");
            File.WriteAllText(loaderPath, code);
        }

        [MenuItem("Puffin Framework/Config/Generate Config")]
        public static void GenerateConfigMenu()
        {
            var window = GetWindow<LubanEditorWindow>();
            window.GenerateConfig();
        }
    }
}
#endif
