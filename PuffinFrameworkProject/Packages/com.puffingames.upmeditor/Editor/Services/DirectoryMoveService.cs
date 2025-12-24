#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PuffinGames.UPMEditor
{
    /// <summary>
    /// Service for moving directories between Assets and Packages
    /// </summary>
    public static class DirectoryMoveService
    {
        /// <summary>
        /// Move directory from Assets to Packages
        /// </summary>
        public static MoveResult MoveToPackages(string assetPath)
        {
            var result = new MoveResult { SourcePath = assetPath };

            if (!CanMoveToPackages(assetPath, out string reason))
            {
                result.Success = false;
                result.ErrorMessage = reason;
                return result;
            }

            // Get package name from package.json or directory name
            string packageName = GetPackageName(assetPath);
            string targetPath = Path.Combine(UPMConstants.PackagesPath, packageName);

            // Check target doesn't exist
            if (Directory.Exists(Path.GetFullPath(targetPath)))
            {
                result.Success = false;
                result.ErrorMessage = $"Target already exists: {targetPath}";
                return result;
            }

            return PerformMove(assetPath, targetPath, result);
        }

        /// <summary>
        /// Move directory from Packages to Assets
        /// </summary>
        public static MoveResult MoveToAssets(string packagePath)
        {
            var result = new MoveResult { SourcePath = packagePath };

            if (!CanMoveToAssets(packagePath, out string reason))
            {
                result.Success = false;
                result.ErrorMessage = reason;
                return result;
            }

            // Get directory name
            string dirName = Path.GetFileName(packagePath);
            string targetPath = Path.Combine(UPMConstants.AssetsPath, dirName);

            // Check target doesn't exist
            if (Directory.Exists(Path.GetFullPath(targetPath)) || AssetDatabase.IsValidFolder(targetPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Target already exists: {targetPath}";
                return result;
            }

            return PerformMove(packagePath, targetPath, result);
        }

        /// <summary>
        /// Preview target path for move operation
        /// </summary>
        public static string PreviewTargetPath(string sourcePath, bool toPackages)
        {
            if (toPackages)
            {
                string packageName = GetPackageName(sourcePath);
                return Path.Combine(UPMConstants.PackagesPath, packageName);
            }
            else
            {
                string dirName = Path.GetFileName(sourcePath);
                return Path.Combine(UPMConstants.AssetsPath, dirName);
            }
        }

        /// <summary>
        /// Check if directory can be moved to Packages
        /// </summary>
        public static bool CanMoveToPackages(string path, out string reason)
        {
            reason = null;

            if (string.IsNullOrEmpty(path))
            {
                reason = "Path is empty";
                return false;
            }

            if (!UPMPackageValidator.IsInAssetsFolder(path))
            {
                reason = "Source must be in Assets folder";
                return false;
            }

            if (!Directory.Exists(Path.GetFullPath(path)))
            {
                reason = "Directory does not exist";
                return false;
            }

            if (!UPMPackageValidator.HasValidPackageJson(path))
            {
                reason = "Directory must contain a valid package.json";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if directory can be moved to Assets
        /// </summary>
        public static bool CanMoveToAssets(string path, out string reason)
        {
            reason = null;

            if (string.IsNullOrEmpty(path))
            {
                reason = "Path is empty";
                return false;
            }

            if (!UPMPackageValidator.IsInPackagesFolder(path))
            {
                reason = "Source must be in Packages folder";
                return false;
            }

            if (!UPMPackageValidator.IsLocalPackage(path))
            {
                reason = "Only local (embedded) packages can be moved";
                return false;
            }

            if (!Directory.Exists(Path.GetFullPath(path)))
            {
                reason = "Directory does not exist";
                return false;
            }

            return true;
        }

        private static MoveResult PerformMove(string sourcePath, string targetPath, MoveResult result)
        {
            result.TargetPath = targetPath;

            try
            {
                var sourceFullPath = Path.GetFullPath(sourcePath);
                var targetFullPath = Path.GetFullPath(targetPath);

                // Perform the move
                Directory.Move(sourceFullPath, targetFullPath);

                // Delete meta file if exists (for Assets folder)
                var metaPath = sourceFullPath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }

                // Refresh AssetDatabase
                AssetDatabase.Refresh();

                result.Success = true;
                Debug.Log($"Successfully moved package from {sourcePath} to {targetPath}");
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;
                Debug.LogError($"Failed to move package: {e.Message}");
            }

            return result;
        }

        private static string GetPackageName(string path)
        {
            // Try to get name from package.json
            var packageData = PackageJsonService.ReadPackageJson(path);
            if (packageData != null && !string.IsNullOrEmpty(packageData.name))
            {
                return packageData.name;
            }

            // Fall back to directory name
            return Path.GetFileName(path);
        }
    }

    /// <summary>
    /// Result of a move operation
    /// </summary>
    public class MoveResult
    {
        public bool Success;
        public string SourcePath;
        public string TargetPath;
        public string ErrorMessage;
    }
}
#endif
