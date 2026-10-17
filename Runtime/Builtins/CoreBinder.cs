using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Console meta commands: version, history, clipboard.</summary>
    internal sealed class CoreBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public CoreBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers core commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("version", (Func<string>)Version, "Console and Unity version", "core");
            c.Command("history", (Action)History, "Last input lines", "core");
            c.Command("copy", (Action<string>)Copy, "Copy text: copy hello", "core");
            c.Command("paste", (Func<string>)Paste, "Print clipboard text", "core");
        }

        string Version()
        {
            return "Universal Console " + StandardPack.Version + " (Unity " + Application.unityVersion + ")";
        }

        void History()
        {
            List<string> lines = new List<string>();
            try
            {
                string raw = PlayerPrefs.GetString("UniversalConsole.History", "");
                if (!string.IsNullOrEmpty(raw))
                {
                    var parts = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in parts)
                    {
                        try { lines.Add(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p))); }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Print(Red("history: " + ex.Message));
                return;
            }
            if (lines.Count == 0) { Print("(empty)"); return; }
            int start = lines.Count > 20 ? lines.Count - 20 : 0;
            for (int i = start; i < lines.Count; i++)
                Print((i + 1) + ": " + lines[i]);
        }

        void Copy(string text)
        {
            GUIUtility.systemCopyBuffer = text ?? "";
            Print("Copied " + (text != null ? text.Length : 0) + " chars.");
        }

        string Paste()
        {
            string s = GUIUtility.systemCopyBuffer;
            return string.IsNullOrEmpty(s) ? "(empty)" : s;
        }
    }
}
