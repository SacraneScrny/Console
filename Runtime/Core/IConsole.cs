using System;

namespace UniversalConsole
{
    /// <summary>Facade visible to packs inside Build. Disposing removes everything registered by the pack.</summary>
    public interface IConsole : IDisposable
    {
        /// <summary>Registers a command.</summary>
        void Command(string name, Delegate handler, string help = "", string category = "", params string[] aliases);

        /// <summary>Registers a readable/writable variable.</summary>
        void Var<T>(string name, Func<T> getter, Action<T> setter, string help = "");

        /// <summary>Registers a read-only variable.</summary>
        void VarReadOnly<T>(string name, Func<T> getter, string help = "");

        /// <summary>Creates a child scope with the "prefix." name prefix.</summary>
        IConsole Scope(string prefix);

        /// <summary>Writes a line to the output.</summary>
        void Print(string line);
    }
}
