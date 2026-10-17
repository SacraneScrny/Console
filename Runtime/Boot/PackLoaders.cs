using System;
using System.Collections.Generic;

namespace UniversalConsole
{
    /// <summary>Finds all non-abstract IConsolePack classes with a parameterless constructor.</summary>
    public sealed class TypeCachePackLoader : IPackLoader
    {
        static readonly string[] BlacklistPrefixes = new[]
        {
            "System.", "Microsoft.", "Mono.", "mscorlib", "netstandard",
            "Unity.", "UnityEngine", "UnityEditor", "Bee.", "nunit.", "Newtonsoft",
            "UniversalConsole"
        };

        /// <summary>Loads available packs.</summary>
        public IEnumerable<IConsolePack> Load()
        {
            var out_ = new List<IConsolePack>();
            var asmList = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in asmList)
            {
                string an = asm.FullName ?? "";
                bool skip = false;
                foreach (var p in BlacklistPrefixes)
                    if (an.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { skip = true; break; }
                if (skip) continue;
                if (an.StartsWith("UniversalConsole", StringComparison.OrdinalIgnoreCase)) continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t.IsAbstract || t.IsInterface) continue;
                    if (!typeof(IConsolePack).IsAssignableFrom(t)) continue;
                    if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                    try { out_.Add((IConsolePack)Activator.CreateInstance(t)); }
                    catch { }
                }
            }
            return out_;
        }
    }

    /// <summary>Manual pack list without reflection.</summary>
    public sealed class ManualPackLoader : IPackLoader
    {
        readonly List<IConsolePack> _packs = new List<IConsolePack>();

        /// <summary>Creates an empty loader.</summary>
        public ManualPackLoader() { }

        /// <summary>Creates a loader with an initial pack set.</summary>
        public ManualPackLoader(IEnumerable<IConsolePack> packs)
        {
            if (packs != null) _packs.AddRange(packs);
        }

        /// <summary>Adds a pack.</summary>
        public void Add(IConsolePack p) { if (p != null) _packs.Add(p); }

        /// <summary>Loads available packs.</summary>
        public IEnumerable<IConsolePack> Load() { return _packs; }
    }
}
