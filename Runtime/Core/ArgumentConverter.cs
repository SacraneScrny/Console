using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Converts command string arguments to typed values. Custom types can be added via Register.</summary>
    public static class ArgumentConverter
    {
        static readonly Dictionary<Type, Func<string, object>> _parsers = new Dictionary<Type, Func<string, object>>();

        static ArgumentConverter()
        {
            Register<string>(s => s);
            Register<char>(s => { if (s.Length == 1) return s[0]; throw new FormatException("expected single char"); });
            Register<bool>(ParseBool);
            Register<int>(s => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            Register<uint>(s => uint.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            Register<long>(s => long.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            Register<ulong>(s => ulong.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            Register<float>(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            Register<double>(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            Register<decimal>(s => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            Register<Vector2>(ParseVector2);
            Register<Vector3>(ParseVector3);
            Register<Vector4>(ParseVector4);
            Register<Quaternion>(ParseQuaternion);
            Register<Color>(ParseColor);
            Register<DateTime>(s => DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            Register<TimeSpan>(s => TimeSpan.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            Register<GameObject>(s => GameObject.Find(s));
        }

        /// <summary>Registers a parser for a custom argument type.</summary>
        public static void Register<T>(Func<string, T> parser)
        {
            if (parser == null) throw new ArgumentNullException("parser");
            lock (_parsers) _parsers[typeof(T)] = s => parser(s);
        }

        /// <summary>Returns true if the type can be converted from a string.</summary>
        public static bool CanConvert(Type t)
        {
            if (t == null) return false;
            lock (_parsers) if (_parsers.ContainsKey(t)) return true;
            if (t.IsEnum) return true;
            if (t == typeof(string)) return true;
            return false;
        }

        /// <summary>Tries to convert a string to the target type.</summary>
        public static bool TryConvert(Type t, string s, out object value, out string error)
        {
            value = null; error = null;
            if (t == null) { error = "null type"; return false; }
            s = s ?? "";
            try
            {
                Func<string, object> p = null;
                lock (_parsers) _parsers.TryGetValue(t, out p);
                if (p != null) { value = p(s); return true; }
                if (t.IsEnum)
                {
                    value = Enum.Parse(t, s, true);
                    return true;
                }
                error = "unsupported type " + t.Name;
                return false;
            }
            catch (Exception ex)
            {
                error = "'" + s + "' is not " + Friendly(t) + " (" + ex.Message + ")";
                return false;
            }
        }

        static bool ParseBool(string s)
        {
            string v = s.Trim().ToLowerInvariant();
            if (v == "true" || v == "1" || v == "yes" || v == "y" || v == "on") return true;
            if (v == "false" || v == "0" || v == "no" || v == "n" || v == "off") return false;
            throw new FormatException("expected bool (true/false/1/0/on/off)");
        }

        static float[] ParseFloats(string s, int expected)
        {
            string t = s.Trim().Trim('(', ')').Replace(';', ',').Replace(' ', ',');
            var parts = t.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != expected)
                throw new FormatException("expected " + expected + " comma-separated floats, e.g. 1,2,3");
            var f = new float[expected];
            for (int i = 0; i < expected; i++)
                f[i] = float.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
            return f;
        }

        static Vector2 ParseVector2(string s) { var f = ParseFloats(s, 2); return new Vector2(f[0], f[1]); }
        static Vector3 ParseVector3(string s) { var f = ParseFloats(s, 3); return new Vector3(f[0], f[1], f[2]); }
        static Vector4 ParseVector4(string s) { var f = ParseFloats(s, 4); return new Vector4(f[0], f[1], f[2], f[3]); }
        static Quaternion ParseQuaternion(string s) { var f = ParseFloats(s, 4); return new Quaternion(f[0], f[1], f[2], f[3]); }

        static Color ParseColor(string s)
        {
            string t = s.Trim();
            if (t.StartsWith("#"))
            {
                Color c;
                if (ColorUtility.TryParseHtmlString(t, out c)) return c;
                throw new FormatException("bad html color, e.g. #ff0000");
            }
            var f3 = TryFloats(t, 3);
            if (f3 != null) return new Color(f3[0], f3[1], f3[2]);
            var f4 = TryFloats(t, 4);
            if (f4 != null) return new Color(f4[0], f4[1], f4[2], f4[3]);
            throw new FormatException("expected #rrggbb or r,g,b[,a]");
        }

        static float[] TryFloats(string s, int n)
        {
            try { return ParseFloats(s, n); }
            catch { return null; }
        }

        static string Friendly(Type t)
        {
            if (t == typeof(void)) return "void";
            if (t == typeof(int)) return "int";
            if (t == typeof(uint)) return "uint";
            if (t == typeof(long)) return "long";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";
            if (t == typeof(char)) return "char";
            if (t == typeof(decimal)) return "decimal";
            if (t == typeof(DateTime)) return "date";
            if (t == typeof(TimeSpan)) return "span";
            return t.Name;
        }
    }
}
