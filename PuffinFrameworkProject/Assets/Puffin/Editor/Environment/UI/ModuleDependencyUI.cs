#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Environment.UI
{
    /// <summary>
    /// 模块依赖检测 UI 组件
    /// </summary>
    public static class ModuleDependencyUI
    {
        /// <summary>
        /// 绘制依赖检测界面，返回是否所有必须依赖已安装
        /// </summary>
        /// <param name="moduleName">模块名称</param>
        /// <param name="manager">依赖管理器实例</param>
        /// <returns>true 表示所有必须依赖已安装</returns>
        public static bool DrawDependencyCheck(string moduleName, DependencyManager manager)
        {
            var config = DependencyManager.GetModuleConfig(moduleName);
            if (config?.dependencies == null) return true;

            var requiredDeps = config.dependencies.Where(d => d.requirement == DependencyRequirement.Required).ToArray();
            if (requiredDeps.Length == 0) return true;

            var missingDeps = requiredDeps.Where(d => !manager.IsInstalled(d)).ToArray();
            if (missingDeps.Length == 0) return true;

            // 绘制安装界面
            EditorGUILayout.Space(20);
            GUILayout.FlexibleSpace();

            // 状态标签
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            foreach (var dep in requiredDeps)
            {
                var installed = manager.IsInstalled(dep);
                DrawStatusLabel(dep.displayName ?? dep.id, installed);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            // 安装按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("安装环境", GUILayout.Width(200), GUILayout.Height(40)))
            {
                InstallWindow.Show(missingDeps, () => {
                    // 刷新当前窗口
                    if (EditorWindow.focusedWindow != null)
                        EditorWindow.focusedWindow.Repaint();
                });
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            return false;
        }

        private static void DrawStatusLabel(string label, bool ok)
        {
            var color = ok ? Color.green : Color.gray;
            var icon = ok ? "✓" : "○";
            var style = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
            GUILayout.Label($"{icon} {label}", style, GUILayout.Width(100));
        }
    }
}
#endif
