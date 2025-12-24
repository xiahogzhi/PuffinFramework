#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PuffinGames.UPMEditor
{
    /// <summary>
    /// 右键菜单 - 只保留转换和创建功能
    /// </summary>
    public static class UPMContextMenu
    {
        private const int MenuPriority = 1000;

        #region Convert

        [MenuItem(UPMConstants.ContextMenuRoot + "转换到 Packages", false, MenuPriority)]
        private static void MoveToPackages()
        {
            var selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath)) return;

            var targetPath = DirectoryMoveService.PreviewTargetPath(selectedPath, true);

            if (EditorUtility.DisplayDialog(
                "转换到 Packages",
                $"从:\n{selectedPath}\n\n移动到:\n{targetPath}\n\n此操作不可撤销",
                "转换", "取消"))
            {
                var result = DirectoryMoveService.MoveToPackages(selectedPath);
                if (!result.Success)
                {
                    EditorUtility.DisplayDialog("错误", result.ErrorMessage, "确定");
                }
            }
        }

        [MenuItem(UPMConstants.ContextMenuRoot + "转换到 Packages", true)]
        private static bool MoveToPackagesValidation()
        {
            var selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath)) return false;
            return DirectoryMoveService.CanMoveToPackages(selectedPath, out _);
        }

        [MenuItem(UPMConstants.ContextMenuRoot + "转换到 Assets", false, MenuPriority + 1)]
        private static void MoveToAssets()
        {
            var selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath)) return;

            var targetPath = DirectoryMoveService.PreviewTargetPath(selectedPath, false);

            if (EditorUtility.DisplayDialog(
                "转换到 Assets",
                $"从:\n{selectedPath}\n\n移动到:\n{targetPath}\n\n此操作不可撤销",
                "转换", "取消"))
            {
                var result = DirectoryMoveService.MoveToAssets(selectedPath);
                if (!result.Success)
                {
                    EditorUtility.DisplayDialog("错误", result.ErrorMessage, "确定");
                }
            }
        }

        [MenuItem(UPMConstants.ContextMenuRoot + "转换到 Assets", true)]
        private static bool MoveToAssetsValidation()
        {
            var selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath)) return false;
            return DirectoryMoveService.CanMoveToAssets(selectedPath, out _);
        }

        #endregion

        #region Create

        [MenuItem(UPMConstants.ContextMenuRoot + "在此创建包", false, MenuPriority + 10)]
        private static void CreatePackageHere()
        {
            var selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath)) return;
            UPMEditorWindow.CreatePackageAt(selectedPath);
        }

        [MenuItem(UPMConstants.ContextMenuRoot + "在此创建包", true)]
        private static bool CreatePackageHereValidation()
        {
            var selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath)) return false;
            return !UPMPackageValidator.HasValidPackageJson(selectedPath);
        }

        #endregion

        #region Helper

        private static string GetSelectedFolderPath()
        {
            if (Selection.activeObject == null) return null;

            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path)) return null;

            if (AssetDatabase.IsValidFolder(path))
                return path;

            if (path.StartsWith("Packages/"))
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                if (System.IO.Directory.Exists(fullPath))
                    return path;
            }

            return null;
        }

        #endregion
    }
}
#endif
