#if UNITY_EDITOR
using System;
using Puffin.Editor.Hub.Data;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.UI
{
    /// <summary>
    /// 添加仓库窗口
    /// </summary>
    public class AddRegistryWindow : EditorWindow
    {
        private Action<RegistrySource> _onAdd;
        private string _name = "";
        private string _url = "";
        private string _branch = "main";

        public static void Show(Action<RegistrySource> onAdd)
        {
            var window = GetWindow<AddRegistryWindow>(true, "添加仓库源");
            window._onAdd = onAdd;
            window.minSize = window.maxSize = new Vector2(350, 130);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            _name = EditorGUILayout.TextField("名称", _name);
            _url = EditorGUILayout.TextField("URL (owner/repo)", _url);
            _branch = EditorGUILayout.TextField("分支", _branch);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Width(80))) Close();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_name) || string.IsNullOrEmpty(_url));
            if (GUILayout.Button("添加", GUILayout.Width(80)))
            {
                _onAdd?.Invoke(new RegistrySource
                {
                    id = Guid.NewGuid().ToString("N").Substring(0, 8),
                    name = _name, url = _url, branch = _branch, enabled = true
                });
                Close();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif