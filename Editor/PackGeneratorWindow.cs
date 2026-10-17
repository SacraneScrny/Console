using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UniversalConsole.Editor
{
    /// <summary>Editor window that generates pack stubs for a static class.</summary>
    public sealed class PackGeneratorWindow : EditorWindow
    {
        string _packName = "";
        string _autoPackName = "";
        Vector2 _scroll;
        List<MethodInfo> _methods = new List<MethodInfo>();
        Dictionary<string, bool> _checked = new Dictionary<string, bool>();
        Type _targetType;
        List<Type> _candidates = new List<Type>();
        string[] _candidateNames = new string[0];
        int _selected = -1;

        [MenuItem("Tools/Universal Console/Generate Pack...")]
        public static void Open()
        {
            GetWindow<PackGeneratorWindow>("Console Pack");
        }

        [MenuItem("Tools/Universal Console/Create Example Pack")]
        public static void CreateExample()
        {
            string dir = "Assets/UniversalConsoleExample";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = dir + "/ExamplePack.cs";
            File.WriteAllText(path, ExampleText(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log("UniversalConsole: example written to " + path);
        }

        static string ExampleText()
        {
            return @"using System;
using UniversalConsole;
using UnityEngine;

public static class GameCheats
{
    public static int FpsMax = 60;
    public static void Spawn(string prefabName, int count = 1)
    {
        Debug.Log(""Spawn "" + count + ""x "" + prefabName);
    }
    public static void KillAll() { Debug.Log(""Killed all""); }
}

public sealed class ExamplePack : IConsolePack
{
    public void Build(IConsole c)
    {
        c.Command(""spawn"", (Action<string, int>)GameCheats.Spawn, ""Spawn: spawn \""big orc\"" 5"");
        c.Command(""killall"", (Action)GameCheats.KillAll, ""Kill all"");
        c.Var(""fps_max"", () => GameCheats.FpsMax, v => GameCheats.FpsMax = v, ""FPS limit"");
    }
}
";
        }

        void OnEnable()
        {
            RefreshCandidates();
        }

        void OnGUI()
        {
            GUILayout.Label("Pack Generator", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (_candidateNames.Length == 0)
                EditorGUILayout.HelpBox("No static classes found. Press Refresh.", MessageType.Info);
            else
            {
                int sel = EditorGUILayout.Popup("Static class", _selected, _candidateNames);
                if (sel != _selected && sel >= 0 && sel < _candidates.Count)
                {
                    _selected = sel;
                    _targetType = _candidates[sel];
                    SuggestPackName();
                    FindMethods();
                }
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(64))) RefreshCandidates();
            EditorGUILayout.EndHorizontal();
            _packName = EditorGUILayout.TextField("Pack name", _packName);

            if (_targetType != null && GUILayout.Button("Find methods")) FindMethods();

            if (_methods.Count > 0)
            {
                GUILayout.Label("Methods (" + _methods.Count + "):", EditorStyles.miniBoldLabel);
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(220));
                foreach (var m in _methods)
                {
                    string key = m.ToString();
                    bool v;
                    if (!_checked.TryGetValue(key, out v)) v = IsGoodCandidate(m);
                    string label = (m.IsPublic ? "+" : "-") + " " + m.ReturnType.Name + " " + m.Name + "(" +
                        string.Join(", ", m.GetParameters().Select(p =>
                            (p.IsOptional ? "[" : "") + p.ParameterType.Name + " " + p.Name + (p.IsOptional ? "]" : "")).ToArray()) + ")";
                    _checked[key] = EditorGUILayout.ToggleLeft(label, v);
                }
                GUILayout.EndScrollView();

                if (GUILayout.Button("Generate pack")) Generate();
            }
            else if (_targetType != null)
            {
                EditorGUILayout.HelpBox("No static methods found.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Select a static class, tick methods, then press Generate pack.", MessageType.Info);
            }
        }

        void SuggestPackName()
        {
            string auto = _targetType.Name + "Pack";
            if (string.IsNullOrEmpty(_packName) || _packName == _autoPackName || _packName == "MyGamePack")
                _packName = auto;
            _autoPackName = auto;
        }

        void RefreshCandidates()
        {
            _candidates.Clear();
            var allowed = new HashSet<Assembly>();
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new string[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) continue;
                Type main;
                try { main = script.GetClass(); }
                catch { continue; }
                if (main == null) continue;
                allowed.Add(main.Assembly);
            }
            string[] fallback = new string[]
            {
                "Assembly-CSharp", "Assembly-CSharp-Editor",
                "Assembly-CSharp-firstpass", "Assembly-CSharp-firstpass Editor"
            };
            foreach (var n in fallback)
            {
                try { allowed.Add(Assembly.Load(n)); }
                catch { }
            }
            foreach (var asm in allowed)
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!t.IsClass || !t.IsAbstract || !t.IsSealed) continue;
                    if (t.IsGenericTypeDefinition || t.ContainsGenericParameters) continue;
                    if (t.Name.IndexOf('<') >= 0) continue;
                    if (t.IsNested && !t.IsNestedPublic) continue;
                    MethodInfo[] ms;
                    try { ms = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly); }
                    catch { continue; }
                    bool any = false;
                    foreach (var m in ms)
                        if (!m.IsSpecialName) { any = true; break; }
                    if (!any) continue;
                    _candidates.Add(t);
                }
            }
            _candidates.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
            _candidateNames = _candidates.Select(t => t.FullName.Replace('+', '.')).ToArray();
            _selected = -1;
            if (_targetType != null)
            {
                for (int i = 0; i < _candidates.Count; i++)
                {
                    if (_candidates[i] == _targetType) { _selected = i; break; }
                }
            }
        }

        void FindMethods()
        {
            _methods.Clear();
            _checked.Clear();
            if (_targetType == null) return;
            foreach (var m in _targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.IsSpecialName) continue;
                _methods.Add(m);
            }
        }

        static bool IsGoodCandidate(MethodInfo m)
        {
            if (!m.IsPublic) return false;
            if (m.IsGenericMethodDefinition || m.ContainsGenericParameters) return false;
            var rt = m.ReturnType;
            bool okRet = rt == typeof(void) || rt == typeof(string) || rt == typeof(int) ||
                         rt == typeof(bool) || rt == typeof(float) || rt == typeof(double);
            if (!okRet) return false;
            foreach (var p in m.GetParameters())
            {
                if (p.ParameterType.IsByRef) return false;
                if (p.IsDefined(typeof(ParamArrayAttribute), false)) continue;
            }
            return true;
        }

        void Generate()
        {
            if (_targetType == null) return;
            if (string.IsNullOrWhiteSpace(_packName))
            {
                Debug.LogError("UniversalConsole: pack name is empty.");
                return;
            }
            if (_packName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                Debug.LogError("UniversalConsole: pack name contains invalid file name characters.");
                return;
            }
            var usings = new HashSet<string>(StringComparer.Ordinal);
            usings.Add("UniversalConsole");
            usings.Add("UnityEngine.Scripting");
            var lines = new List<KeyValuePair<string, string>>();
            foreach (var m in _methods)
            {
                string key = m.ToString();
                bool v;
                if (!_checked.TryGetValue(key, out v) || !v) continue;
                string cmd = m.Name.ToLowerInvariant();
                lines.Add(new KeyValuePair<string, string>(TargetRef(m), "c.Command(\"" + cmd + "\", " + BuildDelegateCast(m, usings) + ");"));
            }
            if (lines.Count == 0)
            {
                Debug.LogWarning("UniversalConsole: no methods selected.");
                return;
            }
            string dir = "Assets/UniversalConsoleGenerated";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = dir + "/" + _packName + ".cs";
            if (File.Exists(path))
            {
                int choice = EditorUtility.DisplayDialogComplex("Pack already exists",
                    "File already exists:\n" + path + "\n\nOverwrite it, append only missing methods, or cancel?",
                    "Overwrite", "Append missing", "Cancel");
                if (choice == 2) return;
                if (choice == 1)
                {
                    AppendMissing(path, lines, usings);
                    return;
                }
            }
            File.WriteAllText(path, BuildFileText(lines, usings), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log("UniversalConsole: pack written to " + path);
        }

        string BuildFileText(List<KeyValuePair<string, string>> lines, HashSet<string> usings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            var rest = new List<string>(usings);
            rest.Sort(StringComparer.Ordinal);
            foreach (var u in rest) sb.AppendLine("using " + u + ";");
            sb.AppendLine();
            sb.AppendLine("[Preserve]");
            sb.AppendLine("public sealed class " + _packName + " : IConsolePack");
            sb.AppendLine("{");
            sb.AppendLine("    [Preserve]");
            sb.AppendLine("    public void Build(IConsole c)");
            sb.AppendLine("    {");
            foreach (var l in lines) sb.AppendLine("        " + l.Value);
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        void AppendMissing(string path, List<KeyValuePair<string, string>> lines, HashSet<string> usings)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match mt in Regex.Matches(text, @"global::[A-Za-z_][\w.]*"))
                present.Add(mt.Value);
            var fresh = new List<string>();
            foreach (var l in lines)
                if (!present.Contains(l.Key)) fresh.Add(l.Value);
            if (fresh.Count == 0)
            {
                Debug.Log("UniversalConsole: all selected methods are already present.");
                return;
            }
            var have = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match mt in Regex.Matches(text, @"^\s*using\s+([\w.]+)\s*;", RegexOptions.Multiline))
                have.Add(mt.Groups[1].Value);
            var missing = new List<string>();
            if (!have.Contains("System")) missing.Add("System");
            var rest = new List<string>(usings);
            rest.Sort(StringComparer.Ordinal);
            foreach (var u in rest)
                if (!have.Contains(u)) missing.Add(u);
            if (missing.Count > 0)
            {
                var sbU = new StringBuilder();
                foreach (var u in missing) sbU.AppendLine("using " + u + ";");
                var mum = Regex.Matches(text, @"^\s*using\s+[\w.]+\s*;\s*\r?\n", RegexOptions.Multiline);
                if (mum.Count > 0)
                {
                    var last = mum[mum.Count - 1];
                    text = text.Insert(last.Index + last.Length, sbU.ToString());
                }
                else text = sbU.ToString() + text;
            }
            int close = FindBuildClose(text);
            if (close < 0)
            {
                Debug.LogError("UniversalConsole: Build(IConsole) body not found. Use Overwrite instead.");
                return;
            }
            int ins = close;
            int j = ins - 1;
            while (j >= 0 && (text[j] == ' ' || text[j] == '\t')) j--;
            if (j < 0 || text[j] == '\n') ins = j + 1;
            var sbI = new StringBuilder();
            foreach (var f in fresh) sbI.AppendLine("        " + f);
            text = text.Insert(ins, sbI.ToString());
            File.WriteAllText(path, text, Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log("UniversalConsole: appended " + fresh.Count + " command(s) to " + path);
        }

        static int FindBuildClose(string text)
        {
            int mi = text.IndexOf("Build(IConsole", StringComparison.Ordinal);
            if (mi < 0) mi = text.IndexOf("Build", StringComparison.Ordinal);
            if (mi < 0) return -1;
            int open = text.IndexOf('{', mi);
            if (open < 0) return -1;
            int depth = 0;
            bool inLine = false;
            bool inBlock = false;
            bool inStr = false;
            bool inChar = false;
            bool verbatim = false;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                char n = i + 1 < text.Length ? text[i + 1] : '\0';
                if (inLine)
                {
                    if (c == '\n') inLine = false;
                    continue;
                }
                if (inBlock)
                {
                    if (c == '*' && n == '/') { inBlock = false; i++; }
                    continue;
                }
                if (inStr)
                {
                    if (!verbatim && c == '\\') { i++; continue; }
                    if (c == '"')
                    {
                        if (verbatim && n == '"') { i++; continue; }
                        inStr = false;
                        verbatim = false;
                    }
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"')
                {
                    inStr = true;
                    verbatim = false;
                    int j = i - 1;
                    int mods = 0;
                    while (j >= 0 && mods < 2 && (text[j] == '@' || text[j] == '$'))
                    {
                        if (text[j] == '@') verbatim = true;
                        j--;
                        mods++;
                    }
                    continue;
                }
                if (c == '\'') { inChar = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        string TargetRef(MethodInfo m)
        {
            return "global::" + _targetType.FullName.Replace('+', '.') + "." + m.Name;
        }

        string BuildDelegateCast(MethodInfo m, HashSet<string> usings)
        {
            var ps = m.GetParameters();
            var parts = new List<string>();
            foreach (var p in ps) parts.Add(Friendly(p.ParameterType, usings));
            string del;
            if (m.ReturnType == typeof(void))
                del = ps.Length == 0 ? "Action" : "Action<" + string.Join(", ", parts.ToArray()) + ">";
            else
            {
                parts.Add(Friendly(m.ReturnType, usings));
                del = "Func<" + string.Join(", ", parts.ToArray()) + ">";
            }
            return "(" + del + ")" + TargetRef(m);
        }

        static string Friendly(Type t, HashSet<string> usings)
        {
            while (t.IsByRef) t = t.GetElementType();
            if (t == typeof(void)) return "void";
            if (t == typeof(int)) return "int";
            if (t == typeof(uint)) return "uint";
            if (t == typeof(long)) return "long";
            if (t == typeof(ulong)) return "ulong";
            if (t == typeof(short)) return "short";
            if (t == typeof(ushort)) return "ushort";
            if (t == typeof(byte)) return "byte";
            if (t == typeof(sbyte)) return "sbyte";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t == typeof(decimal)) return "decimal";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";
            if (t == typeof(char)) return "char";
            if (t == typeof(object)) return "object";
            if (t.IsArray) return Friendly(t.GetElementType(), usings) + "[]";
            if (t.IsGenericParameter) return t.Name;
            if (t.IsGenericType)
            {
                Type def = t.GetGenericTypeDefinition();
                AddUsing(usings, def.Namespace);
                string name = CleanName(def);
                var args = new List<string>();
                foreach (var a in t.GetGenericArguments()) args.Add(Friendly(a, usings));
                return NestedName(t, usings) + name + "<" + string.Join(", ", args.ToArray()) + ">";
            }
            AddUsing(usings, t.Namespace);
            return NestedName(t, usings) + CleanName(t);
        }

        static string NestedName(Type t, HashSet<string> usings)
        {
            if (!t.IsNested) return "";
            Type d = t.DeclaringType;
            AddUsing(usings, d.Namespace);
            return NestedName(d, usings) + CleanName(d) + ".";
        }

        static string CleanName(Type t)
        {
            string n = t.Name;
            int tick = n.IndexOf('`');
            if (tick >= 0) n = n.Substring(0, tick);
            return n;
        }

        static void AddUsing(HashSet<string> usings, string ns)
        {
            if (string.IsNullOrEmpty(ns)) return;
            if (ns == "System") return;
            usings.Add(ns);
        }
    }
}
