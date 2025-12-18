using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Puffin.Runtime.Tools
{
    public class PuffinLogger : IPuffinLogger
    {
        private readonly StringBuilder _sb = new();
        private readonly Stack<int> _colorStack = new();
        private LogSettings _settings;

        private static readonly Color DefaultInfoColor = new(1f, 0.88f, 0.73f);
        private static readonly Color DefaultTagColor = new(0.42f, 1f, 0.5f);
        private static readonly Color DefaultWarningColor = new(1f, 1f, 0.51f);
        private static readonly Color DefaultErrorColor = new(1f, 0.18f, 0.11f);

        private LogSettings Settings => _settings ??= LogSettings.Instance;

        private bool ShouldLog(LogLevel level)
        {
            if (Settings == null) return true;
            return level >= Settings.GetEffectiveLogLevel();
        }

        private bool UseColors
        {
            get
            {
                if (Settings == null) return true;
                var platformConfig = Settings.GetCurrentPlatformConfig();
                return platformConfig?.enableColors ?? Settings.enableColors;
            }
        }

        private bool UseStackTrace
        {
            get
            {
                if (Settings == null) return true;
                var platformConfig = Settings.GetCurrentPlatformConfig();
                return platformConfig?.enableStackTrace ?? Settings.enableStackTrace;
            }
        }

        private Color InfoColor(int style) => Settings != null ? Settings.GetInfoColor(style) : DefaultInfoColor;
        private Color TagColor => Settings != null ? Settings.tagColor : DefaultTagColor;
        private Color WarnColor => Settings != null ? Settings.warningColor : DefaultWarningColor;
        private Color ErrColor => Settings != null ? Settings.errorColor : DefaultErrorColor;

        public void BeginColor(int i) => _colorStack.Push(i);

        public void EndColor()
        {
            if (_colorStack.Count > 0) _colorStack.Pop();
        }

        public void Verbose(object message, Object context = null, int colorStyle = 0)
        {
            if (!ShouldLog(LogLevel.Verbose)) return;
            LogInternal("Verbose", message, context, InfoColor(colorStyle));
        }

        public void Info(object message, Object context = null, int colorStyle = 0)
        {
            if (!ShouldLog(LogLevel.Info)) return;
            if (_colorStack.Count > 0) colorStyle = _colorStack.Peek();
            LogInternal("Info", message, context, InfoColor(colorStyle));
        }

        public void Warning(object message, Object context = null)
        {
            if (!ShouldLog(LogLevel.Warning)) return;
            var msg = FormatMessage("Warn", message, WarnColor, WarnColor);
            UnityEngine.Debug.LogWarning(msg, context);
        }

        public void Error(object message, Object context = null)
        {
            if (!ShouldLog(LogLevel.Error)) return;
            var msg = FormatMessage("Error", message, ErrColor, ErrColor);
            UnityEngine.Debug.LogError(msg, context);
        }

        public void Exception(Exception exception)
        {
            if (!ShouldLog(LogLevel.Error)) return;
            UnityEngine.Debug.LogException(exception);
        }

        public void Separator(object message = null, int colorStyle = 0, string separator = "★")
        {
            if (!ShouldLog(LogLevel.Info)) return;
            if (_colorStack.Count > 0) colorStyle = _colorStack.Peek();

            _sb.Clear();
            if (message != null)
            {
                _sb.Append("  ");
                _sb.Append(message);
                _sb.Append("  ");
            }

            var len = GetDisplayLength(_sb);
            while (len < 40)
            {
                _sb.Insert(0, separator);
                _sb.Append(separator);
                len += GetDisplayLength(separator) * 2;
            }

            var caller = UseStackTrace ? GetCallerInfo() : "";
            var tag = string.IsNullOrEmpty(caller) ? "[Info]" : $"[Info][{caller}]";
            var output = $"{Colorize(tag, TagColor)} {Colorize(_sb.ToString(), InfoColor(colorStyle))}";
            UnityEngine.Debug.Log(output);
        }

        public void InfoWithTag(string tag, object message, Object context = null)
        {
            if (!ShouldLog(LogLevel.Info)) return;
            var tagConfig = Settings?.GetTagConfig(tag);
            if (tagConfig != null && !tagConfig.enabled) return;

            var tagColor = tagConfig != null ? tagConfig.color : TagColor;
            var output = $"{Colorize($"[{tag}]", tagColor)} {Colorize(message?.ToString() ?? "null", InfoColor(0))}";
            UnityEngine.Debug.Log(output, context);
        }

        public void WarningWithTag(string tag, object message, Object context = null)
        {
            if (!ShouldLog(LogLevel.Warning)) return;
            var tagConfig = Settings?.GetTagConfig(tag);
            if (tagConfig != null && !tagConfig.enabled) return;

            var tagColor = tagConfig != null ? tagConfig.color : WarnColor;
            var output = $"{Colorize($"[{tag}]", tagColor)} {Colorize(message?.ToString() ?? "null", tagColor)}";
            UnityEngine.Debug.LogWarning(output, context);
        }

        public void ErrorWithTag(string tag, object message, Object context = null)
        {
            if (!ShouldLog(LogLevel.Error)) return;
            var tagConfig = Settings?.GetTagConfig(tag);
            if (tagConfig != null && !tagConfig.enabled) return;

            var output = $"{Colorize($"[{tag}]", ErrColor)} {Colorize(message?.ToString() ?? "null", ErrColor)}";
            UnityEngine.Debug.LogError(output, context);
        }

        public void LogCollection(string name, IEnumerable collection, Object context = null, int colorStyle = 0)
        {
            if (!ShouldLog(LogLevel.Info)) return;
            if (collection == null)
            {
                Info($"{name}: null", context, colorStyle);
                return;
            }

            _sb.Clear();
            _sb.Append(name);
            _sb.Append(": ");

            var maxElements = Settings?.maxCollectionElements ?? 20;
            var count = 0;

            if (collection is IDictionary dict)
            {
                _sb.Append("{\n");
                foreach (DictionaryEntry entry in dict)
                {
                    if (count >= maxElements)
                    {
                        _sb.Append($"  ... ({dict.Count} total)\n");
                        break;
                    }

                    _sb.Append($"  [{entry.Key}] = {entry.Value}\n");
                    count++;
                }

                _sb.Append("}");
            }
            else
            {
                _sb.Append("[\n");
                foreach (var item in collection)
                {
                    if (count >= maxElements)
                    {
                        _sb.Append("  ... (more items)\n");
                        break;
                    }

                    _sb.Append($"  [{count}] {item}\n");
                    count++;
                }

                _sb.Append("]");
            }

            Info(_sb.ToString(), context, colorStyle);
        }

        private void LogInternal(string level, object message, Object context, Color msgColor)
        {
            var output = FormatMessage(level, message, TagColor, msgColor);
            UnityEngine.Debug.Log(output, context);
        }

        private string FormatMessage(string level, object message, Color tagColor, Color msgColor)
        {
            var caller = UseStackTrace ? GetCallerInfo() : "";
            var tag = string.IsNullOrEmpty(caller) ? $"[{level}]" : $"[{level}][{caller}]";
            return $"{Colorize(tag, tagColor)} {Colorize(message?.ToString() ?? "null", msgColor)}";
        }

        private string GetCallerInfo()
        {
            // 跳过前2帧(GetCallerInfo和调用者)，不获取文件信息以提升性能
            var stackTrace = new StackTrace(2, false);
            for (int i = 0; i < Math.Min(stackTrace.FrameCount, 10); i++)
            {
                var frame = stackTrace.GetFrame(i);
                var method = frame?.GetMethod();
                var type = method?.ReflectedType;
                if (type != null && type != typeof(PuffinLogger) && type != typeof(Log))
                    return $"{type.Name}.{method.Name}";
            }

            return "";
        }

        private string Colorize(string text, Color color)
        {
#if UNITY_EDITOR
            if (!UseColors || string.IsNullOrEmpty(text)) return text;
            return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
#else
            return text;
#endif
        }

        private int GetDisplayLength(string str)
        {
            int len = 0;
            foreach (var c in str)
                len += c > 127 ? 2 : 1;
            return len;
        }

        private int GetDisplayLength(StringBuilder sb)
        {
            int len = 0;
            for (int i = 0; i < sb.Length; i++)
                len += sb[i] > 127 ? 2 : 1;
            return len;
        }
    }
}