#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PuffinGames.UPMEditor
{
    /// <summary>
    /// Validation utilities for UPM packages
    /// </summary>
    public static class UPMPackageValidator
    {
        private static readonly Regex PackageNameRegex = new Regex(@"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$");
        private static readonly Regex SemverRegex = new Regex(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?(\+[a-zA-Z0-9.-]+)?$");

        /// <summary>
        /// Check if directory contains a valid package.json
        /// </summary>
        public static bool HasValidPackageJson(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return false;

            var fullPath = Path.GetFullPath(directoryPath);
            var packageJsonPath = Path.Combine(fullPath, UPMConstants.PackageJsonFileName);
            return File.Exists(packageJsonPath);
        }

        /// <summary>
        /// Validate package name format (reverse domain notation)
        /// </summary>
        public static bool IsValidPackageName(string name, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(name))
            {
                error = "Package name cannot be empty";
                return false;
            }

            if (name != name.ToLowerInvariant())
            {
                error = "Package name must be lowercase";
                return false;
            }

            if (!PackageNameRegex.IsMatch(name))
            {
                error = "Package name must follow reverse domain notation (e.g., com.company.package)";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate version format (semver)
        /// </summary>
        public static bool IsValidVersion(string version, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(version))
            {
                error = "Version cannot be empty";
                return false;
            }

            if (!SemverRegex.IsMatch(version))
            {
                error = "Version must follow semantic versioning (e.g., 1.0.0)";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if path is in Assets folder
        /// </summary>
        public static bool IsInAssetsFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var normalized = path.Replace("\\", "/");
            return normalized.StartsWith(UPMConstants.AssetsPath + "/") || normalized == UPMConstants.AssetsPath;
        }

        /// <summary>
        /// Check if path is in Packages folder
        /// </summary>
        public static bool IsInPackagesFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var normalized = path.Replace("\\", "/");
            return normalized.StartsWith(UPMConstants.PackagesPath + "/") || normalized == UPMConstants.PackagesPath;
        }

        /// <summary>
        /// Check if a package is a local (embedded) package
        /// </summary>
        public static bool IsLocalPackage(string packagePath)
        {
            if (!IsInPackagesFolder(packagePath)) return false;

            var fullPath = Path.GetFullPath(packagePath).Replace("\\", "/").TrimEnd('/');
            var packagesFullPath = Path.GetFullPath(UPMConstants.PackagesPath).Replace("\\", "/").TrimEnd('/');

            // Local packages are directories directly under Packages/
            var parentDir = Path.GetDirectoryName(fullPath)?.Replace("\\", "/");
            return Directory.Exists(fullPath) && parentDir == packagesFullPath;
        }

        /// <summary>
        /// Validate entire package data
        /// </summary>
        public static ValidationResult ValidatePackageData(UPMPackageData data)
        {
            var result = new ValidationResult();

            if (!IsValidPackageName(data.name, out string nameError))
                result.Errors.Add(nameError);

            if (string.IsNullOrEmpty(data.displayName))
                result.Warnings.Add("Display name is empty");

            if (!IsValidVersion(data.version, out string versionError))
                result.Errors.Add(versionError);

            if (string.IsNullOrEmpty(data.unity))
                result.Warnings.Add("Unity version is not specified");

            if (string.IsNullOrEmpty(data.description))
                result.Warnings.Add("Description is empty");

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        /// <summary>
        /// Convert package name to namespace
        /// </summary>
        public static string PackageNameToNamespace(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return "";

            var parts = packageName.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
                    parts[i] = parts[i].Replace("-", "");
                }
            }
            return string.Join(".", parts);
        }
    }

    /// <summary>
    /// Result of package validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid = true;
        public System.Collections.Generic.List<string> Errors = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> Warnings = new System.Collections.Generic.List<string>();
    }
}
#endif
