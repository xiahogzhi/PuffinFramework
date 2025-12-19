#if UNITY_EDITOR
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;
using L = Puffin.Editor.Localization.EditorLocalization;

namespace Puffin.Editor.Core
{
    /// <summary>
    /// PuffinSettings 的自定义编辑器，提供本地化的框架配置界面
    /// </summary>
    [CustomEditor(typeof(PuffinSettings))]
    public class PuffinettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (PuffinSettings)target;
            serializedObject.Update();

            // 语言选择
            EditorGUILayout.LabelField(L.L("settings.editor_config"), EditorStyles.boldLabel);
            var newLang = (EditorLanguage)EditorGUILayout.EnumPopup(
                new GUIContent(L.L("settings.language")), settings.editorLanguage);
            if (newLang != settings.editorLanguage)
            {
                settings.editorLanguage = newLang;
                L.Reload();
            }

            EditorGUILayout.Space();

            // 扫描配置
            EditorGUILayout.LabelField(L.L("settings.scan_config"), EditorStyles.boldLabel);
            settings.scanMode = (ScanMode)EditorGUILayout.EnumPopup(
                new GUIContent(L.L("settings.scan_mode"), L.L("settings.scan_mode_tip")), settings.scanMode);
            settings.requireAutoRegister = EditorGUILayout.Toggle(
                new GUIContent(L.L("settings.require_autoregister"), L.L("settings.require_autoregister_tip")), settings.requireAutoRegister);

            if (settings.scanMode == ScanMode.Specified)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("assemblyNames"),
                    new GUIContent(L.L("settings.assembly_names"), L.L("settings.assembly_names_tip")), true);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("excludeAssemblyPrefixes"),
                new GUIContent(L.L("settings.exclude_prefixes"), L.L("settings.exclude_prefixes_tip")), true);

            EditorGUILayout.Space();

            // Runtime 配置
            EditorGUILayout.LabelField(L.L("settings.runtime_config"), EditorStyles.boldLabel);
            settings.enableProfiling = EditorGUILayout.Toggle(
                new GUIContent(L.L("settings.enable_profiling"), L.L("settings.enable_profiling_tip")), settings.enableProfiling);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("symbols"),
                new GUIContent(L.L("settings.symbols"), L.L("settings.symbols_tip")), true);

            EditorGUILayout.Space();

            // 初始化配置
            EditorGUILayout.LabelField(L.L("settings.init_config"), EditorStyles.boldLabel);
            settings.autoInitialize = EditorGUILayout.Toggle(
                new GUIContent(L.L("settings.auto_init"), L.L("settings.auto_init_tip")), settings.autoInitialize);
            settings.enableEditorSupport = EditorGUILayout.Toggle(
                new GUIContent(L.L("settings.editor_support"), L.L("settings.editor_support_tip")), settings.enableEditorSupport);

            EditorGUILayout.Space();

            // 日志配置
            EditorGUILayout.LabelField(L.L("settings.log_config"), EditorStyles.boldLabel);
            settings.systemInfoLevel = (SystemInfoLevel)EditorGUILayout.EnumPopup(
                new GUIContent(L.L("settings.system_info_level"), L.L("settings.system_info_level_tip")), settings.systemInfoLevel);

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(settings);
            }
        }
    }
}
#endif
