using System;
using System.Collections;
using System.Collections.Generic;
using Puffin.Runtime.Core;
using Puffin.Runtime.Interfaces;
using Object = UnityEngine.Object;

namespace Puffin.Runtime.Tools
{
    /// <summary>
    /// 日志工具类，提供静态方法访问框架日志系统
    /// 是 PuffinFramework.Logger 的便捷封装
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// 获取当前日志记录器实例
        /// </summary>
        public static IPuffinLogger Logger => PuffinFramework.Logger;

        /// <summary>
        /// 输出详细日志
        /// </summary>
        public static void Verbose(object message, Object context = null, int colorStyle = 0)
        {
            Logger.Verbose(message, context, colorStyle);
        }

        /// <summary>
        /// 输出信息日志
        /// </summary>
        public static void Info(object message, Object context = null, int colorStyle = 0)
        {
            Logger.Info(message, context, colorStyle);
        }

        /// <summary>
        /// 输出警告日志
        /// </summary>
        public static void Warning(object message, Object context = null)
        {
            Logger.Warning(message, context);
        }

        /// <summary>
        /// 输出错误日志
        /// </summary>
        public static void Error(object message, Object context = null)
        {
            Logger.Error(message, context);
        }

        /// <summary>
        /// 输出异常日志
        /// </summary>
        public static void Exception(Exception exception)
        {
            Logger.Exception(exception);
        }

        /// <summary>
        /// 输出分隔线日志
        /// </summary>
        public static void Separator(object message = null, int colorStyle = 0, string separator = "★")
        {
            Logger.Separator(message, colorStyle, separator);
        }

        /// <summary>
        /// 开始颜色样式
        /// </summary>
        public static void BeginColor(int colorStyle)
        {
            Logger.BeginColor(colorStyle);
        }

        /// <summary>
        /// 结束颜色样式
        /// </summary>
        public static void EndColor()
        {
            Logger.EndColor();
        }

        /// <summary>
        /// 输出带标签的信息日志
        /// </summary>
        public static void InfoWithTag(string tag, object message, Object context = null)
        {
            Logger.InfoWithTag(tag, message, context);
        }

        /// <summary>
        /// 输出带标签的警告日志
        /// </summary>
        public static void WarningWithTag(string tag, object message, Object context = null)
        {
            Logger.WarningWithTag(tag, message, context);
        }

        /// <summary>
        /// 输出带标签的错误日志
        /// </summary>
        public static void ErrorWithTag(string tag, object message, Object context = null)
        {
            Logger.ErrorWithTag(tag, message, context);
        }

        /// <summary>
        /// 输出集合内容日志
        /// </summary>
        public static void LogCollection(string name, IEnumerable collection, Object context = null, int colorStyle = 0)
        {
            Logger.LogCollection(name, collection, context, colorStyle);
        }

        /// <summary>
        /// 输出 List 内容日志
        /// </summary>
        public static void LogList<T>(string name, IList<T> list, Object context = null, int colorStyle = 0)
        {
            Logger.LogCollection(name, list, context, colorStyle);
        }

        /// <summary>
        /// 输出 Dictionary 内容日志
        /// </summary>
        public static void LogDict<TKey, TValue>(string name, IDictionary<TKey, TValue> dict, Object context = null, int colorStyle = 0)
        {
            Logger.LogCollection(name, (IEnumerable)dict, context, colorStyle);
        }
    }
}
