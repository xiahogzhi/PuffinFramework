using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Puffin.Runtime.Tools
{
    /// <summary>
    /// 高性能低GC的JSON解析器
    /// 延迟解析 - 只在访问时才解析值
    /// 零拷贝读取 - 使用索引引用原始字符串
    /// </summary>
    public readonly struct JsonValue
    {
        private readonly string _source;
        private readonly int _start;
        private readonly int _end;
        private readonly JsonType _type;

        public JsonType Type => _type;
        public bool IsNull => _type == JsonType.Null;
        public bool IsValid => _type != JsonType.Invalid;

        internal JsonValue(string source, int start, int end, JsonType type)
        {
            _source = source;
            _start = start;
            _end = end;
            _type = type;
        }

        public static JsonValue Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new JsonValue(null, 0, 0, JsonType.Null);
            return new JsonParser(json).ParseValue();
        }

        public string AsString()
        {
            if (_type != JsonType.String || _source == null) return null;
            return UnescapeString(_source, _start + 1, _end - 1);
        }

        public string AsRawString()
        {
            if (_type != JsonType.String || _source == null) return null;
            return _source.Substring(_start + 1, _end - _start - 2);
        }

        public int AsInt(int defaultValue = 0)
        {
            if (_type != JsonType.Number || _source == null) return defaultValue;
            return int.TryParse(_source.Substring(_start, _end - _start), out var r) ? r : defaultValue;
        }

        public long AsLong(long defaultValue = 0)
        {
            if (_type != JsonType.Number || _source == null) return defaultValue;
            return long.TryParse(_source.Substring(_start, _end - _start), out var r) ? r : defaultValue;
        }

        public float AsFloat(float defaultValue = 0)
        {
            if (_type != JsonType.Number || _source == null) return defaultValue;
            return float.TryParse(_source.Substring(_start, _end - _start), out var r) ? r : defaultValue;
        }

        public bool AsBool(bool defaultValue = false)
        {
            if (_type != JsonType.Boolean || _source == null) return defaultValue;
            return _source[_start] == 't';
        }

        public JsonValue this[string key]
        {
            get
            {
                if (_type != JsonType.Object || _source == null) return default;
                var parser = new JsonParser(_source, _start + 1);
                while (parser.Position < _end - 1)
                {
                    parser.SkipWhitespace();
                    if (parser.Position >= _end - 1) break;
                    var keyValue = parser.ParseValue();
                    if (keyValue.Type != JsonType.String) break;
                    parser.SkipWhitespace();
                    if (!parser.Expect(':')) break;
                    parser.SkipWhitespace();
                    var value = parser.ParseValue();
                    if (KeyEquals(keyValue, key)) return value;
                    parser.SkipWhitespace();
                    if (parser.Current == ',') parser.Advance();
                }

                return default;
            }
        }

        public JsonValue this[int index]
        {
            get
            {
                if (_type != JsonType.Array || _source == null) return default;
                var parser = new JsonParser(_source, _start + 1);
                var i = 0;
                while (parser.Position < _end - 1)
                {
                    parser.SkipWhitespace();
                    if (parser.Position >= _end - 1) break;
                    var value = parser.ParseValue();
                    if (i == index) return value;
                    i++;
                    parser.SkipWhitespace();
                    if (parser.Current == ',') parser.Advance();
                }

                return default;
            }
        }

        public int ArrayLength
        {
            get
            {
                if (_type != JsonType.Array || _source == null) return 0;
                var parser = new JsonParser(_source, _start + 1);
                var count = 0;
                while (parser.Position < _end - 1)
                {
                    parser.SkipWhitespace();
                    if (parser.Position >= _end - 1) break;
                    parser.ParseValue();
                    count++;
                    parser.SkipWhitespace();
                    if (parser.Current == ',') parser.Advance();
                }

                return count;
            }
        }

        public ObjectEnumerator GetObjectEnumerator() => new ObjectEnumerator(this);
        public ArrayEnumerator GetArrayEnumerator() => new ArrayEnumerator(this);

        public Dictionary<string, JsonValue> ToDictionary()
        {
            if (_type != JsonType.Object) return null;
            var dict = new Dictionary<string, JsonValue>();
            var e = GetObjectEnumerator();
            while (e.MoveNext()) dict[e.Current.key] = e.Current.value;
            return dict;
        }

        public List<string> ToStringList()
        {
            if (_type != JsonType.Array) return null;
            var list = new List<string>();
            var e = GetArrayEnumerator();
            while (e.MoveNext())
                if (e.Current.Type == JsonType.String)
                    list.Add(e.Current.AsString());
            return list;
        }

        private static bool KeyEquals(JsonValue keyValue, string key)
        {
            if (keyValue._source == null || key == null) return false;
            var len = keyValue._end - keyValue._start - 2;
            if (len != key.Length) return false;
            var start = keyValue._start + 1;
            for (var i = 0; i < len; i++)
                if (keyValue._source[start + i] != key[i])
                    return false;
            return true;
        }

        private static string UnescapeString(string source, int start, int end)
        {
            var hasEscape = false;
            for (var i = start; i < end; i++)
                if (source[i] == '\\')
                {
                    hasEscape = true;
                    break;
                }

            if (!hasEscape) return source.Substring(start, end - start);

            var sb = new StringBuilder(end - start);
            for (var i = start; i < end; i++)
            {
                var c = source[i];
                if (c == '\\' && i + 1 < end)
                {
                    c = source[++i];
                    switch (c)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'u':
                            if (i + 4 < end && int.TryParse(source.Substring(i + 1, 4),
                                    System.Globalization.NumberStyles.HexNumber, null, out var code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }

                            break;
                        default: sb.Append(c); break;
                    }
                }
                else sb.Append(c);
            }

            return sb.ToString();
        }

        public override string ToString() => _source?.Substring(_start, _end - _start) ?? "null";
    }

    public enum JsonType : byte
    {
        Invalid = 0,
        Null,
        Boolean,
        Number,
        String,
        Array,
        Object
    }

    public struct ObjectEnumerator
    {
        private readonly string _source;
        private readonly int _end;
        private JsonParser _parser;
        private (string key, JsonValue value) _current;
        private readonly bool _valid;

        internal ObjectEnumerator(JsonValue value)
        {
            _source = value.Type == JsonType.Object ? value.ToString() : null;
            _end = _source?.Length ?? 0;
            _parser = _source != null ? new JsonParser(_source, 1) : default;
            _current = default;
            _valid = _source != null;
        }

        public (string key, JsonValue value) Current => _current;

        public bool MoveNext()
        {
            if (!_valid) return false;
            _parser.SkipWhitespace();
            if (_parser.Position >= _end - 1 || _parser.Current == '}') return false;
            var keyValue = _parser.ParseValue();
            if (keyValue.Type != JsonType.String) return false;
            _parser.SkipWhitespace();
            if (!_parser.Expect(':')) return false;
            _parser.SkipWhitespace();
            _current = (keyValue.AsString(), _parser.ParseValue());
            _parser.SkipWhitespace();
            if (_parser.Current == ',') _parser.Advance();
            return true;
        }
    }

    public struct ArrayEnumerator
    {
        private readonly string _source;
        private readonly int _end;
        private JsonParser _parser;
        private JsonValue _current;
        private readonly bool _valid;

        internal ArrayEnumerator(JsonValue value)
        {
            _source = value.Type == JsonType.Array ? value.ToString() : null;
            _end = _source?.Length ?? 0;
            _parser = _source != null ? new JsonParser(_source, 1) : default;
            _current = default;
            _valid = _source != null;
        }

        public JsonValue Current => _current;

        public bool MoveNext()
        {
            if (!_valid) return false;
            _parser.SkipWhitespace();
            if (_parser.Position >= _end - 1 || _parser.Current == ']') return false;
            _current = _parser.ParseValue();
            _parser.SkipWhitespace();
            if (_parser.Current == ',') _parser.Advance();
            return true;
        }
    }

    internal struct JsonParser
    {
        private readonly string _json;
        private int _pos;

        public int Position => _pos;
        public char Current => _pos < _json.Length ? _json[_pos] : '\0';

        public JsonParser(string json, int startPos = 0)
        {
            _json = json;
            _pos = startPos;
        }

        public void Advance() => _pos++;

        public bool Expect(char c)
        {
            if (Current == c)
            {
                _pos++;
                return true;
            }

            return false;
        }

        public void SkipWhitespace()
        {
            while (_pos < _json.Length)
            {
                var c = _json[_pos];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r') break;
                _pos++;
            }
        }

        public JsonValue ParseValue()
        {
            SkipWhitespace();
            if (_pos >= _json.Length) return default;
            var c = _json[_pos];
            switch (c)
            {
                case '"': return ParseString();
                case '{': return ParseObject();
                case '[': return ParseArray();
                case 't':
                case 'f': return ParseBoolean();
                case 'n': return ParseNull();
                default: return (c == '-' || (c >= '0' && c <= '9')) ? ParseNumber() : default;
            }
        }

        private JsonValue ParseString()
        {
            var start = _pos++;
            while (_pos < _json.Length)
            {
                var c = _json[_pos];
                if (c == '\\')
                {
                    _pos += 2;
                    continue;
                }

                if (c == '"')
                {
                    _pos++;
                    return new JsonValue(_json, start, _pos, JsonType.String);
                }

                _pos++;
            }

            return default;
        }

        private JsonValue ParseNumber()
        {
            var start = _pos;
            if (_json[_pos] == '-') _pos++;
            while (_pos < _json.Length)
            {
                var c = _json[_pos];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') _pos++;
                else break;
            }

            return new JsonValue(_json, start, _pos, JsonType.Number);
        }

        private JsonValue ParseBoolean()
        {
            var start = _pos;
            _pos += _json[_pos] == 't' ? 4 : 5;
            return new JsonValue(_json, start, _pos, JsonType.Boolean);
        }

        private JsonValue ParseNull()
        {
            var start = _pos;
            _pos += 4;
            return new JsonValue(_json, start, _pos, JsonType.Null);
        }

        private JsonValue ParseObject()
        {
            var start = _pos++;
            var depth = 1;
            while (_pos < _json.Length && depth > 0)
            {
                var c = _json[_pos];
                if (c == '"')
                {
                    _pos++;
                    SkipStringContent();
                    continue;
                }

                if (c == '{') depth++;
                else if (c == '}') depth--;
                _pos++;
            }

            return new JsonValue(_json, start, _pos, JsonType.Object);
        }

        private JsonValue ParseArray()
        {
            var start = _pos++;
            var depth = 1;
            while (_pos < _json.Length && depth > 0)
            {
                var c = _json[_pos];
                if (c == '"')
                {
                    _pos++;
                    SkipStringContent();
                    continue;
                }

                if (c == '[') depth++;
                else if (c == ']') depth--;
                _pos++;
            }

            return new JsonValue(_json, start, _pos, JsonType.Array);
        }

        private void SkipStringContent()
        {
            while (_pos < _json.Length)
            {
                var c = _json[_pos];
                if (c == '\\')
                {
                    _pos += 2;
                    continue;
                }

                if (c == '"')
                {
                    _pos++;
                    return;
                }

                _pos++;
            }
        }
    }

    /// <summary>
    /// JSON 构建器
    /// </summary>
    public class JsonBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder(256);
        private bool _needComma;

        public JsonBuilder BeginObject()
        {
            WriteComma();
            _sb.Append('{');
            _needComma = false;
            return this;
        }

        public JsonBuilder EndObject()
        {
            _sb.Append('}');
            _needComma = true;
            return this;
        }

        public JsonBuilder BeginArray()
        {
            WriteComma();
            _sb.Append('[');
            _needComma = false;
            return this;
        }

        public JsonBuilder EndArray()
        {
            _sb.Append(']');
            _needComma = true;
            return this;
        }

        public JsonBuilder Key(string key)
        {
            WriteComma();
            WriteString(key);
            _sb.Append(':');
            _needComma = false;
            return this;
        }

        public JsonBuilder Value(string value)
        {
            WriteComma();
            if (value == null) _sb.Append("null");
            else WriteString(value);
            _needComma = true;
            return this;
        }

        public JsonBuilder Value(int value)
        {
            WriteComma();
            _sb.Append(value);
            _needComma = true;
            return this;
        }

        public JsonBuilder Value(long value)
        {
            WriteComma();
            _sb.Append(value);
            _needComma = true;
            return this;
        }

        public JsonBuilder Value(float value)
        {
            WriteComma();
            _sb.Append(value);
            _needComma = true;
            return this;
        }

        public JsonBuilder Value(bool value)
        {
            WriteComma();
            _sb.Append(value ? "true" : "false");
            _needComma = true;
            return this;
        }

        public JsonBuilder Property(string key, string value)
        {
            if (value != null) Key(key).Value(value);
            return this;
        }

        public JsonBuilder Property(string key, int value)
        {
            Key(key).Value(value);
            return this;
        }

        public JsonBuilder Property(string key, long value)
        {
            Key(key).Value(value);
            return this;
        }

        public JsonBuilder Property(string key, float value)
        {
            Key(key).Value(value);
            return this;
        }

        public JsonBuilder Property(string key, bool value)
        {
            Key(key).Value(value);
            return this;
        }

        public JsonBuilder PropertyIf(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) Key(key).Value(value);
            return this;
        }

        public JsonBuilder StringArray(string key, IEnumerable<string> values)
        {
            Key(key).BeginArray();
            foreach (var v in values) Value(v);
            return EndArray();
        }

        private void WriteComma()
        {
            if (_needComma) _sb.Append(',');
        }

        internal void WriteString(string s)
        {
            _sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default: _sb.Append(c); break;
                }
            }

            _sb.Append('"');
        }

        internal void WriteRaw(string s)
        {
            _sb.Append(s);
        }

        internal void SetNeedComma(bool v)
        {
            _needComma = v;
        }

        public override string ToString() => _sb.ToString();

        public void Clear()
        {
            _sb.Clear();
            _needComma = false;
        }
    }

    /// <summary>
    /// JSON ORM 映射器 - 支持对象序列化和反序列化
    /// </summary>
    public static class JsonMapper
    {
        private static readonly Dictionary<Type, FieldInfo[]> _fieldCache = new Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// 从 JSON 字符串反序列化为对象
        /// </summary>
        public static T Deserialize<T>(string json) where T : new()
        {
            var obj = new T();
            Populate(obj, JsonValue.Parse(json));
            return obj;
        }

        /// <summary>
        /// 从 JsonValue 反序列化为对象
        /// </summary>
        public static T Deserialize<T>(JsonValue json) where T : new()
        {
            var obj = new T();
            Populate(obj, json);
            return obj;
        }

        /// <summary>
        /// 填充已有对象
        /// </summary>
        public static void Populate(object obj, JsonValue json)
        {
            if (obj == null || json.Type != JsonType.Object) return;
            var fields = GetFields(obj.GetType());
            foreach (var field in fields)
            {
                var value = json[field.Name];
                if (!value.IsValid) continue;
                SetFieldValue(obj, field, value);
            }
        }

        /// <summary>
        /// 序列化对象为 JSON 字符串
        /// </summary>
        public static string Serialize(object obj)
        {
            if (obj == null) return "null";
            var builder = new JsonBuilder();
            WriteObject(builder, obj);
            return builder.ToString();
        }

        private static FieldInfo[] GetFields(Type type)
        {
            if (_fieldCache.TryGetValue(type, out var cached)) return cached;
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsInitOnly && !f.IsLiteral).ToArray();
            _fieldCache[type] = fields;
            return fields;
        }

        private static void SetFieldValue(object obj, FieldInfo field, JsonValue value)
        {
            var type = field.FieldType;

            // 基础类型
            if (type == typeof(string))
            {
                field.SetValue(obj, value.AsString());
                return;
            }

            if (type == typeof(int))
            {
                field.SetValue(obj, value.AsInt());
                return;
            }

            if (type == typeof(long))
            {
                field.SetValue(obj, value.AsLong());
                return;
            }

            if (type == typeof(float))
            {
                field.SetValue(obj, value.AsFloat());
                return;
            }

            if (type == typeof(bool))
            {
                field.SetValue(obj, value.AsBool());
                return;
            }

            // 字符串数组
            if (type == typeof(string[]) && value.Type == JsonType.Array)
            {
                field.SetValue(obj, value.ToStringList()?.ToArray());
                return;
            }

            // List<string>
            if (type == typeof(List<string>) && value.Type == JsonType.Array)
            {
                field.SetValue(obj, value.ToStringList());
                return;
            }

            // 泛型 List<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) && value.Type == JsonType.Array)
            {
                var elementType = type.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(type);
                var enumerator = value.GetArrayEnumerator();
                while (enumerator.MoveNext())
                {
                    var item = DeserializeValue(enumerator.Current, elementType);
                    if (item != null) list.Add(item);
                }

                field.SetValue(obj, list);
                return;
            }

            // 数组 T[]
            if (type.IsArray && value.Type == JsonType.Array)
            {
                var elementType = type.GetElementType();
                var tempList = new List<object>();
                var enumerator = value.GetArrayEnumerator();
                while (enumerator.MoveNext())
                {
                    var item = DeserializeValue(enumerator.Current, elementType);
                    if (item != null) tempList.Add(item);
                }

                var array = Array.CreateInstance(elementType, tempList.Count);
                for (var i = 0; i < tempList.Count; i++) array.SetValue(tempList[i], i);
                field.SetValue(obj, array);
                return;
            }

            // 嵌套对象
            if (type.IsClass && value.Type == JsonType.Object)
            {
                var nested = Activator.CreateInstance(type);
                Populate(nested, value);
                field.SetValue(obj, nested);
            }
        }

        private static object DeserializeValue(JsonValue value, Type type)
        {
            if (type == typeof(string)) return value.AsString();
            if (type == typeof(int)) return value.AsInt();
            if (type == typeof(long)) return value.AsLong();
            if (type == typeof(float)) return value.AsFloat();
            if (type == typeof(bool)) return value.AsBool();
            if (type.IsClass && value.Type == JsonType.Object)
            {
                var obj = Activator.CreateInstance(type);
                Populate(obj, value);
                return obj;
            }

            return null;
        }

        private static void WriteObject(JsonBuilder builder, object obj)
        {
            builder.BeginObject();
            var fields = GetFields(obj.GetType());
            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                if (value == null) continue;
                builder.Key(field.Name);
                WriteValue(builder, value, field.FieldType);
            }

            builder.EndObject();
        }

        private static void WriteValue(JsonBuilder builder, object value, Type type)
        {
            if (value == null)
            {
                builder.WriteRaw("null");
                builder.SetNeedComma(true);
                return;
            }

            if (type == typeof(string))
            {
                builder.Value((string)value);
                return;
            }

            if (type == typeof(int))
            {
                builder.Value((int)value);
                return;
            }

            if (type == typeof(long))
            {
                builder.Value((long)value);
                return;
            }

            if (type == typeof(float))
            {
                builder.Value((float)value);
                return;
            }

            if (type == typeof(bool))
            {
                builder.Value((bool)value);
                return;
            }

            // 字符串集合
            if (value is IEnumerable<string> strList)
            {
                builder.BeginArray();
                foreach (var s in strList) builder.Value(s);
                builder.EndArray();
                return;
            }

            // 其他集合
            if (value is IEnumerable enumerable && !(value is string))
            {
                var elementType = type.IsArray ? type.GetElementType() :
                    type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
                builder.BeginArray();
                foreach (var item in enumerable)
                    WriteValue(builder, item, elementType);
                builder.EndArray();
                return;
            }

            // 嵌套对象
            if (type.IsClass)
            {
                WriteObject(builder, value);
                return;
            }

            builder.WriteRaw(value.ToString());
            builder.SetNeedComma(true);
        }
    }
}