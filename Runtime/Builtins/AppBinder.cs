using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UniversalConsole
{
    /// <summary>Application and time commands.</summary>
    internal sealed class AppBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public AppBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers app commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("quit", (Action)Quit, "Quit app (editor: exit play mode)", "app");
            c.Command("pause", (Action)Pause, "Freeze time", "app");
            c.Command("resume", (Action)Resume, "Unfreeze time", "app");
            c.Command("restart", (Action)Restart, "Reload active scene", "app");
            c.Command("time", (Func<string>)TimeInfo, "Time scale and clocks", "app");
            c.Command("appinfo", (Func<string>)AppInfo, "Product, platform, language", "app");
            c.Var("timescale", () => Time.timeScale, v => Time.timeScale = CheckScale(v), "Time scale 0..20");
            c.Var("targetfps", () => Application.targetFrameRate, v => Application.targetFrameRate = CheckFps(v), "Target fps -1..240");
            c.Var("vsync", () => QualitySettings.vSyncCount, v => QualitySettings.vSyncCount = CheckVsync(v), "Vsync count 0..1");
            c.Var("runbg", () => Application.runInBackground, v => Application.runInBackground = v, "Run in background");
        }

        void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
            Print("Quitting.");
        }

        void Pause()
        {
            Time.timeScale = 0f;
            Print("Paused.");
        }

        void Resume()
        {
            Time.timeScale = 1f;
            Print("Resumed.");
        }

        void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        string TimeInfo()
        {
            float fps = Time.unscaledDeltaTime > 0.0001f ? 1f / Time.unscaledDeltaTime : 0f;
            return "scale=" + Time.timeScale + " time=" + Time.time + " unscaled=" + Time.realtimeSinceStartup +
                " frame=" + Time.frameCount + " fps=" + fps.ToString("F0");
        }

        string AppInfo()
        {
            return Application.productName + " by " + Application.companyName + " id=" + Application.identifier +
                " v=" + Application.version + " platform=" + Application.platform +
                " lang=" + Application.systemLanguage + " debug=" + Debug.isDebugBuild;
        }

        static float CheckScale(float v)
        {
            if (v < 0f || v > 20f) throw new ArgumentOutOfRangeException("timescale", "Range 0..20.");
            return v;
        }

        static int CheckFps(int v)
        {
            if (v < -1 || v > 240) throw new ArgumentOutOfRangeException("targetfps", "Range -1..240.");
            return v;
        }

        static int CheckVsync(int v)
        {
            if (v < 0 || v > 1) throw new ArgumentOutOfRangeException("vsync", "Range 0..1.");
            return v;
        }
    }
}
