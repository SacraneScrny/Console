namespace UniversalConsole
{
    /// <summary>Marker for user packs. All bindings are defined inside Build.</summary>
    public interface IConsolePack
    {
        /// <summary>Registers commands and variables of this pack.</summary>
        void Build(IConsole c);
    }
}
