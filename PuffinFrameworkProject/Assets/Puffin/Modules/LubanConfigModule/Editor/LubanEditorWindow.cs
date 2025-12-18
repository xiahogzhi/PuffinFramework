#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Environment;
using Puffin.Editor.Environment.Core;
using Puffin.Editor.Environment.UI;
using Puffin.Modules.LubanConfigModule.Runtime;
using Puffin.Runtime.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Puffin.Modules.LubanConfigModule.Editor
{
    public class LubanEditorWindow : EditorWindow
    {
        private const string ModuleId = "LubanConfigModule";
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private bool _isProcessing;
        private string _status = "";

        private Vector2 _scrollPos;
        private bool _showDirConfig = true;
        private bool _showGenConfig = true;
        private bool _showAdvConfig;
        private SerializedObject _settingsSO;
        private DependencyManager _depManager;

        private DependencyDefinition _depLubanTool;
        private DependencyDefinition _depTemplate;

        [MenuItem("Puffin/Config/Luban Editor")]
        public static void ShowWindow() => GetWindow<LubanEditorWindow>("Luban Editor");

        private void OnEnable()
        {
            _settingsSO = new SerializedObject(LubanSettings.Instance);
            _depManager = new DependencyManager();

            _depLubanTool = ModuleDependencyUI.FindEnvDependency(ModuleId, "LubanTool");
            _depTemplate = ModuleDependencyUI.FindEnvDependency(ModuleId, "LubanTemplate");
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // 检测必须依赖是否安装
            if (!ModuleDependencyUI.DrawDependencyCheck(ModuleId, _depManager))
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

            if (GUILayout.Button("重新加载", EditorStyles.toolbarButton))
                PuffinFramework.GetSystem<IConfigSystem>()?.ReloadAsync().Forget();

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
                InstallWindow.ShowForModule(ModuleId);

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
                if (ct is not (CodeTarget.CsBin or CodeTarget.CsSimpleJson or CodeTarget.CsNewtonsoft))
                    EditorGUILayout.HelpBox("当前代码目标不是 C#，Unity 项目可能无法使用生成的代码", MessageType.Warning);

                EditorGUILayout.PropertyField(_settingsSO.FindProperty("dataTarget"), new GUIContent("数据格式"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("topModule"), new GUIContent("命名空间"));
                EditorGUILayout.PropertyField(_settingsSO.FindProperty("managerName"), new GUIContent("管理类名"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("enableAutoImport"), new GUIContent("启用自动导入 Table", "文件名以 # 开头的 excel 自动识别为表"));

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
            _status = "正在生成配置...";
            Repaint();

            SaveLubanConf();
            var success = GenerateConfigStatic();

            _status = success ? "生成完成" : "生成失败";
            _isProcessing = false;
            Repaint();
        }

        /// <summary>
        /// 静态生成配置方法
        /// </summary>
        public static bool GenerateConfigStatic()
        {
            try
            {
                var confDir = GetLubanConfigDir();
                var confPath = Path.Combine(confDir, "luban.conf");

                // 保存配置
                if (!Directory.Exists(confDir)) Directory.CreateDirectory(confDir);
                File.WriteAllText(confPath, LubanSettings.Instance.GenerateLubanConf());

                var settings = LubanSettings.Instance;
                var codeTarget = LubanSettings.GetCodeTargetString(settings.codeTarget);
                var dataTarget = LubanSettings.GetDataTargetString(settings.dataTarget);

                var codeDir = Path.GetFullPath(Path.Combine(confDir, settings.outputCodeDir));
                var dataDir = Path.GetFullPath(Path.Combine(confDir, settings.outputDataDir));
                Directory.CreateDirectory(codeDir);
                Directory.CreateDirectory(dataDir);

                var lubanExe = GetLubanExePathStatic();
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
                    if (LubanSettings.NeedsRuntime(settings.codeTarget))
                        GenerateLubanRuntime(codeDir);
                    Debug.Log($"[Luban] 生成完成\n{output}");
                    AssetDatabase.Refresh();
                    return true;
                }

                Debug.LogError($"[Luban] 生成失败\n{output}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Luban] 生成失败: {e}");
                return false;
            }
        }

        private static string GetLubanExePathStatic()
        {
            var depLubanTool = ModuleDependencyUI.FindEnvDependency(ModuleId, "LubanTool");
            if (depLubanTool == null) return null;
            var manager = new DependencyManager();
            var installPath = manager.GetInstallPath(depLubanTool);
            return Path.Combine(installPath, "Luban", "Luban.dll");
        }

        /// <summary>
        /// 检查必须依赖是否已安装
        /// </summary>
        public static bool CheckDependencies()
        {
            var envDeps = ModuleDependencyUI.GetModuleEnvDependencies(ModuleId);
            if (envDeps == null || envDeps.Count == 0) return true;

            var manager = new DependencyManager();
            return envDeps.All(d => manager.IsInstalled(d));
        }

        private static void GenerateConfigLoader(LubanSettings settings, string codeDir)
        {
            var (loaderParam, wrapLoader, usings) = settings.codeTarget switch
            {
                CodeTarget.CsBin => ("Func<string, byte[]> loader", "name => new Luban.ByteBuf(loader(name))", ""),
                CodeTarget.CsSimpleJson => ("Func<string, string> loader", "name => SimpleJSON.JSON.Parse(loader(name))", ""),
                CodeTarget.CsNewtonsoft => ("Func<string, string> loader", "name => Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JArray>(loader(name))!", ""),
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

        private static void GenerateLubanRuntime(string codeDir)
        {
            var srcDir = Path.Combine(Application.dataPath, "Puffin/Modules/Config/Runtime/LubanLib");
            if (!Directory.Exists(srcDir))
            {
                Debug.LogWarning("[Luban] 运行时未安装，请先安装 LubanRuntime 依赖");
                return;
            }

            var destDir = Path.Combine(codeDir, "Luban");
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(srcDir, "*.cs"))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        }

        [MenuItem("Puffin/Config/Generate Config &q")]
        public static void GenerateConfigMenu()
        {
            // 检查依赖是否安装
            if (!CheckDependencies())
            {
                // 打开窗口让用户安装依赖
                ShowWindow();
                return;
            }

            // 直接生成，不打开窗口
            GenerateConfigStatic();
        }
    }
}
#endif
