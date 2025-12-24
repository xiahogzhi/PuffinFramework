#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PuffinGames.UPMEditor
{
    /// <summary>
    /// Shared GUI styles for UPM Editor
    /// </summary>
    public static class UPMEditorStyles
    {
        // Colors
        public static readonly Color HeaderBg = new Color(0.15f, 0.15f, 0.15f);
        public static readonly Color SectionBg = new Color(0.2f, 0.2f, 0.2f);
        public static readonly Color ErrorColor = new Color(1f, 0.4f, 0.4f);
        public static readonly Color WarningColor = new Color(1f, 0.8f, 0.4f);
        public static readonly Color SuccessColor = new Color(0.4f, 1f, 0.4f);

        private static GUIStyle _headerStyle;
        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle _boxStyle;

        public static GUIStyle HeaderStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        padding = new RectOffset(5, 5, 5, 5)
                    };
                }
                return _headerStyle;
            }
        }

        public static GUIStyle SectionHeaderStyle
        {
            get
            {
                if (_sectionHeaderStyle == null)
                {
                    _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                        padding = new RectOffset(0, 0, 5, 5)
                    };
                }
                return _sectionHeaderStyle;
            }
        }

        public static GUIStyle BoxStyle
        {
            get
            {
                if (_boxStyle == null)
                {
                    _boxStyle = new GUIStyle("box")
                    {
                        padding = new RectOffset(10, 10, 10, 10),
                        margin = new RectOffset(0, 0, 5, 5)
                    };
                }
                return _boxStyle;
            }
        }

        /// <summary>
        /// Draw a section header with background
        /// </summary>
        public static void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(5);
            var rect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(rect, HeaderBg);
            GUI.Label(rect, title, SectionHeaderStyle);
        }

        /// <summary>
        /// Draw a horizontal line
        /// </summary>
        public static void DrawLine()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
        }
    }
}
#endif
