using System;
using System.Collections.Generic;

namespace UniversalConsole
{
    internal sealed class BuiltinPack : IConsolePack
    {
        readonly ConsoleRegistry _reg;
        readonly Action _clearScreen;
        public BuiltinPack(ConsoleRegistry reg, Action clearScreen)
        {
            _reg = reg;
            _clearScreen = clearScreen;
        }

        public void Build(IConsole c)
        {
            c.Command("help", (Func<string, string>)Help, "Show commands: help [filter]");
            c.Command("list", (Action)List, "Short name list");
            c.Command("echo", (Func<string, string>)Echo, "Print text");
            c.Command("clear", (Action)Clear, "Clear screen");
        }

        string Help(string filter = "")
        {
            var lines = new List<string>();
            _reg.GetHelp(lines);
            if (!string.IsNullOrEmpty(filter))
                lines.RemoveAll(l => l.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0);
            if (lines.Count == 0) return "No commands.";
            return string.Join("\n", lines.ToArray());
        }

        void List()
        {
            var lines = new List<string>();
            _reg.GetHelp(lines);
            var names = new List<string>();
            foreach (var l in lines)
            {
                int sp = l.IndexOf(' ');
                names.Add(sp < 0 ? l : l.Substring(0, sp));
            }
            _reg.Print(string.Join("  ", names.ToArray()));
        }

        string Echo(string text) { return text; }

        void Clear()
        {
            if (_clearScreen != null) _clearScreen();
        }
    }
}
