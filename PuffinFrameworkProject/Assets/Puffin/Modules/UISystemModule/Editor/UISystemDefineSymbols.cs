#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;

namespace XFrameworks.Systems.UISystems.Editor
{
    /// <summary>
    /// 自动检测DOTween和Odin并添加宏定义
    /// </summary>
    [InitializeOnLoad]
    public static class UISystemDefineSymbols
    {
        private const string DOTWEEN_SYMBOL = "DOTWEEN";
        private const string ODIN_SYMBOL = "ODIN_INSPECTOR";

        static UISystemDefineSymbols()
        {
            CheckAndUpdateSymbols();
        }

        [MenuItem("Puffin/UI System/Refresh Define Symbols")]
        public static void CheckAndUpdateSymbols()
        {
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (targetGroup == BuildTargetGroup.Unknown)
                return;

            var currentSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            var symbolList = currentSymbols.Split(';').ToList();
            bool changed = false;

            // 检测 DOTween
            bool hasDOTween = HasDOTween();
            if (hasDOTween && !symbolList.Contains(DOTWEEN_SYMBOL))
            {
                symbolList.Add(DOTWEEN_SYMBOL);
                changed = true;
            }
            else if (!hasDOTween && symbolList.Contains(DOTWEEN_SYMBOL))
            {
                symbolList.Remove(DOTWEEN_SYMBOL);
                changed = true;
            }

            // 检测 Odin Inspector
            bool hasOdin = HasOdinInspector();
            if (hasOdin && !symbolList.Contains(ODIN_SYMBOL))
            {
                symbolList.Add(ODIN_SYMBOL);
                changed = true;
            }
            else if (!hasOdin && symbolList.Contains(ODIN_SYMBOL))
            {
                symbolList.Remove(ODIN_SYMBOL);
                changed = true;
            }

            if (changed)
            {
                var newSymbols = string.Join(";", symbolList.Where(s => !string.IsNullOrEmpty(s)));
                PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newSymbols);
            }
        }

        private static bool HasDOTween()
        {
            // 检查 DOTween.dll 是否存在
            var dllFiles = Directory.GetFiles(UnityEngine.Application.dataPath, "DOTween.dll", SearchOption.AllDirectories);
            if (dllFiles.Any(f => !f.Contains("~")))
                return true;

            // 检查 DOTween 程序集定义
            var asmdefFiles = Directory.GetFiles(UnityEngine.Application.dataPath, "*.asmdef", SearchOption.AllDirectories);
            foreach (var file in asmdefFiles)
            {
                if (file.Contains("~")) continue;
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.Contains("DOTween"))
                    return true;
            }

            return false;
        }

        private static bool HasOdinInspector()
        {
            // 检查 Odin DLL 是否存在
            var dllFiles = Directory.GetFiles(UnityEngine.Application.dataPath, "Sirenix.OdinInspector*.dll", SearchOption.AllDirectories);
            if (dllFiles.Any(f => !f.Contains("~")))
                return true;

            // 检查 Odin 程序集定义
            var asmdefFiles = Directory.GetFiles(UnityEngine.Application.dataPath, "*.asmdef", SearchOption.AllDirectories);
            foreach (var file in asmdefFiles)
            {
                if (file.Contains("~")) continue;
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.Contains("Sirenix") || fileName.Contains("OdinInspector"))
                    return true;
            }

            return false;
        }
    }
}
#endif
