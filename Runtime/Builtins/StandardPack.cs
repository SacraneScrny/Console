using UnityEngine.Scripting;

namespace UniversalConsole
{
    /// <summary>Standard cross-project command set. Registered automatically at startup.</summary>
    [Preserve]
    public sealed class StandardPack : IConsolePack
    {
        /// <summary>Package version shown by the version command.</summary>
        public const string Version = "0.1.0";

        /// <summary>Master switch. Set false before startup to skip standard commands.</summary>
        public static bool Enabled = true;

        /// <summary>Creates the pack.</summary>
        public StandardPack() { }

        /// <summary>Binds every standard domain binder.</summary>
        [Preserve]
        public void Build(IConsole c)
        {
            ConsoleRegistry reg = ConsoleManager.Registry;
            if (reg == null) return;
            new CoreBinder(reg).Bind(c);
            new AppBinder(reg).Bind(c);
            new ScreenBinder(reg).Bind(c);
            new RenderBinder(reg).Bind(c);
            new SceneBinder(reg).Bind(c);
            new PrefsBinder(reg).Bind(c);
            new LogBinder(reg).Bind(c);
            new PhysicsBinder(reg).Bind(c);
            new AudioBinder(reg).Bind(c);
            new CameraBinder(reg).Bind(c);
            new InputBinder(reg).Bind(c);
            new DeviceBinder(reg).Bind(c);
            new MemBinder(reg).Bind(c);
            new UtilBinder(reg).Bind(c);
        }
    }
}
