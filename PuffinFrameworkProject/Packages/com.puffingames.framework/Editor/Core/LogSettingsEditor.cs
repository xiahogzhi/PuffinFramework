#if UNITY_EDITOR
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Core
{
    /// <summary>
    /// LogSettings 的自定义编辑器
    /// </summary>
    [CustomEditor(typeof(LogSettings))]
    public class LogSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (LogSettings)target;
            serializedObject.Update();

            // 基础配置
            EditorGUILayout.LabelField("基础配置", EditorStyles.boldLabel);
            settings.globalLogLevel = (LogLevel)EditorGUILayout.EnumPopup(
                new GUIContent("全局日志等级", "全局日志等级"), settings.globalLogLevel);
            settings.enableStackTrace = EditorGUILayout.Toggle(
                new GUIContent("启用堆栈追踪", "是否启用堆栈追踪"), settings.enableStackTrace);
            settings.enableColors = EditorGUILayout.Toggle(
                new GUIContent("启用颜色", "是否启用颜色"), settings.enableColors);
            settings.maxCollectionElements = EditorGUILayout.IntField(
                new GUIContent("集合最大元素数", "集合输出最大元素数"), settings.maxCollectionElements);

            EditorGUILayout.Space();

            // 平台配置
            EditorGUILayout.LabelField("平台配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("platformConfigs"),
                new GUIContent("平台特定配置", "平台特定配置（优先级高于全局配置）"), true);

            EditorGUILayout.Space();

            // 颜色配置
            EditorGUILayout.LabelField("颜色配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("infoColors"),
                new GUIContent("Info 颜色列表"), true);
            settings.warningColor = EditorGUILayout.ColorField("Warning 颜色", settings.warningColor);
            settings.errorColor = EditorGUILayout.ColorField("Error 颜色", settings.errorColor);
            settings.tagColor = EditorGUILayout.ColorField("标签颜色", settings.tagColor);

            EditorGUILayout.Space();

            // 自定义标签
            EditorGUILayout.LabelField("自定义标签", EditorStyles.boldLabel);
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
