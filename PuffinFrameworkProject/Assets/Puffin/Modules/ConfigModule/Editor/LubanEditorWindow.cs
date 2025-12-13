#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Puffin.Editor.Environment;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Puffin.Modules.ConfigModule.Editor
{
    public class LubanEditorWindow : EditorWindow
    {
        private static string DependenciesJsonPath => Path.Combine(Application.dataPath, "Puffin/Modules/ConfigModule/Editor/dependencies.json");
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private bool _isProcessing;
        private string _status = "";

        private Vector2 _scrollPos;
        private bool _showDirConfig = true;
        private bool _showGenConfig = true;
        private bool _showAdvConfig;
        private SerializedObject _settingsSO;
        private DependencyConfig _depConfig;
        private DependencyManager _depManager;

        private DependencyDefinition _depLubanTool;
        private DependencyDefinition _depTemplate;

        [MenuItem("Puffin Framework/Config/Luban Editor")]
        public static void ShowWindow() => GetWindow<LubanEditorWindow>("Luban Editor");

        private void OnEnable()
        {
            _settingsSO = new SerializedObject(LubanSettings.Instance);
            _depConfig = DependencyManager.LoadConfig(DependenciesJsonPath);
            _depManager = new DependencyManager();

            _depLubanTool = DependencyManager.FindDependency(_depConfig, "LubanTool");
            _depTemplate = DependencyManager.FindDependency(_depConfig, "LubanTemplate");
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // 检测必须依赖是否安装
            if (!ModuleDependencyUI.DrawDependencyCheck("ConfigModule", _depManager))
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawToolbar();
            DrawConfigSection();
            DrawStatusSection();

            EditorGUILayout.EndScrollView();

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
                {
                    _depManager.Uninstall(_depTemplate);
                    InstallWindow.Show(new[] { _depTemplate }, () => {
                        GenerateLubanConf();
                        Repaint();
                    });
                }
            }

            if (GUILayout.Button("依赖管理", EditorStyles.toolbarButton))
                InstallWindow.ShowForModule("ConfigModule");

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
                Repaint();

            EditorGUILayout.EndHorizontal();
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

                var ct = LubanSettings.Instance.codeTarget;
                if (ct is not (CodeTarget.CsBin or CodeTarget.CsSimpleJson or CodeTarget.CsDotnetJson or CodeTarget.CsNewtonsoft))
                    EditorGUILayout.HelpBox("当前代码目标不是 C#，Unity 项目可能无法使用生成的代码", MessageType.Warning);

                EditorGUILayout.PropertyField(_settingsSO.FindProperty("dataTarget"), new GUIContent("数据格式"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("topModule"), new GUIContent("命名空间"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("managerName"), new GUIContent("管理类名"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("enableAutoImport"), new GUIContent("启用自动导入 Table", "文件名以 # 开头的 excel 自动识别为表"));

            // 检测运行时依赖
            var codeTarget = LubanSettings.Instance.codeTarget;
            if (LubanSettings.NeedsRuntime(codeTarget))
            {
                var deps = GetRequiredDependencies(codeTarget);
                var missingDeps = deps.FindAll(d => !_depManager.IsInstalled(d));
                if (missingDeps.Count > 0)
                {
                    var (_, hint) = GetRuntimeInfo(codeTarget);
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(hint, MessageType.Warning);
                    if (GUILayout.Button("安装依赖"))
                    {
                        InstallWindow.Show(missingDeps.ToArray(), () => Repaint());
                    }
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

        private System.Collections.Generic.List<DependencyDefinition> GetRequiredDependencies(CodeTarget target)
        {
            var deps = new System.Collections.Generic.List<DependencyDefinition>();
            if (_depConfig == null) return deps;

            // JSON 库依赖
            switch (target)
            {
                case CodeTarget.CsSimpleJson:
                    AddDep(deps, "SimpleJSON");
                    break;
                case CodeTarget.CsNewtonsoft:
                    AddDep(deps, "Newtonsoft.Json");
                    break;
                case CodeTarget.CsDotnetJson:
                    AddDep(deps, "System.Text.Json");
                    break;
            }
            return deps;
        }

        private void AddDep(System.Collections.Generic.List<DependencyDefinition> list, string id)
        {
            var dep = DependencyManager.FindDependency(_depConfig, id);
            if (dep != null) list.Add(dep);
        }

        private static (string name, string hint) GetRuntimeInfo(CodeTarget target) => target switch
        {
            CodeTarget.CsBin => ("Luban 运行时", "cs-bin 格式需要 Luban 运行时库（ByteBuf）"),
            CodeTarget.CsSimpleJson => ("SimpleJSON", "cs-simple-json 格式需要 SimpleJSON 库"),
            CodeTarget.CsNewtonsoft => ("Newtonsoft.Json", "cs-newtonsoft-json 格式需要 Newtonsoft.Json 库"),
            CodeTarget.CsDotnetJson => ("System.Text.Json", "cs-dotnet-json 格式需要 System.Text.Json 库"),
            _ => ("", "")
        };

        private void DrawStatusSection()
        {
            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(_status, MessageType.Info);
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

        private void GenerateLubanConf()
        {
            var confDir = GetLubanConfigDir();
            if (!Directory.Exists(confDir)) return;
            var confPath = Path.Combine(confDir, "luban.conf");
            File.WriteAllText(confPath, LubanSettings.Instance.GenerateLubanConf());
        }

        private string GetLubanExePath()
        {
            if (_depLubanTool == null) return null;
            var installPath = _depManager.GetInstallPath(_depLubanTool);
            return Path.Combine(installPath, "Luban", "Luban.dll");
        }

        private void GenerateConfig()
        {
            _isProcessing = true;
            _status = "正在保存配置...";
            Repaint();

            SaveLubanConf();

            _status = "正在生成配置...";
            Repaint();

            try
            {
                var confDir = GetLubanConfigDir();
                var confPath = Path.Combine(confDir, "luban.conf");

                var settings = LubanSettings.Instance;
                var codeTarget = LubanSettings.GetCodeTargetString(settings.codeTarget);
                var dataTarget = LubanSettings.GetDataTargetString(settings.dataTarget);

                var codeDir = Path.GetFullPath(Path.Combine(confDir, settings.outputCodeDir));
                var dataDir = Path.GetFullPath(Path.Combine(confDir, settings.outputDataDir));
                Directory.CreateDirectory(codeDir);
                Directory.CreateDirectory(dataDir);

                var lubanExe = GetLubanExePath();
                var args = $"\"{lubanExe}\" --conf \"{confPath}\" -t client -c {codeTarget} -d {dataTarget} -x outputCodeDir=\"{codeDir}\" -x outputDataDir=\"{dataDir}\"";

                if (!string.IsNullOrEmpty(settings.timeZone))
                    args += $" --timeZone \"{settings.timeZone}\"";
                if (!string.IsNullOrEmpty(settings.customTemplateDir))
                    args += $" --customTemplateDir \"{settings.customTemplateDir}\"";
                if (!string.IsNullOrEmpty(settings.extraArgs))
                    args += $" {settings.extraArgs}";

                if (EnvironmentChecker.RunCommand("dotnet", args, out var output, confDir))
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

        private static void GenerateConfigLoader(LubanSettings settings, string codeDir)
        {
            var (loaderParam, wrapLoader, usings) = settings.codeTarget switch
            {
                CodeTarget.CsBin => ("Func<string, byte[]> loader", "name => new Luban.ByteBuf(loader(name))", ""),
                CodeTarget.CsSimpleJson => ("Func<string, string> loader", "name => SimpleJSON.JSON.Parse(loader(name))", ""),
                CodeTarget.CsNewtonsoft => ("Func<string, string> loader", "name => Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JArray>(loader(name))!", ""),
                CodeTarget.CsDotnetJson => ("Func<string, string> loader", "name => System.Text.Json.JsonDocument.Parse(loader(name)).RootElement", ""),
                _ => ("Func<string, string> loader", "loader", "")
            };

            var code = $@"// Auto-generated by Luban Editor
using System;
{usings}
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
