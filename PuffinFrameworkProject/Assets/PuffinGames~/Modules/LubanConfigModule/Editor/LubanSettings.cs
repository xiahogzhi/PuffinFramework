#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Puffin.Modules.LubanConfigModule.Editor
{
    [Serializable]
    public class LubanSettings : ScriptableObject
    {
        private const string SettingsPath = "Assets/Puffin/Modules/LubanConfigModule/Editor/LubanSettings.asset";

        [Header("目录配置")]
        [Tooltip("Excel/数据源目录（相对于 Luban 目录）")]
        public string dataDir = "Datas";

        [Tooltip("Schema 定义目录（相对于 Luban 目录）")]
        public string defineDir = "Defines";

        [Tooltip("代码输出目录（相对于 Luban 目录）")]
        public string outputCodeDir = "../Assets/Puffin/Modules/LubanConfigModule/Runtime/Generate";

        [Tooltip("数据输出目录（相对于 Luban 目录）")]
        public string outputDataDir = "../Assets/Puffin/Modules/LubanConfigModule/Resources";

        [Header("生成配置")]
        [Tooltip("代码目标")]
        public CodeTarget codeTarget = CodeTarget.CsSimpleJson;

        [Tooltip("数据目标")]
        public DataTarget dataTarget = DataTarget.Json;

        [Tooltip("顶层命名空间")]
        public string topModule = "PuffinFrameworks.Config.Gen";

        [Tooltip("Tables 管理类名")]
        public string managerName = "Tables";

        [Header("自动导入配置")]
        [Tooltip("启用自动导入 table（文件名以 # 开头的 excel 自动识别为表）")]
        public bool enableAutoImport = true;

        [Header("高级配置")]
        [Tooltip("时区")]
        public string timeZone = "";

        [Tooltip("自定义模板目录")]
        public string customTemplateDir = "";

        [Tooltip("额外命令行参数")]
        public string extraArgs = "";

        private static LubanSettings _instance;

        public static LubanSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<LubanSettings>(SettingsPath);
                    if (_instance == null)
                    {
                        _instance = CreateInstance<LubanSettings>();
                        var dir = Path.GetDirectoryName(SettingsPath);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        AssetDatabase.CreateAsset(_instance, SettingsPath);
                        AssetDatabase.SaveAssets();
                    }
                }
                return _instance;
            }
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public string GenerateLubanConf()
        {
            var conf = new LubanConf
            {
                groups = new List<LubanGroup>
                {
                    new() { names = new List<string> { "c" }, @default = true },
                    new() { names = new List<string> { "s" }, @default = true },
                    new() { names = new List<string> { "e" }, @default = true }
                },
                schemaFiles = new List<LubanSchemaFile>
                {
                    new() { fileName = defineDir, type = "" },
                    new() { fileName = $"{dataDir}/__tables__.xlsx", type = "table" },
                    new() { fileName = $"{dataDir}/__beans__.xlsx", type = "bean" },
                    new() { fileName = $"{dataDir}/__enums__.xlsx", type = "enum" }
                },
                dataDir = dataDir,
                targets = new List<LubanTarget>
                {
                    new()
                    {
                        name = "client",
                        manager = managerName,
                        groups = new List<string> { "c" },
                        topModule = topModule
                    }
                },
                codeTargets = new List<string> { GetCodeTargetString(codeTarget) },
                dataTargets = new List<string> { GetDataTargetString(dataTarget) },
                outputCodeDir = outputCodeDir,
                outputDataDir = outputDataDir
            };

            // 启用自动导入 table
            if (enableAutoImport)
            {
                conf.importTables = new List<LubanImportTable>
                {
                    new()
                    {
                        filePattern = $"{dataDir}/**/#*.xlsx",
                        tableNamePattern = "{0}::Tb{1}",
                        tableValueTypePattern = "{0}::{1}"
                    }
                };
            }

            return JsonUtility.ToJson(conf, true);
        }

        public static bool NeedsRuntime(CodeTarget target) => target is CodeTarget.CsBin or CodeTarget.CsSimpleJson or CodeTarget.CsNewtonsoft;

        public static string GetCodeTargetString(CodeTarget target) => target switch
        {
            CodeTarget.CsBin => "cs-bin",
            CodeTarget.CsSimpleJson => "cs-simple-json",
            CodeTarget.CsNewtonsoft => "cs-newtonsoft-json",
            CodeTarget.JavaBin => "java-bin",
            CodeTarget.JavaJson => "java-json",
            CodeTarget.CppBin => "cpp-bin",
            CodeTarget.GoBin => "go-bin",
            CodeTarget.GoJson => "go-json",
            CodeTarget.LuaBin => "lua-bin",
            CodeTarget.LuaLua => "lua-lua",
            CodeTarget.PythonJson => "python-json",
            CodeTarget.TypescriptJson => "typescript-json",
            CodeTarget.RustJson => "rust-json",
            CodeTarget.Protobuf2 => "protobuf2",
            CodeTarget.Protobuf3 => "protobuf3",
            CodeTarget.Flatbuffers => "flatbuffers",
            _ => "cs-simple-json"
        };

        public static string GetDataTargetString(DataTarget target) => target switch
        {
            DataTarget.Bin => "bin",
            DataTarget.Json => "json",
            DataTarget.Lua => "lua",
            DataTarget.Xml => "xml",
            DataTarget.Yaml => "yaml",
            DataTarget.Protobuf2Bin => "protobuf-bin",
            DataTarget.Protobuf3Bin => "protobuf3-bin",
            DataTarget.FlatbuffersBin => "flatbuffers-bin",
            _ => "json"
        };
    }

    public enum CodeTarget
    {
        [InspectorName("C# Binary")] CsBin,
        [InspectorName("C# SimpleJson")] CsSimpleJson,
        [InspectorName("C# Newtonsoft.Json")] CsNewtonsoft,
        [InspectorName("Java Binary")] JavaBin,
        [InspectorName("Java Json")] JavaJson,
        [InspectorName("C++ Binary")] CppBin,
        [InspectorName("Go Binary")] GoBin,
        [InspectorName("Go Json")] GoJson,
        [InspectorName("Lua Binary")] LuaBin,
        [InspectorName("Lua Lua")] LuaLua,
        [InspectorName("Python Json")] PythonJson,
        [InspectorName("TypeScript Json")] TypescriptJson,
        [InspectorName("Rust Json")] RustJson,
        [InspectorName("Protobuf 2")] Protobuf2,
        [InspectorName("Protobuf 3")] Protobuf3,
        [InspectorName("FlatBuffers")] Flatbuffers
    }

    public enum DataTarget
    {
        [InspectorName("Binary")] Bin,
        [InspectorName("JSON")] Json,
        [InspectorName("Lua")] Lua,
        [InspectorName("XML")] Xml,
        [InspectorName("YAML")] Yaml,
        [InspectorName("Protobuf 2 Binary")] Protobuf2Bin,
        [InspectorName("Protobuf 3 Binary")] Protobuf3Bin,
        [InspectorName("FlatBuffers Binary")] FlatbuffersBin
    }

    // luban.conf JSON 结构
    [Serializable]
    public class LubanConf
    {
        public List<LubanGroup> groups;
        public List<LubanSchemaFile> schemaFiles;
        public string dataDir;
        public List<LubanTarget> targets;
        public List<string> codeTargets;
        public List<string> dataTargets;
        public string outputCodeDir;
        public string outputDataDir;
        public List<LubanImportTable> importTables;
    }

    [Serializable]
    public class LubanImportTable
    {
        public string filePattern;
        public string tableNamePattern;
        public string tableValueTypePattern;
    }

    [Serializable]
    public class LubanGroup
    {
        public List<string> names;
        public bool @default;
    }

    [Serializable]
    public class LubanSchemaFile
    {
        public string fileName;
        public string type;
    }

    [Serializable]
    public class LubanTarget
    {
        public string name;
        public string manager;
        public List<string> groups;
        public string topModule;
    }
}
#endif
