using System;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Memory info and collection commands.</summary>
    internal sealed class MemBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public MemBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers memory commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("mem", (Func<string>)Mem, "Managed and native heap", "mem");
            c.Command("gc", (Action)Gc, "Collect garbage now", "mem");
        }

        string Mem()
        {
            long mono = GC.GetTotalMemory(false);
            long unity = UnityEngine.Profiling.Profiler.usedHeapSizeLong;
            return "mono=" + (mono / 1048576L) + "MB unity=" + (unity / 1048576L) + "MB";
        }

        void Gc()
        {
            GC.Collect();
            Resources.UnloadUnusedAssets();
            Print("Collected.");
        }
    }
}
