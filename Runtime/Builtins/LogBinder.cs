using System;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Log emitters and capture toggles.</summary>
    internal sealed class LogBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public LogBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers log commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("log", (Action<string>)Log, "Info message: log hello", "log");
            c.Command("warn", (Action<string>)Warn, "Warning message: warn slow", "log");
            c.Command("err", (Action<string>)Err, "Error message: err boom", "log");
            c.Var("cap_info", () => ManagerFlag(m => m.captureInfoLogs, true), v => SetFlag((m, x) => m.captureInfoLogs = x, v), "Capture info logs");
            c.Var("cap_warn", () => ManagerFlag(m => m.captureWarningLogs, true), v => SetFlag((m, x) => m.captureWarningLogs = x, v), "Capture warnings");
            c.Var("cap_err", () => ManagerFlag(m => m.captureErrorLogs, true), v => SetFlag((m, x) => m.captureErrorLogs = x, v), "Capture errors");
            c.Var("mirror", () => ManagerFlag(m => m.mirrorToUnityLog, true), v => SetFlag((m, x) => m.mirrorToUnityLog = x, v), "Mirror to Unity console");
        }

        void Log(string text)
        {
            Debug.Log(text);
        }

        void Warn(string text)
        {
            Debug.LogWarning(text);
        }

        void Err(string text)
        {
            Debug.LogError(text);
        }

        static bool ManagerFlag(Func<ConsoleManager, bool> read, bool fallback)
        {
            var m = ConsoleManager.Instance;
            return m != null ? read(m) : fallback;
        }

        static void SetFlag(Action<ConsoleManager, bool> write, bool v)
        {
            var m = ConsoleManager.Instance;
            if (m != null) write(m, v);
        }
    }
}
