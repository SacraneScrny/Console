using System;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Screen, resolution and quality commands.</summary>
    internal sealed class ScreenBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public ScreenBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers screen commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("resolution", (Action<int, int, bool>)Resolution, "Set resolution: resolution 1920 1080 true", "screen");
            c.Command("quality", (Func<int, string>)Quality, "List or set quality: quality 2", "screen");
            c.Command("shot", (Action<string>)Shot, "Screenshot: shot bug.png", "screen");
            c.Command("display", (Func<string>)Display, "Resolution, dpi, orientation", "screen");
            c.Command("orient", (Action<string>)Orient, "Orientation: portrait landscape auto", "screen");
            c.Var("fullscreen", () => Screen.fullScreen, v => Screen.fullScreen = v, "Fullscreen window");
            c.Var("sleeptimeout", () => Screen.sleepTimeout, v => Screen.sleepTimeout = v, "Sleep timeout secs (-1 never, -2 system)");
            c.Var("brightness", () => Screen.brightness, v => Screen.brightness = CheckBrightness(v), "Screen brightness 0..1");
        }

        void Resolution(int w, int h, bool fullscreen = true)
        {
            if (w <= 0 || h <= 0) throw new ArgumentOutOfRangeException("resolution", "Width and height must be positive.");
            Screen.SetResolution(w, h, fullscreen);
            Print("Resolution " + w + "x" + h + " fullscreen=" + fullscreen + ".");
        }

        string Quality(int level = -1)
        {
            string[] names = QualitySettings.names;
            if (level < 0)
            {
                var lines = new System.Collections.Generic.List<string>();
                for (int i = 0; i < names.Length; i++)
                    lines.Add(i + (i == QualitySettings.GetQualityLevel() ? "*" : "") + ": " + names[i]);
                return string.Join("  ", lines.ToArray());
            }
            if (level >= names.Length) throw new ArgumentOutOfRangeException("quality", "Range 0.." + (names.Length - 1) + ".");
            QualitySettings.SetQualityLevel(level);
            return "Quality: " + names[level];
        }

        void Shot(string name = "shot.png")
        {
            if (string.IsNullOrEmpty(name)) name = "shot.png";
            ScreenCapture.CaptureScreenshot(name);
            Print("Saved " + name + ".");
        }

        string Display()
        {
            Resolution r = Screen.currentResolution;
            return Screen.width + "x" + Screen.height + " dpi=" + Screen.dpi + " refresh=" + r.refreshRate +
                " orient=" + Screen.orientation + " safe=" + Screen.safeArea + " fullscreen=" + Screen.fullScreen;
        }

        void Orient(string mode)
        {
            string m = (mode ?? "").Trim().ToLowerInvariant();
            if (m == "portrait") Screen.orientation = ScreenOrientation.Portrait;
            else if (m == "landscape" || m == "left") Screen.orientation = ScreenOrientation.LandscapeLeft;
            else if (m == "right") Screen.orientation = ScreenOrientation.LandscapeRight;
            else if (m == "auto") Screen.orientation = ScreenOrientation.AutoRotation;
            else throw new ArgumentException("orient: portrait landscape left right auto.");
            Print("Orientation " + Screen.orientation + ".");
        }

        static float CheckBrightness(float v)
        {
            if (v < 0f || v > 1f) throw new ArgumentOutOfRangeException("brightness", "Range 0..1.");
            return v;
        }
    }
}
