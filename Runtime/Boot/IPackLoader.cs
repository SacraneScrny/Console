using System.Collections.Generic;

namespace UniversalConsole
{
    /// <summary>Defines how packs are provided at runtime.</summary>
    public interface IPackLoader
    {
        /// <summary>Loads available packs.</summary>
        IEnumerable<IConsolePack> Load();
    }
}
