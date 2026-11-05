namespace Dreamy.Debugger.Presentation.Internal
{
    using Services;
    using Dreamy.Debugger.Foundation.Service;

    public static class Service
    {
        private static int generation=-1;
        private static void EnsureGeneration(){
            if(generation==ServiceManager.Generation)return;
            generation=ServiceManager.Generation;
            _consoleService=null;_debugPanelService=null;_debugTriggerService=null;_pinnedUiService=null;_debugCameraService=null;_optionsService=null;_dockConsoleService=null;
        }
        private static IConsoleService _consoleService;
        private static IDebugPanelService _debugPanelService;
        private static IDebugTriggerService _debugTriggerService;
        private static IPinnedUIService _pinnedUiService;
        private static IDebugCameraService _debugCameraService;
        private static IOptionsService _optionsService;
        private static IDockConsoleService _dockConsoleService;

#if UNITY_EDITOR && ((!UNITY_2017 && !UNITY_2018 && !UNITY_2019) || UNITY_2019_3_OR_NEWER)
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void RuntimeInitialize()
        {
            // Clear service references at startup in case of "enter play mode without domain reload"
            _consoleService = null;
            _debugPanelService = null;
            _debugTriggerService = null;
            _pinnedUiService = null;
            _debugCameraService = null;
            _optionsService = null;
            _dockConsoleService = null;
        }
#endif

        public static IConsoleService Console
        {
            get
            {
                EnsureGeneration();
                if (_consoleService == null)
                {
                    _consoleService = ServiceManager.GetService<IConsoleService>();
                }

                return _consoleService;
            }
        }

        public static IDockConsoleService DockConsole
        {
            get
            {
                EnsureGeneration();
                if (_dockConsoleService == null)
                {
                    _dockConsoleService = ServiceManager.GetService<IDockConsoleService>();
                }

                return _dockConsoleService;
            }
        }

        public static IDebugPanelService Panel
        {
            get
            {
                EnsureGeneration();
                if (_debugPanelService == null)
                {
                    _debugPanelService = ServiceManager.GetService<IDebugPanelService>();
                }

                return _debugPanelService;
            }
        }

        public static IDebugTriggerService Trigger
        {
            get
            {
                EnsureGeneration();
                if (_debugTriggerService == null)
                {
                    _debugTriggerService = ServiceManager.GetService<IDebugTriggerService>();
                }

                return _debugTriggerService;
            }
        }

        public static IPinnedUIService PinnedUI
        {
            get
            {
                EnsureGeneration();
                if (_pinnedUiService == null)
                {
                    _pinnedUiService = ServiceManager.GetService<IPinnedUIService>();
                }

                return _pinnedUiService;
            }
        }

        public static IDebugCameraService DebugCamera
        {
            get
            {
                EnsureGeneration();
                if (_debugCameraService == null)
                {
                    _debugCameraService = ServiceManager.GetService<IDebugCameraService>();
                }

                return _debugCameraService;
            }
        }

        public static IOptionsService Options
        {
            get
            {
                EnsureGeneration();
                if (_optionsService == null)
                {
                    _optionsService = ServiceManager.GetService<IOptionsService>();
                }

                return _optionsService;
            }
        }
    }
}
