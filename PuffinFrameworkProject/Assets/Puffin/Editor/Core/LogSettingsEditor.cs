#if UNITY_EDITOR
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;
using L = Puffin.Editor.Localization.EditorLocalization;

namespace Puffin.Editor.Core
{
    /// <summary>
    /// LogSettings 的自定义编辑器，提供本地化的日志配置界面
    /// </summary>
    [CustomEditor(typeof(LogSettings))]
    public class LogSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (LogSettings)target;
            serializedObject.Update();

            // Basic Config
            EditorGUILayout.LabelField(L.L("log.basic_config"), EditorStyles.boldLabel);
            settings.globalLogLevel = (LogLevel)EditorGUILayout.EnumPopup(
                new GUIContent(L.L("log.global_level"), L.L("log.global_level_tip")), settings.globalLogLevel);
            settings.enableStackTrace = EditorGUILayout.Toggle(
                new GUIContent(L.L("log.stack_trace"), L.L("log.stack_trace_tip")), settings.enableStackTrace);
            settings.enableColors = EditorGUILayout.Toggle(
                new GUIContent(L.L("log.enable_colors"), L.L("log.enable_colors_tip")), settings.enableColors);
            settings.maxCollectionElements = EditorGUILayout.IntField(
                new GUIContent(L.L("log.max_elements"), L.L("log.max_elements_tip")), settings.maxCollectionElements);

            EditorGUILayout.Space();

            // Platform Config
            EditorGUILayout.LabelField(L.L("log.platform_config"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("platformConfigs"),
                new GUIContent(L.L("log.platform_specific"), L.L("log.platform_specific_tip")), true);

            EditorGUILayout.Space();

            // Color Config
            EditorGUILayout.LabelField(L.L("log.color_config"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("infoColors"),
                new GUIContent(L.L("log.info_colors")), true);
            settings.warningColor = EditorGUILayout.ColorField(L.L("log.warning_color"), settings.warningColor);
            settings.errorColor = EditorGUILayout.ColorField(L.L("log.error_color"), settings.errorColor);
            settings.tagColor = EditorGUILayout.ColorField(L.L("log.tag_color"), settings.tagColor);

            EditorGUILayout.Space();

            // Custom Tags
            EditorGUILayout.LabelField(L.L("log.custom_tags"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("customTags"), true);

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(settings);
            }
        }
    }
}
#endif
