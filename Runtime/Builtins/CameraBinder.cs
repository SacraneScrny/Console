using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Camera list command.</summary>
    internal sealed class CameraBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public CameraBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers camera commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("cameras", (Func<string>)Cameras, "List cameras", "camera");
        }

        string Cameras()
        {
            var all = Camera.allCameras;
            if (all == null || all.Length == 0) return "(no cameras)";
            Camera main = null;
            try { main = Camera.main; }
            catch { }
            var lines = new List<string>();
            foreach (var cam in all)
            {
                if (cam == null) continue;
                lines.Add(cam.name + " pos=" + cam.transform.position + " fov=" + cam.fieldOfView +
                    (cam == main ? " [main]" : ""));
            }
            return string.Join("\n", lines.ToArray());
        }
    }
}
