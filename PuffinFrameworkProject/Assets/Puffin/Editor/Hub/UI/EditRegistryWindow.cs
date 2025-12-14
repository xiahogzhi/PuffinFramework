using System;
using Puffin.Editor.Hub.Data;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.UI
{
    /// <summary>
    /// 编辑仓库窗口
    /// </summary>
    public class EditRegistryWindow : EditorWindow
    {
        private RegistrySource _registry;
        private Action _onSave;

        public static void Show(RegistrySource registry, Action onSave)
        {
            var window = GetWindow<EditRegistryWindow>(true, "编辑仓库源");
            window._registry = registry;
            window._onSave = onSave;
            window.minSize = window.maxSize = new Vector2(350, 150);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (_registry == null) { Close(); return; }

            EditorGUILayout.Space(5);
            _registry.name = EditorGUILayout.TextField("名称", _registry.name);
            _registry.url = EditorGUILayout.TextField("URL (owner/repo)", _registry.url);
            _registry.branch = EditorGUILayout.TextField("分支", _registry.branch);
            _registry.authToken = EditorGUILayout.PasswordField("Token (可选)", _registry.authToken ?? "");
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Width(80))) Close();
            if (GUILayout.Button("保存", GUILayout.Width(80))) { _onSave?.Invoke(); Close(); }
            EditorGUILayout.EndHorizontal();
        }
    }
}