using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Registers the standard pack without touching console startup code.</summary>
    public static class StandardBootstrap
    {
        /// <summary>Queues the standard pack before scene load.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register()
        {
            try
            {
                if (StandardPack.Enabled) ConsoleManager.RegisterPack(new StandardPack());
            }
            catch { }
        }
    }
}
