#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace PuffinGames.UPMEditor
{
    /// <summary>
    /// Data model for package.json
    /// </summary>
    [Serializable]
    public class UPMPackageData
    {
        public string name = "";
        public string displayName = "";
        public string version = UPMConstants.DefaultVersion;
        public string unity = UPMConstants.DefaultUnityVersion;
        public string description = "";
        public string documentationUrl = "";
        public string changelogUrl = "";
        public string licensesUrl = "";
        public List<string> keywords = new List<string>();
        public AuthorInfo author = new AuthorInfo();
        public Dictionary<string, string> dependencies = new Dictionary<string, string>();

        [Serializable]
        public class AuthorInfo
        {
            public string name = "";
            public string email = "";
            public string url = "";
        }

        /// <summary>
        /// Create a copy of this package data
        /// </summary>
        public UPMPackageData Clone()
        {
            var clone = new UPMPackageData
            {
                name = name,
                displayName = displayName,
                version = version,
                unity = unity,
                description = description,
                documentationUrl = documentationUrl,
                changelogUrl = changelogUrl,
                licensesUrl = licensesUrl,
                keywords = new List<string>(keywords),
                author = new AuthorInfo
                {
                    name = author.name,
                    email = author.email,
                    url = author.url
                },
                dependencies = new Dictionary<string, string>(dependencies)
            };
            return clone;
        }
    }

    /// <summary>
    /// Template options for package creation
    /// </summary>
    [Serializable]
    public class PackageTemplateOptions
    {
        public bool createRuntime = true;
        public bool createEditor = true;
        public bool createReadme = true;
        public bool createChangelog = false;
        public bool createLicense = false;
        public bool createTests = false;
        public bool createDocumentation = false;
    }
}
#endif
