#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Puffin.Editor.Hub.Data
{
    /// <summary>
    /// 已安装模块锁定记录
    /// </summary>
    [Serializable]
    public class InstalledModuleLock
    {
        public string moduleId;
        public string version;
        public string registryId;
        public string checksum;
        public string installedAt;
        public List<string> resolvedDependencies;  // moduleId@version
    }

    /// <summary>
    /// 已安装模块锁定文件
    /// </summary>
    [Serializable]
    public class InstalledModulesLock
    {
        public List<InstalledModuleLock> modules = new();

        private static InstalledModulesLock _instance;

        public static InstalledModulesLock Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }

        public static InstalledModulesLock Load()
        {
            var path = HubSettings.LockFilePath;
            if (!File.Exists(path))
                return new InstalledModulesLock();

            try
            {
                var json = File.ReadAllText(path);
                return JsonUtility.FromJson<InstalledModulesLock>(json) ?? new InstalledModulesLock();
            }
            catch
            {
                return new InstalledModulesLock();
            }
        }

        public void Save()
        {
            var path = HubSettings.LockFilePath;
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonUtility.ToJson(this, true);
            File.WriteAllText(path, json);
        }

        public InstalledModuleLock GetModule(string moduleId)
        {
            return modules.Find(m => m.moduleId == moduleId);
        }

        public bool IsInstalled(string moduleId)
        {
            return modules.Exists(m => m.moduleId == moduleId);
        }

        public string GetInstalledVersion(string moduleId)
        {
            return GetModule(moduleId)?.version;
        }

        public void AddOrUpdate(InstalledModuleLock module)
        {
            modules.RemoveAll(m => m.moduleId == module.moduleId);
            modules.Add(module);
            Save();
        }

        public void Remove(string moduleId)
        {
            modules.RemoveAll(m => m.moduleId == moduleId);
            Save();
        }

        public static void Reload()
        {
            _instance = Load();
        }
    }
}
#endif
