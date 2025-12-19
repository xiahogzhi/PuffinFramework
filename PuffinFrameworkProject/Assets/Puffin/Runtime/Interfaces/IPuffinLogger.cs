using System;
using System.Collections;
using Object = UnityEngine.Object;

namespace Puffin.Runtime.Interfaces
{
    /// <summary>
    /// 框架日志接口，定义日志输出的标准方法
    /// 可通过实现此接口自定义日志输出方式
    /// </summary>
    public interface IPuffinLogger
    {
        /// <summary>
        /// 输出详细日志（最低级别）
        /// </summary>
        void Verbose(object message, Object context = null, int colorStyle = 0);

        /// <summary>
        /// 输出信息日志
        /// </summary>
        void Info(object message, Object context = null, int colorStyle = 0);

        /// <summary>
        /// 输出警告日志
        /// </summary>
        void Warning(object message, Object context = null);

        /// <summary>
        /// 输出错误日志
        /// </summary>
        void Error(object message, Object context = null);

        /// <summary>
        /// 输出异常日志
        /// </summary>
        void Exception(Exception exception);

        /// <summary>
        /// 输出分隔线日志
        /// </summary>
        void Separator(object message = null, int colorStyle = 0, string separator = "★");

        /// <summary>
        /// 开始颜色样式
        /// </summary>
        void BeginColor(int colorStyle);

        /// <summary>
        /// 结束颜色样式
        /// </summary>
        void EndColor();

        /// <summary>
        /// 输出带标签的信息日志
        /// </summary>
        void InfoWithTag(string tag, object message, Object context = null);

        /// <summary>
        /// 输出带标签的警告日志
        /// </summary>
        void WarningWithTag(string tag, object message, Object context = null);

        /// <summary>
        /// 输出带标签的错误日志
        /// </summary>
        void ErrorWithTag(string tag, object message, Object context = null);

        /// <summary>
        /// 输出集合内容日志
        /// </summary>
        void LogCollection(string name, IEnumerable collection, Object context = null, int colorStyle = 0);
    }
}
