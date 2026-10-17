using System;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Misc utility commands.</summary>
    internal sealed class UtilBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public UtilBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers utility commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("url", (Action<string>)Url, "Open address: url example.com", "util");
            c.Command("sleep", (Action<int>)Sleep, "Freeze ms 0..5000: sleep 500", "util");
            c.Command("boom", (Action)Boom, "Throw a test exception", "util");
            c.Command("seed", (Action<int>)Seed, "Random seed: seed 42", "util");
            c.Command("repeat", (Action<int, string>)Repeat, "Run N times: repeat 3 echo hi", "util");
            c.Command("vibrate", (Action)Vibrate, "Vibrate device", "util");
        }

        void Url(string address)
        {
            Application.OpenURL(address);
            Print("Opened " + address + ".");
        }

        void Sleep(int ms)
        {
            int v = ClampInt(ms, 0, 5000);
            System.Threading.Thread.Sleep(v);
            Print("Slept " + v + " ms.");
        }

        void Boom()
        {
            throw new InvalidOperationException("boom");
        }

        void Seed(int seed)
        {
            UnityEngine.Random.InitState(seed);
            Print("Seed " + seed + ".");
        }

        void Repeat(int times, string command)
        {
            int n = ClampInt(times, 1, 1000);
            string cmd = (command ?? "").Trim();
            if (cmd.Length == 0) throw new ArgumentException("repeat: empty command.");
            if (cmd.StartsWith("repeat", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("repeat: nesting is not allowed.");
            for (int i = 0; i < n; i++) Reg.Execute(cmd);
            Print("Ran " + n + ".");
        }

        void Vibrate()
        {
            Handheld.Vibrate();
            Print("Vibrated.");
        }
    }
}
