#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Puffin.Editor.UPMEditor
{
    /// <summary>
    /// UPM 包发布工具 (支持 Verdaccio / GitHub Packages)
    /// </summary>
    public class UPMPublisher : EditorWindow
    {
        private string _packagePath = "";
        private string _registry = "http://localhost:4873";
        private string _packageName = "";
        private string _packageVersion = "";

        [MenuItem("Puffin/UPM Publisher")]
        public static void ShowWindow()
        {
            var window = GetWindow<UPMPublisher>("UPM Publisher");
            window.minSize = new Vector2(450, 300);
            window.LoadPrefs();
        }

        private void LoadPrefs()
        {
            _registry = EditorPrefs.GetString("UPM_Registry", "http://localhost:4873");
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString("UPM_Registry", _registry);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UPM Publisher", EditorStyles.boldLabel);

            EditorGUILayout.Space(10);

            // Registry
            EditorGUI.BeginChangeCheck();
            _registry = EditorGUILayout.TextField("Registry URL", _registry);
            if (EditorGUI.EndChangeCheck())
                SavePrefs();

            EditorGUILayout.Space(5);

            // 包路径
            EditorGUILayout.BeginHorizontal();
            _packagePath = EditorGUILayout.TextField("Package Path", _packagePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("Select Package", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    path = path.Replace("\\", "/");
                    var dataPath = Application.dataPath.Replace("\\", "/");
                    _packagePath = path.StartsWith(dataPath) ? "Assets" + path.Substring(dataPath.Length) : path;
                }
            }
            EditorGUILayout.EndHorizontal();

            // 读取 package.json
            ReadPackageJson();

            if (!string.IsNullOrEmpty(_packageName))
                EditorGUILayout.LabelField($"{_packageName} @ {_packageVersion}");
            else if (!string.IsNullOrEmpty(_packagePath))
                EditorGUILayout.HelpBox("package.json not found", MessageType.Warning);

            EditorGUILayout.Space(15);

            // 操作按钮
            GUI.enabled = !string.IsNullOrEmpty(_packageName);

            if (GUILayout.Button("Publish", GUILayout.Height(30)))
                Publish();

            if (GUILayout.Button("Unpublish This Version", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Unpublish", $"Delete {_packageName}@{_packageVersion}?", "Delete", "Cancel"))
                    Unpublish();
            }

            GUI.enabled = true;

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Copy Scoped Registry Config"))
                CopyConfig();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Verdaccio: npm adduser --registry http://localhost:4873\n" +
                "GitHub Packages: 需要在 ~/.npmrc 配置 token",
                MessageType.Info);
        }

        private void ReadPackageJson()
        {
            _packageName = "";
            _packageVersion = "";

            if (string.IsNullOrEmpty(_packagePath)) return;

            var jsonPath = Path.Combine(Path.GetFullPath(_packagePath), "package.json");
            if (!File.Exists(jsonPath)) return;

            try
            {
                var json = File.ReadAllText(jsonPath);
                _packageName = GetJsonValue(json, "name");
                _packageVersion = GetJsonValue(json, "version");
            }
            catch { }
        }

        private string GetJsonValue(string json, string key)
        {
            var idx = json.IndexOf($"\"{key}\"");
            if (idx < 0) return "";
            var start = json.IndexOf("\"", idx + key.Length + 2) + 1;
            var end = json.IndexOf("\"", start);
            return start > 0 && end > start ? json.Substring(start, end - start) : "";
        }

        private void Publish()
        {
            RunNpm($"publish --registry {_registry}");
        }

        private void Unpublish()
        {
            RunNpm($"unpublish {_packageName}@{_packageVersion} --registry {_registry}");
        }

        private void RunNpm(string args)
        {
            var npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                Debug.LogError("npm not found. Install Node.js and restart Unity.");
                return;
            }

            var workDir = Path.GetFullPath(_packagePath);

            var psi = new ProcessStartInfo
            {
                FileName = npmPath,
                Arguments = args,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Debug.Log($"[npm] {args}");

            try
            {
                using var p = Process.Start(psi);
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrEmpty(stdout)) Debug.Log(stdout);
                if (p.ExitCode == 0)
                    Debug.Log($"<color=green>✓ Success</color>");
                else
                {
                    if (!string.IsNullOrEmpty(stderr)) Debug.LogError(stderr);
                    Debug.LogError($"✗ Failed (code {p.ExitCode})");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"npm error: {e.Message}");
            }
        }

        private string FindNpmPath()
        {
            var candidates = new[]
            {
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "nodejs", "npm.cmd"),
                @"C:\Program Files\nodejs\npm.cmd",
                "/usr/local/bin/npm",
                "/usr/bin/npm",
                "/opt/homebrew/bin/npm"
            };

            foreach (var path in candidates)
                if (File.Exists(path)) return path;

            var pathEnv = System.Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var p in pathEnv.Split(Path.PathSeparator))
            {
                var npm = Path.Combine(p, Application.platform == RuntimePlatform.WindowsEditor ? "npm.cmd" : "npm");
                if (File.Exists(npm)) return npm;
            }

            return null;
        }

        private void CopyConfig()
        {
            var scope = _packageName.Contains("/") ? _packageName.Split('/')[0] : _packageName;
            var config = $@"Packages/manifest.json 添加:

""scopedRegistries"": [
  {{
    ""name"": ""Private"",
    ""url"": ""{_registry}"",
    ""scopes"": [""{scope}""]
  }}
]";
            GUIUtility.systemCopyBuffer = config;
            Debug.Log("Config copied:\n" + config);
        }
    }
}
#endif
