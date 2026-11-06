using System;
using System.Linq;
using UnityEngine;
using Dreamy.Debugger.Presentation;

namespace Dreamy.Debugger
{
    public enum DebuggerLoadingMode { Automatic, Manual }
    /// <summary>One startup path for Editor provider, build payload and explicit/manual initialization.</summary>
    public static class DebuggerBootstrap
    {
        public const string ResourcePath = "DreamyDebugger/Startup";
        public static Func<DebuggerStartupAssets> AssetProvider { get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            if (!Application.isEditor) AssetProvider = null;
            DebuggerRuntime.Instance?.Shutdown();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutomaticStart()
        {
            var authored = UnityEngine.Object.FindObjectsByType<DebuggerRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(runtime => runtime.gameObject.scene.IsValid() && !runtime.IsAutomatic).ToArray();
            // Respect even inactive/Off/disabled authored setups.
            if (authored.Length != 0)
            {
                foreach (var runtime in authored) if (runtime.isActiveAndEnabled) runtime.EnsureInitialized();
                return;
            }
            var assets = Resolve();
            if (assets != null && assets.Loading == DebuggerLoadingMode.Automatic) InitializeFrom(assets, true);
        }
        public static DebuggerStartupAssets Resolve() => AssetProvider != null ? AssetProvider() : Resources.Load<DebuggerStartupAssets>(ResourcePath);
        public static DebuggerRuntime Initialize(DebuggerConfiguration configuration = null, DebuggerAssetCatalog catalog = null)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Initialize the debugger while the game is playing.");
            if (DebuggerRuntime.Instance != null) return DebuggerRuntime.Instance;
            var authored = UnityEngine.Object.FindObjectsByType<DebuggerRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(runtime => runtime.gameObject.scene.IsValid() && !runtime.IsAutomatic);
            if (authored != null) { if (authored.isActiveAndEnabled) authored.EnsureInitialized(); return DebuggerRuntime.Instance; }
            var assets = Resolve();
            if (configuration == null && assets != null) configuration = assets.Configuration;
            if (catalog == null && assets != null) catalog = assets.Catalog;
            return Create(configuration, catalog, false);
        }
        private static DebuggerRuntime InitializeFrom(DebuggerStartupAssets assets, bool automatic)
        {
            if (DebuggerRuntime.Instance != null) return DebuggerRuntime.Instance;
            return Create(assets.Configuration, assets.Catalog, automatic);
        }
        private static DebuggerRuntime Create(DebuggerConfiguration config, DebuggerAssetCatalog catalog, bool automatic)
        {
            var mode = config != null ? config.Mode : DebuggerMode.Development;
            if (mode == DebuggerMode.Off || !Debug.isDebugBuild && mode != DebuggerMode.ReleaseDiagnostics) return null;
            var root = new GameObject("Dreamy Debugger Runtime"); root.SetActive(false);
            var runtime = root.AddComponent<DebuggerRuntime>(); runtime.IsAutomatic = automatic;
            runtime.ConfigurationAsset = config; runtime.Catalog = catalog; root.SetActive(true);
            return DebuggerRuntime.Instance;
        }
    }
}
