using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Entry point. Auto-created before scene load. Owns the registry, packs and overlay.</summary>
    [DisallowMultipleComponent]
    public sealed class ConsoleManager : MonoBehaviour
    {
        /// <summary>Singleton instance.</summary>
        public static ConsoleManager Instance { get; private set; }

        /// <summary>Shared command registry.</summary>
        public static ConsoleRegistry Registry { get; private set; }

        [Header("Behavior")]
        [Tooltip("Auto-discover all IConsolePack implementations.")]
        public bool autoDiscoverPacks = true;

        [Tooltip("Console open at startup.")]
        public bool startOpen = false;

        [Tooltip("Set Time.timeScale to 0 while open.")]
        public bool pauseWhileOpen = false;

        [Tooltip("Mirror Unity logs into the console.")]
        public bool captureUnityLogs = true;

        [Tooltip("Mirror Log (info) messages.")]
        public bool captureInfoLogs = true;

        [Tooltip("Mirror Warning messages.")]
        public bool captureWarningLogs = true;

        [Tooltip("Mirror Error and Exception messages.")]
        public bool captureErrorLogs = true;

        [Tooltip("Duplicate console output to the Unity console.")]
        public bool mirrorToUnityLog = true;

        [Tooltip("Show a small open button for touch devices.")]
        public bool showToggleButton = true;

        [Tooltip("Toggle key.")]
        public KeyCode toggleKey = KeyCode.BackQuote;

        [Tooltip("Alternative toggle key.")]
        public KeyCode toggleKeyAlt = KeyCode.F1;

        [Tooltip("Log pack and command counts to the Unity console at startup.")]
        public bool verboseStartup = true;

        readonly List<IConsole> _scopes = new List<IConsole>();
        IPackLoader _manualLoader;
        ConsoleOverlay _overlay;
        ConsoleRegistry _registry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            Registry = new ConsoleRegistry();
            PendingManual.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            if (Registry == null) Registry = new ConsoleRegistry();
            var go = new GameObject("UniversalConsoleManager");
            go.AddComponent<ConsoleManager>();
            DontDestroyOnLoad(go);
        }

        /// <summary>Registers a pack manually from code. Can be called before or after startup.</summary>
        public static void RegisterPack(IConsolePack pack)
        {
            if (pack == null) throw new ArgumentNullException("pack");
            if (Registry == null) Registry = new ConsoleRegistry();
            if (Instance != null) Instance.RegisterPackInternal(pack);
            else
            {
                PendingManual.Add(pack);
            }
        }

        static readonly List<IConsolePack> PendingManual = new List<IConsolePack>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (Registry == null) Registry = new ConsoleRegistry();
            _manualLoader = new ManualPackLoader();
        }

        void OnEnable()
        {
            if (Registry == null) Registry = new ConsoleRegistry();
            _registry = Registry;
            _registry.OnPrint += MirrorToUnityLog;
            Application.logMessageReceived += OnUnityLog;
            _overlay = gameObject.GetComponent<ConsoleOverlay>();
            if (_overlay == null) _overlay = gameObject.AddComponent<ConsoleOverlay>();
            _overlay.Setup(_registry, this);

            LoadAllPacks();
            if (verboseStartup)
            {
                try { Debug.Log("[Console] startup: packs=" + _scopes.Count + " commands=" + _registry.CommandCount + " vars=" + _registry.VarCount); }
                catch { }
            }
        }

        void OnDisable()
        {
            Application.logMessageReceived -= OnUnityLog;
            if (_registry != null) _registry.OnPrint -= MirrorToUnityLog;
            UnloadAllPacks();
            PendingManual.Clear();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LoadAllPacks()
        {
            UnloadAllPacks();

            var builtin = new BuiltinPack(_registry, () => { if (_overlay != null) _overlay.ClearScreen(); });
            RegisterPackInternal(builtin);

            foreach (var p in PendingManual) RegisterPackInternal(p);
            PendingManual.Clear();
            if (_manualLoader is ManualPackLoader ml)
                foreach (var p in ml.Load()) RegisterPackInternal(p);

            if (autoDiscoverPacks)
            {
                IPackLoader loader = new TypeCachePackLoader();
                foreach (var p in loader.Load())
                {
                    if (p is BuiltinPack) continue;
                    RegisterPackInternal(p);
                }
            }

            PrintLogo();
            Registry.Print("Universal Console ready. Type <b>help</b>.");
        }

        /// <summary>Prints the startup logo from the logo text asset.</summary>
        void PrintLogo()
        {
            TextAsset logo = null;
            try { logo = Resources.Load<TextAsset>("logo"); }
            catch { }
            if (logo == null || string.IsNullOrEmpty(logo.text)) return;
            string[] lines = logo.text.Split(new[] { '\n' }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                string line = raw.Length > 0 && raw[raw.Length - 1] == '\r' ? raw.Substring(0, raw.Length - 1) : raw;
                if (line.Length == 0) continue;
                _registry.Print("<color=#C9C9C9CC>" + line + "</color>");
            }
        }

        void RegisterPackInternal(IConsolePack pack)
        {
            var scope = _registry.CreateScope(pack.GetType().FullName + "@" + pack.GetHashCode());
            _scopes.Add(scope);
            try
            {
                pack.Build(scope);
            }
            catch (Exception ex)
            {
                _registry.Print("<color=red>Pack " + pack.GetType().Name + " failed: " + ex.Message + "</color>");
            }
        }

        void UnloadAllPacks()
        {
            foreach (var s in _scopes)
                try { s.Dispose(); } catch { }
            _scopes.Clear();
        }

        void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (!captureUnityLogs || _overlay == null) return;
            if (!string.IsNullOrEmpty(condition) && condition.StartsWith("[Console] ")) return;
            bool pass;
            if (type == LogType.Log) pass = captureInfoLogs;
            else if (type == LogType.Warning) pass = captureWarningLogs;
            else pass = captureErrorLogs;
            if (!pass) return;
            _overlay.EnqueueExternalLog(condition, type);
        }

        void MirrorToUnityLog(string line)
        {
            if (!mirrorToUnityLog) return;
            try { Debug.Log("[Console] " + line); }
            catch { }
        }

        internal bool IsOpen() { return _overlay != null && _overlay.IsOpen; }
        internal void SetOpen(bool open) { if (_overlay != null) _overlay.SetOpen(open); }
    }
}
