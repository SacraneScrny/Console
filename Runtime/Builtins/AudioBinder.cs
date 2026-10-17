using System;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Audio commands.</summary>
    internal sealed class AudioBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public AudioBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers audio commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Var("mastervol", () => AudioListener.volume, v => AudioListener.volume = CheckVol(v), "Master volume 0..1");
            c.Var("muted", () => AudioListener.pause, v => AudioListener.pause = v, "Mute all audio");
        }

        static float CheckVol(float v)
        {
            if (v < 0f || v > 1f) throw new ArgumentOutOfRangeException("mastervol", "Range 0..1.");
            return v;
        }
    }
}
