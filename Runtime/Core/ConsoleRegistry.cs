using System;
using System.Collections.Generic;
using System.Reflection;

namespace UniversalConsole
{
    internal sealed class CommandEntry
    {
        public string Name;
        public string[] Aliases = Array.Empty<string>();
        public string Help = "";
        public string Category = "";
        public string Owner = "";
        public Delegate Handler;
        public MethodInfo Method;
        public ParameterInfo[] Params = Array.Empty<ParameterInfo>();
    }

    internal sealed class VarEntry
    {
        public string Name;
        public string Help = "";
        public string Owner = "";
        public Type ValueType;
        public Func<object> Getter;
        public Action<object> Setter;
    }

    internal sealed class ScopeHandle : IConsole
    {
        readonly ConsoleRegistry _registry;
        readonly string _owner;
        readonly string _prefix;
        bool _disposed;

        public ScopeHandle(ConsoleRegistry registry, string owner, string prefix)
        {
            _registry = registry;
            _owner = owner;
            _prefix = prefix ?? "";
        }

        public void Command(string name, Delegate handler, string help = "", string category = "", params string[] aliases)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(IConsole));
            try { _registry.RegisterCommand(_owner, _prefix + name, handler, help, category, aliases); }
            catch (ArgumentException ex) { _registry.Print("<color=red>" + ex.Message + "</color>"); }
        }

        public void Var<T>(string name, Func<T> getter, Action<T> setter, string help = "")
        {
            if (_disposed) throw new ObjectDisposedException(nameof(IConsole));
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            if (setter == null) throw new ArgumentNullException(nameof(setter));
            Func<object> g = () => getter();
            Action<object> s = o => setter((T)o);
            try { _registry.RegisterVar(_owner, _prefix + name, typeof(T), g, s, help); }
            catch (ArgumentException ex) { _registry.Print("<color=red>" + ex.Message + "</color>"); }
        }

        public void VarReadOnly<T>(string name, Func<T> getter, string help = "")
        {
            if (_disposed) throw new ObjectDisposedException(nameof(IConsole));
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            Func<object> g = () => getter();
            try { _registry.RegisterVar(_owner, _prefix + name, typeof(T), g, null, help); }
            catch (ArgumentException ex) { _registry.Print("<color=red>" + ex.Message + "</color>"); }
        }

        public IConsole Scope(string prefix)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(IConsole));
            return new ScopeHandle(_registry, _owner, _prefix + prefix + ".");
        }

        public void Print(string line)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(IConsole));
            _registry.Print(line);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _registry.UnregisterOwner(_owner);
        }
    }

    /// <summary>A completion candidate with a usage hint.</summary>
    public struct ConsoleSuggestion
    {
        /// <summary>Command or variable name.</summary>
        public string Name;
        /// <summary>Short params and description.</summary>
        public string Hint;
    }

    /// <summary>Central command and variable registry. Thread-safe for registration and printing.</summary>
    public sealed class ConsoleRegistry
    {
        readonly object _lock = new object();
        readonly Dictionary<string, List<CommandEntry>> _commands =
            new Dictionary<string, List<CommandEntry>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, VarEntry> _vars =
            new Dictionary<string, VarEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised for every output line.</summary>
        public event Action<string> OnPrint;

        /// <summary>Writes a line to the output.</summary>
        public void Print(string line)
        {
            var h = OnPrint;
            if (h != null) h(line ?? "");
        }

        /// <summary>Number of registered command overloads.</summary>
        public int CommandCount
        {
            get
            {
                lock (_lock)
                {
                    int n = 0;
                    foreach (var kv in _commands) n += kv.Value.Count;
                    return n;
                }
            }
        }

        /// <summary>Number of registered variables.</summary>
        public int VarCount
        {
            get { lock (_lock) return _vars.Count; }
        }

        /// <summary>Creates an isolated registration scope. Disposing the scope unregisters its entries.</summary>
        public IConsole CreateScope(string owner)
        {
            if (string.IsNullOrEmpty(owner)) owner = "anon";
            return new ScopeHandle(this, owner, "");
        }

        /// <summary>Removes all commands and variables owned by the given owner.</summary>
        public void UnregisterOwner(string owner)
        {
            lock (_lock)
            {
                var toRemove = new List<string>();
                foreach (var kv in _commands)
                {
                    kv.Value.RemoveAll(e => e.Owner == owner);
                    if (kv.Value.Count == 0) toRemove.Add(kv.Key);
                }
                foreach (var k in toRemove) _commands.Remove(k);

                var vr = new List<string>();
                foreach (var kv in _vars)
                    if (kv.Value.Owner == owner) vr.Add(kv.Key);
                foreach (var k in vr) _vars.Remove(k);
            }
        }

        /// <summary>Clears all commands and variables.</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _commands.Clear();
                _vars.Clear();
            }
        }

        internal void RegisterCommand(string owner, string name, Delegate handler, string help, string category, string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("command name is empty", nameof(name));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var method = handler.Method;
            var entry = new CommandEntry
            {
                Name = name.Trim(),
                Aliases = aliases ?? Array.Empty<string>(),
                Help = help ?? "",
                Category = category ?? "",
                Owner = owner,
                Handler = handler,
                Method = method,
                Params = method.GetParameters()
            };
            ValidateCommand(entry);

            lock (_lock)
            {
                var key = entry.Name.ToLowerInvariant();
                if (!_commands.TryGetValue(key, out var list))
                {
                    list = new List<CommandEntry>();
                    _commands[key] = list;
                }
                list.RemoveAll(e => e.Owner == owner && e.Method == entry.Method);
                foreach (var a in entry.Aliases)
                {
                    var ak = a.Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(ak)) continue;
                    if (!_commands.TryGetValue(ak, out var alist))
                    {
                        alist = new List<CommandEntry>();
                        _commands[ak] = alist;
                    }
                    alist.RemoveAll(e => e.Owner == owner && e.Method == entry.Method);
                }
                list.Add(entry);
                foreach (var a in entry.Aliases)
                {
                    var ak = a.Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(ak)) continue;
                    _commands[ak].Add(entry);
                }
            }
        }

        static void ValidateCommand(CommandEntry e)
        {
            var rt = e.Method.ReturnType;
            bool okReturn = rt == typeof(void) || rt == typeof(string) ||
                            rt == typeof(int) || rt == typeof(bool) || rt == typeof(float) || rt == typeof(double);
            if (!okReturn)
                throw new ArgumentException("Command '" + e.Name + "' has unsupported return type " + Friendly(rt) +
                    ". Allowed: void/string/int/bool/float/double.");
            foreach (var p in e.Params)
            {
                if (p.ParameterType.IsByRef) throw new ArgumentException("Command '" + e.Name + "': ref/out params not supported.");
                if (!ArgumentConverter.CanConvert(p.ParameterType) && !p.IsOptional && !IsParamsArray(p))
                    throw new ArgumentException("Command '" + e.Name + "': unsupported param type " + Friendly(p.ParameterType) + ".");
            }
        }

        static bool IsParamsArray(ParameterInfo p)
        {
            return p.IsDefined(typeof(ParamArrayAttribute), false) && p.ParameterType.IsArray;
        }

        internal void RegisterVar(string owner, string name, Type type, Func<object> getter, Action<object> setter, string help)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("var name is empty", nameof(name));
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!ArgumentConverter.CanConvert(type))
                throw new ArgumentException("Var '" + name + "': unsupported type " + type.Name + ".");
            lock (_lock)
            {
                _vars[name.Trim().ToLowerInvariant()] = new VarEntry
                {
                    Name = name.Trim(), Help = help ?? "", Owner = owner,
                    ValueType = type, Getter = getter, Setter = setter
                };
            }
        }

        /// <summary>Parses and executes a console line. Returns true on success.</summary>
        public bool Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;
            List<string> tokens;
            try { tokens = Tokenizer.Tokenize(line); }
            catch (Exception ex) { Print("<color=red>Parse error: " + ex.Message + "</color>"); return false; }
            if (tokens.Count == 0) return true;

            string name = tokens[0];
            var args = tokens.GetRange(1, tokens.Count - 1);

            VarEntry v;
            lock (_lock) _vars.TryGetValue(name.ToLowerInvariant(), out v);
            if (v != null)
            {
                if (args.Count == 0)
                {
                    try { Print(v.Name + " = " + FormatValue(v.Getter())); }
                    catch (Exception ex) { Print("<color=red>" + v.Name + ": " + ex.Message + "</color>"); }
                    return true;
                }
                if (v.Setter == null) { Print("<color=red>" + v.Name + " is read-only.</color>"); return false; }
                if (args.Count != 1) { Print("<color=red>Usage: " + v.Name + " " + Friendly(v.ValueType) + "</color>"); return false; }
                object cv;
                string cerr;
                if (!ArgumentConverter.TryConvert(v.ValueType, args[0], out cv, out cerr))
                { Print("<color=red>" + v.Name + ": " + cerr + "</color>"); return false; }
                try { v.Setter(cv); Print(v.Name + " = " + FormatValue(cv)); return true; }
                catch (Exception ex) { Print("<color=red>" + v.Name + ": " + ex.Message + "</color>"); return false; }
            }

            List<CommandEntry> candidates;
            lock (_lock) _commands.TryGetValue(name.ToLowerInvariant(), out candidates);
            if (candidates == null || candidates.Count == 0)
            {
                string sug = FindClosest(name);
                Print("<color=red>Command not found: " + name + "." +
                    (sug != null ? " Did you mean " + sug + "?" : "") + "</color>");
                return false;
            }
            var copy = new List<CommandEntry>(candidates);
            string lastError = null;
            foreach (var c in copy)
            {
                object[] callArgs;
                string err;
                if (TryBind(c, args, out callArgs, out err))
                {
                    try
                    {
                        object ret = c.Handler.DynamicInvoke(callArgs);
                        if (c.Method.ReturnType != typeof(void))
                            Print(FormatValue(ret));
                        return true;
                    }
                    catch (Exception ex)
                    {
                        var inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                        Print("<color=red>" + c.Name + ": " + inner.Message + "</color>");
                        return false;
                    }
                }
                lastError = err;
            }
            Print("<color=red>" + lastError + "</color>");
            return false;
        }

        static bool TryBind(CommandEntry c, List<string> args, out object[] callArgs, out string error)
        {
            var ps = c.Params;
            bool hasParams = ps.Length > 0 && IsParamsArray(ps[ps.Length - 1]);
            int fixedCount = hasParams ? ps.Length - 1 : ps.Length;
            int required = 0;
            for (int i = 0; i < fixedCount; i++) if (!ps[i].IsOptional) required++;

            if (args.Count < required || (!hasParams && args.Count > ps.Length))
            {
                error = "Invalid args: " + c.Name + Usage(c) + ". Got " + args.Count + ", need " +
                        (required == ps.Length ? required.ToString() : required + ".." + ps.Length) + ".";
                callArgs = null;
                return false;
            }
            var outArgs = new object[ps.Length];
            for (int i = 0; i < fixedCount; i++)
            {
                if (i < args.Count)
                {
                    object cv; string cerr;
                    if (!ArgumentConverter.TryConvert(ps[i].ParameterType, args[i], out cv, out cerr))
                    {
                        error = "Invalid args: '" + args[i] + "' is not " + Friendly(ps[i].ParameterType) + ". " + c.Name + Usage(c);
                        callArgs = null;
                        return false;
                    }
                    outArgs[i] = cv;
                }
                else
                {
                    outArgs[i] = ps[i].DefaultValue;
                }
            }
            if (hasParams)
            {
                var elem = ps[ps.Length - 1].ParameterType.GetElementType();
                int rest = Math.Max(0, args.Count - fixedCount);
                var arr = Array.CreateInstance(elem, rest);
                for (int i = 0; i < rest; i++)
                {
                    object cv; string cerr;
                    if (!ArgumentConverter.TryConvert(elem, args[fixedCount + i], out cv, out cerr))
                    {
                        error = "Invalid args: '" + args[fixedCount + i] + "' is not " + Friendly(elem) + ". " + c.Name + Usage(c);
                        callArgs = null;
                        return false;
                    }
                    arr.SetValue(cv, i);
                }
                outArgs[ps.Length - 1] = arr;
            }
            else
            {
                for (int i = args.Count; i < ps.Length; i++) outArgs[i] = ps[i].DefaultValue;
            }
            callArgs = outArgs;
            error = null;
            return true;
        }

        internal static string Usage(CommandEntry c)
        {
            var parts = new List<string> { c.Name };
            foreach (var p in c.Params)
            {
                bool isParams = IsParamsArray(p);
                string t = isParams ? Friendly(p.ParameterType.GetElementType()) + "..." : Friendly(p.ParameterType);
                if (p.IsOptional) parts.Add("[" + p.Name + ":" + t + "]");
                else if (isParams) parts.Add("[" + p.Name + ":" + t + "]");
                else parts.Add(p.Name + ":" + t);
            }
            return " Usage: " + string.Join(" ", parts.ToArray());
        }

        string FindClosest(string name)
        {
            string best = null; int bestD = 3;
            lock (_lock)
            {
                foreach (var k in _commands.Keys)
                {
                    int d = Levenshtein(name.ToLowerInvariant(), k);
                    if (d < bestD) { bestD = d; best = k; }
                }
            }
            if (best != null)
            {
                lock (_lock)
                {
                    List<CommandEntry> l;
                    if (_commands.TryGetValue(best, out l) && l.Count > 0) return l[0].Name;
                }
            }
            return best;
        }

        static int Levenshtein(string a, string b)
        {
            int n = a.Length, m = b.Length;
            if (Math.Abs(n - m) > 2) return 99;
            var d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[n, m];
        }

        static string FormatValue(object o)
        {
            return o == null ? "null" : o.ToString();
        }

        /// <summary>Collects command and variable names matching the prefix.</summary>
        public void GetCompletion(string prefix, List<string> outNames, int max = 20)
        {
            outNames.Clear();
            string p = (prefix ?? "").ToLowerInvariant();
            lock (_lock)
            {
                foreach (var kv in _commands)
                {
                    if (kv.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    {
                        if (kv.Value.Count > 0) outNames.Add(kv.Value[0].Name);
                        if (outNames.Count >= max) break;
                    }
                }
                foreach (var kv in _vars)
                {
                    if (kv.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    {
                        outNames.Add(kv.Value.Name);
                        if (outNames.Count >= max) break;
                    }
                }
            }
            outNames.Sort(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Collects completion candidates with usage hints for the given prefix.</summary>
        public void GetSuggestions(string prefix, List<ConsoleSuggestion> outList, int max = 7)
        {
            outList.Clear();
            string p = (prefix ?? "").Trim();
            if (p.Length == 0 || max <= 0) return;
            var tmp = new List<ConsoleSuggestion>();
            lock (_lock)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in _commands)
                {
                    foreach (var e in kv.Value)
                    {
                        if (!seen.Add(e.Name)) continue;
                        if (!e.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) continue;
                        string h = ParamsPart(e);
                        if (!string.IsNullOrEmpty(e.Help)) h = (h + " - " + e.Help).Trim();
                        ConsoleSuggestion s;
                        s.Name = e.Name;
                        s.Hint = h;
                        tmp.Add(s);
                    }
                }
                foreach (var kv in _vars)
                {
                    if (!kv.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase)) continue;
                    string h = Friendly(kv.Value.ValueType);
                    if (!string.IsNullOrEmpty(kv.Value.Help)) h += " - " + kv.Value.Help;
                    ConsoleSuggestion s;
                    s.Name = kv.Value.Name;
                    s.Hint = h;
                    tmp.Add(s);
                }
            }
            tmp.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < tmp.Count && outList.Count < max; i++) outList.Add(tmp[i]);
        }

        static string ParamsPart(CommandEntry c)
        {
            var parts = new List<string>();
            foreach (var q in c.Params)
            {
                bool isParams = IsParamsArray(q);
                string t = isParams ? Friendly(q.ParameterType.GetElementType()) + "..." : Friendly(q.ParameterType);
                if (q.IsOptional) parts.Add("[" + q.Name + ":" + t + "]");
                else if (isParams) parts.Add("[" + q.Name + ":" + t + "]");
                else parts.Add(q.Name + ":" + t);
            }
            return string.Join(" ", parts.ToArray());
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
            return t.Name;
        }

        /// <summary>Collects help lines for all commands and variables.</summary>
        public void GetHelp(List<string> outLines)
        {
            outLines.Clear();
            var cmds = new List<CommandEntry>();
            var vars = new List<VarEntry>();
            lock (_lock)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in _commands)
                    foreach (var e in kv.Value)
                        if (seen.Add(e.Name + "|" + e.Method.ToString()))
                            cmds.Add(e);
                foreach (var kv in _vars) vars.Add(kv.Value);
            }
            cmds.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            vars.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            foreach (var c in cmds)
                outLines.Add(c.Name + Usage(c) + (string.IsNullOrEmpty(c.Help) ? "" : " - " + c.Help));
            foreach (var v in vars)
                outLines.Add(v.Name + " : " + Friendly(v.ValueType) + (string.IsNullOrEmpty(v.Help) ? "" : " - " + v.Help));
        }
    }
}
