using Dreamy.Debugger.Presentation.Services;
using Dreamy.Debugger.Foundation.Service;

namespace Dreamy.Debugger.Presentation
{
    using UnityEngine;

    public static class AutoInitialize
    {
#if UNITY_2018
        private const RuntimeInitializeLoadType InitializeLoadType = RuntimeInitializeLoadType.BeforeSceneLoad;
#else
        private const RuntimeInitializeLoadType InitializeLoadType = RuntimeInitializeLoadType.SubsystemRegistration;
#endif

        /// <summary>
        /// Initialize the console service before the scene has loaded to catch more of the initialization log.
        /// </summary>
        public static void OnLoadBeforeScene()
        {
            // Populate service manager with types from Dreamy.Debugger.Presentation assembly (asmdef)
            ServiceManager.RegisterAssembly<IDebugService>();

            if (Settings.Instance.IsEnabled)
            {
                // Initialize console if it hasn't already initialized.
                ServiceManager.GetService<IConsoleService>();
            }
        }

        /// <summary>
        /// Initialize Dreamy.Debugger.Presentation after the scene has loaded.
        /// </summary>
        public static void OnLoad()
        {
            if (Settings.Instance.IsEnabled)
            {
                DreamyDebug.Init();
            }
        }
    }
}