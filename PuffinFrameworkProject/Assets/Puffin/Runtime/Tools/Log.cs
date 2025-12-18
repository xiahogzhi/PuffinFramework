using System;
using System.Collections;
using System.Collections.Generic;
using Puffin.Runtime.Core;
using Puffin.Runtime.Interfaces;
using Object = UnityEngine.Object;

namespace Puffin.Runtime.Tools
{
    public static class Log
    {
        public static IPuffinLogger Logger => PuffinFramework.Logger;

        public static void Verbose(object message, Object context = null, int colorStyle = 0)
        {
            Logger.Verbose(message, context, colorStyle);
        }

        public static void Info(object message, Object context = null, int colorStyle = 0)
        {
            Logger.Info(message, context, colorStyle);
        }

        public static void Warning(object message, Object context = null)
        {
            Logger.Warning(message, context);
        }

        public static void Error(object message, Object context = null)
        {
            Logger.Error(message, context);
        }

        public static void Exception(Exception exception)
        {
            Logger.Exception(exception);
        }

        public static void Separator(object message = null, int colorStyle = 0, string separator = "★")
        {
            Logger.Separator(message, colorStyle, separator);
        }

        public static void BeginColor(int colorStyle)
        {
            Logger.BeginColor(colorStyle);
        }

        public static void EndColor()
        {
            Logger.EndColor();
        }

        // 带标签的日志
        public static void InfoWithTag(string tag, object message, Object context = null)
        {
            Logger.InfoWithTag(tag, message, context);
        }

        public static void WarningWithTag(string tag, object message, Object context = null)
        {
            Logger.WarningWithTag(tag, message, context);
        }

        public static void ErrorWithTag(string tag, object message, Object context = null)
        {
            Logger.ErrorWithTag(tag, message, context);
        }

        // 集合输出
        public static void LogCollection(string name, IEnumerable collection, Object context = null, int colorStyle = 0)
        {
            Logger.LogCollection(name, collection, context, colorStyle);
        }

        // 便捷方法：输出 List
        public static void LogList<T>(string name, IList<T> list, Object context = null, int colorStyle = 0)
        {
            Logger.LogCollection(name, list, context, colorStyle);
        }

        // 便捷方法：输出 Dictionary
        public static void LogDict<TKey, TValue>(string name, IDictionary<TKey, TValue> dict, Object context = null, int colorStyle = 0)
        {
            Logger.LogCollection(name, (IEnumerable)dict, context, colorStyle);
        }
    }
}
