#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace PuffinGames.UPMEditor
{
    /// <summary>
    /// Service for reading and writing package.json files
    /// </summary>
    public static class PackageJsonService
    {
        /// <summary>
        /// Read package.json from directory
        /// </summary>
        public static UPMPackageData ReadPackageJson(string directoryPath)
        {
            var packageJsonPath = Path.Combine(Path.GetFullPath(directoryPath), UPMConstants.PackageJsonFileName);
            if (!File.Exists(packageJsonPath))
                return null;

            try
            {
                var json = File.ReadAllText(packageJsonPath);
                return ParsePackageJson(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to read package.json: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Write package.json to directory
        /// </summary>
        public static bool WritePackageJson(string directoryPath, UPMPackageData data)
        {
            try
            {
                var fullPath = Path.GetFullPath(directoryPath);
                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);

                var packageJsonPath = Path.Combine(fullPath, UPMConstants.PackageJsonFileName);
                var json = SerializePackageJson(data);
                File.WriteAllText(packageJsonPath, json, Encoding.UTF8);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to write package.json: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if package.json exists
        /// </summary>
        public static bool PackageJsonExists(string directoryPath)
        {
            var packageJsonPath = Path.Combine(Path.GetFullPath(directoryPath), UPMConstants.PackageJsonFileName);
            return File.Exists(packageJsonPath);
        }

        /// <summary>
        /// Create default package data
        /// </summary>
        public static UPMPackageData CreateDefaultPackageData(string packageName, string displayName)
        {
            return new UPMPackageData
            {
                name = packageName,
                displayName = displayName,
                version = UPMConstants.DefaultVersion,
                unity = UPMConstants.DefaultUnityVersion,
                description = "",
                keywords = new List<string>(),
                author = new UPMPackageData.AuthorInfo(),
                dependencies = new Dictionary<string, string>()
            };
        }

        /// <summary>
        /// Parse JSON string to UPMPackageData (simple parser without external dependencies)
        /// </summary>
        private static UPMPackageData ParsePackageJson(string json)
        {
            var data = new UPMPackageData();

            data.name = GetJsonStringValue(json, "name");
            data.displayName = GetJsonStringValue(json, "displayName");
            data.version = GetJsonStringValue(json, "version");
            data.unity = GetJsonStringValue(json, "unity");
            data.description = GetJsonStringValue(json, "description");
            data.documentationUrl = GetJsonStringValue(json, "documentationUrl");
            data.changelogUrl = GetJsonStringValue(json, "changelogUrl");
            data.licensesUrl = GetJsonStringValue(json, "licensesUrl");

            // Parse keywords array
            data.keywords = GetJsonStringArray(json, "keywords");

            // Parse author object
            var authorJson = GetJsonObject(json, "author");
            if (!string.IsNullOrEmpty(authorJson))
            {
                data.author.name = GetJsonStringValue(authorJson, "name");
                data.author.email = GetJsonStringValue(authorJson, "email");
                data.author.url = GetJsonStringValue(authorJson, "url");
            }

            // Parse dependencies object
            var depsJson = GetJsonObject(json, "dependencies");
            if (!string.IsNullOrEmpty(depsJson))
            {
                data.dependencies = ParseDependencies(depsJson);
            }

            return data;
        }

        /// <summary>
        /// Serialize UPMPackageData to JSON string
        /// </summary>
        private static string SerializePackageJson(UPMPackageData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");

            // Required fields
            sb.AppendLine($"    \"name\": \"{EscapeJson(data.name)}\",");
            sb.AppendLine($"    \"displayName\": \"{EscapeJson(data.displayName)}\",");
            sb.AppendLine($"    \"version\": \"{EscapeJson(data.version)}\",");
            sb.AppendLine($"    \"unity\": \"{EscapeJson(data.unity)}\",");
            sb.AppendLine($"    \"description\": \"{EscapeJson(data.description)}\",");

            // Optional URL fields
            if (!string.IsNullOrEmpty(data.documentationUrl))
                sb.AppendLine($"    \"documentationUrl\": \"{EscapeJson(data.documentationUrl)}\",");
            if (!string.IsNullOrEmpty(data.changelogUrl))
                sb.AppendLine($"    \"changelogUrl\": \"{EscapeJson(data.changelogUrl)}\",");
            if (!string.IsNullOrEmpty(data.licensesUrl))
                sb.AppendLine($"    \"licensesUrl\": \"{EscapeJson(data.licensesUrl)}\",");

            // Keywords
            if (data.keywords != null && data.keywords.Count > 0)
            {
                sb.AppendLine("    \"keywords\": [");
                for (int i = 0; i < data.keywords.Count; i++)
                {
                    var comma = i < data.keywords.Count - 1 ? "," : "";
                    sb.AppendLine($"        \"{EscapeJson(data.keywords[i])}\"{comma}");
                }
                sb.AppendLine("    ],");
            }

            // Author
            if (data.author != null && !string.IsNullOrEmpty(data.author.name))
            {
                sb.AppendLine("    \"author\": {");
                var authorFields = new List<string>();
                if (!string.IsNullOrEmpty(data.author.name))
                    authorFields.Add($"        \"name\": \"{EscapeJson(data.author.name)}\"");
                if (!string.IsNullOrEmpty(data.author.email))
                    authorFields.Add($"        \"email\": \"{EscapeJson(data.author.email)}\"");
                if (!string.IsNullOrEmpty(data.author.url))
                    authorFields.Add($"        \"url\": \"{EscapeJson(data.author.url)}\"");
                sb.AppendLine(string.Join(",\n", authorFields));
                sb.AppendLine("    },");
            }

            // Dependencies
            sb.AppendLine("    \"dependencies\": {");
            if (data.dependencies != null && data.dependencies.Count > 0)
            {
                var deps = new List<string>();
                foreach (var dep in data.dependencies)
                {
                    deps.Add($"        \"{EscapeJson(dep.Key)}\": \"{EscapeJson(dep.Value)}\"");
                }
                sb.AppendLine(string.Join(",\n", deps));
            }
            sb.AppendLine("    }");

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GetJsonStringValue(string json, string key)
        {
            var pattern = $"\"{key}\"\\s*:\\s*\"";
            var idx = json.IndexOf($"\"{key}\"");
            if (idx < 0) return "";

            var colonIdx = json.IndexOf(':', idx);
            if (colonIdx < 0) return "";

            var startQuote = json.IndexOf('"', colonIdx + 1);
            if (startQuote < 0) return "";

            var endQuote = startQuote + 1;
            while (endQuote < json.Length)
            {
                if (json[endQuote] == '"' && json[endQuote - 1] != '\\')
                    break;
                endQuote++;
            }

            if (endQuote >= json.Length) return "";
            return UnescapeJson(json.Substring(startQuote + 1, endQuote - startQuote - 1));
        }

        private static List<string> GetJsonStringArray(string json, string key)
        {
            var result = new List<string>();
            var idx = json.IndexOf($"\"{key}\"");
            if (idx < 0) return result;

            var bracketStart = json.IndexOf('[', idx);
            if (bracketStart < 0) return result;

            var bracketEnd = json.IndexOf(']', bracketStart);
            if (bracketEnd < 0) return result;

            var arrayContent = json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            var inString = false;
            var currentString = new StringBuilder();

            for (int i = 0; i < arrayContent.Length; i++)
            {
                var c = arrayContent[i];
                if (c == '"' && (i == 0 || arrayContent[i - 1] != '\\'))
                {
                    if (inString)
                    {
                        result.Add(UnescapeJson(currentString.ToString()));
                        currentString.Clear();
                    }
                    inString = !inString;
                }
                else if (inString)
                {
                    currentString.Append(c);
                }
            }

            return result;
        }

        private static string GetJsonObject(string json, string key)
        {
            var idx = json.IndexOf($"\"{key}\"");
            if (idx < 0) return "";

            var braceStart = json.IndexOf('{', idx);
            if (braceStart < 0) return "";

            var depth = 1;
            var braceEnd = braceStart + 1;
            while (braceEnd < json.Length && depth > 0)
            {
                if (json[braceEnd] == '{') depth++;
                else if (json[braceEnd] == '}') depth--;
                braceEnd++;
            }

            if (depth != 0) return "";
            return json.Substring(braceStart, braceEnd - braceStart);
        }

        private static Dictionary<string, string> ParseDependencies(string depsJson)
        {
            var result = new Dictionary<string, string>();
            var content = depsJson.Trim().TrimStart('{').TrimEnd('}');

            var inKey = false;
            var inValue = false;
            var currentKey = new StringBuilder();
            var currentValue = new StringBuilder();

            for (int i = 0; i < content.Length; i++)
            {
                var c = content[i];
                if (c == '"')
                {
                    if (!inKey && !inValue)
                    {
                        inKey = true;
                    }
                    else if (inKey)
                    {
                        inKey = false;
                    }
                    else if (inValue)
                    {
                        result[currentKey.ToString()] = currentValue.ToString();
                        currentKey.Clear();
                        currentValue.Clear();
                        inValue = false;
                    }
                }
                else if (c == ':' && !inKey && !inValue)
                {
                    // Skip to value
                }
                else if (c == '"' || inKey)
                {
                    if (inKey && c != '"')
                        currentKey.Append(c);
                }
                else if (inValue)
                {
                    currentValue.Append(c);
                }
                else if (c == '"')
                {
                    inValue = true;
                }
            }

            return result;
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static string UnescapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
}
#endif
