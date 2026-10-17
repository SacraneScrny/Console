using System;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Render settings commands.</summary>
    internal sealed class RenderBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public RenderBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers render commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("shadows", (Func<string, string>)Shadows, "Shadow mode: off hard soft all", "render");
            c.Var("fog", () => RenderSettings.fog, v => RenderSettings.fog = v, "Scene fog on/off");
        }

        string Shadows(string mode = "")
        {
            string m = (mode ?? "").Trim().ToLowerInvariant();
            if (m.Length == 0) return "shadows=" + QualitySettings.shadows + " dist=" + QualitySettings.shadowDistance;
            if (m == "off") QualitySettings.shadows = ShadowQuality.Disable;
            else if (m == "hard") QualitySettings.shadows = ShadowQuality.HardOnly;
            else if (m == "soft" || m == "all") QualitySettings.shadows = ShadowQuality.All;
            else throw new ArgumentException("shadows: off hard soft all.");
            return "shadows=" + QualitySettings.shadows;
        }
    }
}
