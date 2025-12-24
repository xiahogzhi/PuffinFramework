#if UNITY_EDITOR
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Core
{
    /// <summary>
    /// PuffinSettings 的自定义编辑器
    /// </summary>
    [CustomEditor(typeof(PuffinSettings))]
    public class PuffinettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (PuffinSettings)target;
            serializedObject.Update();

            // 扫描配置
            EditorGUILayout.LabelField("扫描配置", EditorStyles.boldLabel);
            settings.scanMode = (ScanMode)EditorGUILayout.EnumPopup(
                new GUIContent("扫描模式", "扫描模式"), settings.scanMode);
            settings.requireAutoRegister = EditorGUILayout.Toggle(
                new GUIContent("只扫描 [AutoRegister]", "是否只扫描带 [AutoRegister] 特性的系统"), settings.requireAutoRegister);

            if (settings.scanMode == ScanMode.Specified)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("assemblyNames"),
                    new GUIContent("程序集名称", "要扫描的程序集名称（ScanMode.Specified 时生效）"), true);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("excludeAssemblyPrefixes"),
                new GUIContent("排除程序集前缀", "排除的程序集前缀"), true);

            EditorGUILayout.Space();

            // Runtime 配置
            EditorGUILayout.LabelField("Runtime 配置", EditorStyles.boldLabel);
            settings.enableProfiling = EditorGUILayout.Toggle(
                new GUIContent("启用性能统计", "是否启用性能统计"), settings.enableProfiling);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("symbols"),
                new GUIContent("条件符号", "预定义的条件符号"), true);

            EditorGUILayout.Space();

            // 初始化配置
            EditorGUILayout.LabelField("初始化配置", EditorStyles.boldLabel);
            settings.autoInitialize = EditorGUILayout.Toggle(
                new GUIContent("自动初始化", "是否自动初始化（运行时进入 Play 模式自动初始化）"), settings.autoInitialize);
            settings.enableEditorSupport = EditorGUILayout.Toggle(
                new GUIContent("编辑器支持", "是否在编辑器模式下初始化支持 IEditorSupport 的系统"), settings.enableEditorSupport);

            EditorGUILayout.Space();

            // 日志配置
            EditorGUILayout.LabelField("日志配置", EditorStyles.boldLabel);
            settings.systemInfoLevel = (SystemInfoLevel)EditorGUILayout.EnumPopup(
                new GUIContent("系统信息级别", "系统信息输出级别"), settings.systemInfoLevel);

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(settings);
            }
        }
    }
}
#endif
