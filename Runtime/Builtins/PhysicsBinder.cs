using System;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Physics world commands.</summary>
    internal sealed class PhysicsBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public PhysicsBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers physics commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Var("gravity", () => Physics.gravity, v => Physics.gravity = v, "World gravity x,y,z");
            c.Var("fixed_dt", () => Time.fixedDeltaTime, v => Time.fixedDeltaTime = CheckDt(v), "Fixed timestep 0.0001..1");
        }

        static float CheckDt(float v)
        {
            if (v < 0.0001f || v > 1f) throw new ArgumentOutOfRangeException("fixed_dt", "Range 0.0001..1.");
            return v;
        }
    }
}
