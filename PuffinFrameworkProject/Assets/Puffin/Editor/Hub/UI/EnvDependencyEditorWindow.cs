using System;
using System.Linq;
using Puffin.Editor.Hub.Data;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.UI
{
    /// <summary>
    /// 环境依赖编辑窗口
    /// </summary>
    public class EnvDependencyEditorWindow : EditorWindow
    {
        private EnvironmentDependency _dependency;
        private Action<EnvironmentDependency> _onSaved;
        private bool _isNew;
        private Vector2 _scrollPos;

        // 临时编辑字段
        private string _requiredFilesStr;
        private string _targetFrameworksStr;
        private string _dllReferencesStr;
        private string _asmdefReferencesStr;

        private static readonly string[] SourceNames = { "NuGet", "GitHub Repo", "Direct URL", "GitHub Release", "Unity Package" };
        private static readonly string[] TypeNames = { "DLL", "Source", "Tool", "ReferenceOnly" };

        public static void ShowNew(Action<EnvironmentDependency> onSaved)
        {
            var window = GetWindow<EnvDependencyEditorWindow>(true, "添加环境依赖");
            window._dependency = new EnvironmentDependency { id = "", source = 0, type = 0 };
            window._onSaved = onSaved;
            window._isNew = true;
            window.InitTempFields();
            window.minSize = new Vector2(450, 500);
            window.ShowUtility();
        }

        public static void ShowEdit(EnvironmentDependency dependency, Action<EnvironmentDependency> onSaved)
        {
            var window = GetWindow<EnvDependencyEditorWindow>(true, "编辑环境依赖");
            window._dependency = dependency;
            window._onSaved = onSaved;
            window._isNew = false;
            window.InitTempFields();
            window.minSize = new Vector2(450, 500);
            window.ShowUtility();
        }

        private void InitTempFields()
        {
            _requiredFilesStr = _dependency.requiredFiles != null ? string.Join(", ", _dependency.requiredFiles) : "";
            _targetFrameworksStr = _dependency.targetFrameworks != null ? string.Join(", ", _dependency.targetFrameworks) : "";
            _dllReferencesStr = _dependency.dllReferences != null ? string.Join(", ", _dependency.dllReferences) : "";
            _asmdefReferencesStr = _dependency.asmdefReferences != null ? string.Join(", ", _dependency.asmdefReferences) : "";
        }

        private void OnGUI()
        {
            if (_dependency == null) { Close(); return; }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField(_isNew ? "添加环境依赖" : "编辑环境依赖", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            // 基本信息
            EditorGUILayout.LabelField("基本信息", EditorStyles.boldLabel);
            _dependency.id = EditorGUILayout.TextField("ID *", _dependency.id);
            _dependency.type = EditorGUILayout.Popup("类型", _dependency.type, TypeNames);

            var isReferenceOnly = _dependency.type == 3; // ReferenceOnly

            if (isReferenceOnly)
            {
                EditorGUILayout.HelpBox("仅引用类型：不需要下载安装，只添加程序集引用", MessageType.Info);
            }
            else
            {
                _dependency.source = EditorGUILayout.Popup("来源", _dependency.source, SourceNames);
                _dependency.version = EditorGUILayout.TextField("版本", _dependency.version);

                EditorGUILayout.Space(10);

                // 来源配置
                EditorGUILayout.LabelField("来源配置", EditorStyles.boldLabel);
                switch (_dependency.source)
                {
                    case 0: // NuGet
                        EditorGUILayout.HelpBox("NuGet 包会自动从 nuget.org 下载", MessageType.Info);
                        _targetFrameworksStr = EditorGUILayout.TextField("目标框架 (逗号分隔)", _targetFrameworksStr);
                        break;
                    case 1: // GitHub Repo
                        _dependency.url = EditorGUILayout.TextField("仓库 URL", _dependency.url);
                        _dependency.extractPath = EditorGUILayout.TextField("提取路径", _dependency.extractPath);
                        break;
                    case 2: // Direct URL
                        _dependency.url = EditorGUILayout.TextField("下载 URL", _dependency.url);
                        _dependency.extractPath = EditorGUILayout.TextField("提取路径", _dependency.extractPath);
                        break;
                    case 3: // GitHub Release
                        _dependency.url = EditorGUILayout.TextField("仓库 URL", _dependency.url);
                        EditorGUILayout.HelpBox("将从 GitHub Release 下载指定版本", MessageType.Info);
                        break;
                }

                EditorGUILayout.Space(10);

                // 安装配置
                EditorGUILayout.LabelField("安装配置", EditorStyles.boldLabel);
                _dependency.installDir = EditorGUILayout.TextField("安装目录", _dependency.installDir);
                EditorGUILayout.LabelField("必需文件 (逗号分隔):");
                _requiredFilesStr = EditorGUILayout.TextField(_requiredFilesStr);
            }

            EditorGUILayout.Space(10);

            // 引用配置
            EditorGUILayout.LabelField("引用配置", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("DLL 引用 (逗号分隔):");
            _dllReferencesStr = EditorGUILayout.TextField(_dllReferencesStr);
            EditorGUILayout.LabelField("程序集定义引用 (逗号分隔):");
            _asmdefReferencesStr = EditorGUILayout.TextField(_asmdefReferencesStr);

            EditorGUILayout.Space(10);

            // 选项
            EditorGUILayout.LabelField("选项", EditorStyles.boldLabel);
            _dependency.optional = EditorGUILayout.Toggle("可选依赖", _dependency.optional);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // 按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Width(80)))
                Close();

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_dependency.id?.Trim()));
            if (GUILayout.Button(_isNew ? "添加" : "保存", GUILayout.Width(80)))
            {
                SaveAndClose();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void SaveAndClose()
        {
            _dependency.id = _dependency.id.Trim();

            // 解析数组字段
            _dependency.requiredFiles = ParseArray(_requiredFilesStr);
            _dependency.targetFrameworks = ParseArray(_targetFrameworksStr);
            _dependency.dllReferences = ParseArray(_dllReferencesStr);
            _dependency.asmdefReferences = ParseArray(_asmdefReferencesStr);

            _onSaved?.Invoke(_dependency);
            Close();
        }

        private string[] ParseArray(string str)
        {
            if (string.IsNullOrEmpty(str)) return null;
            var arr = str.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            return arr.Length > 0 ? arr : null;
        }
    }
}
