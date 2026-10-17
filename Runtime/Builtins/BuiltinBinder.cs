using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Base class for standard command binders. Never allocates per call.</summary>
    internal abstract class BuiltinBinder
    {
        /// <summary>Shared registry for printing and nested execution.</summary>
        protected readonly ConsoleRegistry Reg;

        /// <summary>Creates a binder bound to a registry.</summary>
        protected BuiltinBinder(ConsoleRegistry reg)
        {
            Reg = reg;
        }

        /// <summary>Registers commands and vars on a scope.</summary>
        public abstract void Bind(IConsole c);

        /// <summary>Prints a line to the console output.</summary>
        protected void Print(string line)
        {
            if (Reg != null) Reg.Print(line);
        }

        /// <summary>Wraps a message as a red error line.</summary>
        protected static string Red(string text)
        {
            return "<color=red>" + text + "</color>";
        }

        /// <summary>Clamps an int into range.</summary>
        protected static int ClampInt(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>Finds active objects with an exact name.</summary>
        protected static List<GameObject> FindAll(string name)
        {
            var found = new List<GameObject>();
#if UNITY_2023_1_OR_NEWER
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
#else
            var all = UnityEngine.Object.FindObjectsOfType<GameObject>();
#endif
            foreach (var go in all)
            {
                if (go == null) continue;
                if (!go.activeInHierarchy) continue;
                if (string.Equals(go.name, name, StringComparison.OrdinalIgnoreCase)) found.Add(go);
            }
            return found;
        }

        /// <summary>Finds all live objects of a type.</summary>
        protected static UnityEngine.Object[] FindObjects(Type t)
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType(t);
#endif
        }

        /// <summary>Finds a type by full or short name across loaded assemblies.</summary>
        protected static Type FindTypeByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Type shortHit = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || !t.IsClass) continue;
                    if (t.Name.IndexOf('<') >= 0) continue;
                    if (t.ContainsGenericParameters) continue;
                    if (string.Equals(t.FullName, name, StringComparison.OrdinalIgnoreCase)) return t;
                    if (shortHit == null && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        shortHit = t;
                        if (t.FullName != null && t.FullName.StartsWith("UnityEngine.", StringComparison.Ordinal)) return t;
                    }
                }
            }
            return shortHit;
        }
    }
}
