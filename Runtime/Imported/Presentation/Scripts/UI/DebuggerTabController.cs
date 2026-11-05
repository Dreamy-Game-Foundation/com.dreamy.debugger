namespace Dreamy.Debugger.Presentation.Scripts
{
    using System;
    using System.Linq;
    using Dreamy.Debugger.Foundation;
    using UI.Other;
    using UnityEngine;

    public class DebuggerTabController : ComponentBehaviour
    {
        private DebugTab _aboutTabInstance;
        private DefaultTabs? _activeTab;
        private bool _hasStarted;
        public DebugTab AboutTab;

        [RequiredField] public DebugTabController TabController;

        public DefaultTabs? ActiveTab
        {
            get
            {
                var key = TabController.ActiveTab.Key;

                if (string.IsNullOrEmpty(key))
                {
                    return null;
                }

                var t = Enum.Parse(typeof (DefaultTabs), key);

                if (!Enum.IsDefined(typeof (DefaultTabs), t))
                {
                    return null;
                }

                return (DefaultTabs) t;
            }
        }

        protected override void Start()
        {
            base.Start();

            _hasStarted = true;

            // Loads all available tabs from resources
            var tabs = PresentationAssets.LoadAll<DebugTab>("DreamyDebugger/UI/Prefabs/Tabs");
            var defaultTabs = Enum.GetNames(typeof (DefaultTabs));

            foreach (var srTab in tabs)
            {
                // Package support reporting belongs to Editor Help, never the game panel.
                if (srTab.Key == DefaultTabs.BugReporter.ToString()) continue;
                var enabler = srTab.GetComponent(typeof (IEnableTab)) as IEnableTab;

                if (enabler != null && !enabler.IsEnabled)
                {
                    continue;
                }

                if (defaultTabs.Contains(srTab.Key))
                {
                    var tabValue = Enum.Parse(typeof (DefaultTabs), srTab.Key);

                    if (Enum.IsDefined(typeof (DefaultTabs), tabValue) &&
                        Settings.Instance.DisabledTabs.Contains((DefaultTabs) tabValue))
                    {
                        continue;
                    }
                }

                TabController.AddTab(ObjectFactory.Instantiate(srTab));
            }

            // Add about tab (has no button, accessed via "Stompy" logo
            if (AboutTab != null)
            {
                _aboutTabInstance = ObjectFactory.Instantiate(AboutTab);
                TabController.AddTab(_aboutTabInstance, false);
            }

            // Open active tab (set before panel loaded), or default tab from settings
            var defaultTab = _activeTab ?? Settings.Instance.DefaultTab;

            if (!OpenTab(defaultTab))
            {
                TabController.ActiveTab = TabController.Tabs.FirstOrDefault();
            }
        }

        public bool OpenTab(DefaultTabs tab)
        {
            if (!_hasStarted)
            {
                _activeTab = tab;
                return true;
            }

            var tabName = tab.ToString();

            foreach (var t in TabController.Tabs)
            {
                if (t.Key == tabName)
                {
                    TabController.ActiveTab = t;
                    return true;
                }
            }

            return false;
        }

        public void ShowAboutTab()
        {
            if (_aboutTabInstance != null)
            {
                TabController.ActiveTab = _aboutTabInstance;
            }
        }
    }
}
