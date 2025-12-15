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
        public bool isDisabled;  // 是否被禁用
        public bool isManuallyDisabled;  // 是否是用户手动禁用（不会自动启用）
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

        public bool IsDisabled(string moduleId)
        {
            var module = GetModule(moduleId);
            return module != null && module.isDisabled;
        }

        public void SetDisabled(string moduleId, bool disabled, bool isManual = false)
        {
            var module = GetModule(moduleId);
            if (module == null)
            {
                // 本地模块没有锁定记录，创建一个
                module = new InstalledModuleLock
                {
                    moduleId = moduleId,
                    registryId = "local",
                    installedAt = DateTime.Now.ToString("o")
                };
                modules.Add(module);
            }

            if (module.isDisabled != disabled || module.isManuallyDisabled != isManual)
            {
                module.isDisabled = disabled;
                if (disabled)
                    module.isManuallyDisabled = isManual;
                else
                    module.isManuallyDisabled = false;
                Save();
            }
        }

        public bool IsManuallyDisabled(string moduleId)
        {
            var module = GetModule(moduleId);
            return module != null && module.isManuallyDisabled;
        }

        public List<InstalledModuleLock> GetDisabledModules()
        {
            return modules.FindAll(m => m.isDisabled);
        }
    }
}
#endif
