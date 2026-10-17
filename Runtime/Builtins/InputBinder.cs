using System;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Cursor lock command.</summary>
    internal sealed class InputBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public InputBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers input commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("cursor", (Func<string, string>)Cursor, "Cursor lock: lock none confined", "input");
        }

        string Cursor(string mode = "")
        {
            string m = (mode ?? "").Trim().ToLowerInvariant();
            if (m.Length == 0) return "lock=" + UnityEngine.Cursor.lockState + " visible=" + UnityEngine.Cursor.visible;
            if (m == "lock" || m == "locked") UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            else if (m == "none") UnityEngine.Cursor.lockState = CursorLockMode.None;
            else if (m == "confined") UnityEngine.Cursor.lockState = CursorLockMode.Confined;
            else throw new ArgumentException("cursor: lock none confined.");
            return "lock=" + UnityEngine.Cursor.lockState;
        }
    }
}
