using System;
using System.Collections;
using Object = UnityEngine.Object;

namespace Puffin.Runtime.Interfaces
{
    /// <summary>
    /// 日志接口
    /// </summary>
    public interface IPuffinLogger
    {
        void Verbose(object message, Object context = null, int colorStyle = 0);
        void Info(object message, Object context = null, int colorStyle = 0);
        void Warning(object message, Object context = null);
        void Error(object message, Object context = null);
        void Exception(Exception exception);
        void Separator(object message = null, int colorStyle = 0, string separator = "★");
        void BeginColor(int colorStyle);
        void EndColor();

        // 带标签的日志
        void InfoWithTag(string tag, object message, Object context = null);
        void WarningWithTag(string tag, object message, Object context = null);
        void ErrorWithTag(string tag, object message, Object context = null);

        // 集合输出
        void LogCollection(string name, IEnumerable collection, Object context = null, int colorStyle = 0);
    }
}
