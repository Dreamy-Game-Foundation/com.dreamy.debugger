using UnityEngine;
using Dreamy.Debugger.Foundation.Service;
using Dreamy.Debugger.Presentation.Internal;
namespace Dreamy.Debugger.Presentation.Services.Implementation {
    public class KeyboardShortcutListenerService : ServiceBase<KeyboardShortcutListenerService> {
        private void ToggleTab(DefaultTabs t)
        {
            var activeTab = Service.Panel.ActiveTab;

            if (Service.Panel.IsVisible && activeTab.HasValue && activeTab.Value == t)
            {
                DreamyDebug.Instance.HideDebugPanel();
            }
            else
            {
                DreamyDebug.Instance.ShowDebugPanel(t);
            }
        }

        private void ExecuteShortcut(Settings.KeyboardShortcut shortcut)
        {
            switch (shortcut.Action)
            {
                case Settings.ShortcutActions.OpenSystemInfoTab:

                    ToggleTab(DefaultTabs.SystemInformation);

                    break;

                case Settings.ShortcutActions.OpenConsoleTab:

                    ToggleTab(DefaultTabs.Console);

                    break;

                case Settings.ShortcutActions.OpenOptionsTab:

                    ToggleTab(DefaultTabs.Options);

                    break;

                case Settings.ShortcutActions.OpenProfilerTab:

                    ToggleTab(DefaultTabs.Profiler);

                    break;

                case Settings.ShortcutActions.OpenBugReporterTab:

                    ToggleTab(DefaultTabs.BugReporter);

                    break;

                case Settings.ShortcutActions.ClosePanel:

                    DreamyDebug.Instance.HideDebugPanel();

                    break;

                case Settings.ShortcutActions.OpenPanel:

                    DreamyDebug.Instance.ShowDebugPanel();

                    break;

                case Settings.ShortcutActions.TogglePanel:

                    if (DreamyDebug.Instance.IsDebugPanelVisible)
                    {
                        DreamyDebug.Instance.HideDebugPanel();
                    }
                    else
                    {
                        DreamyDebug.Instance.ShowDebugPanel();
                    }

                    break;

                case Settings.ShortcutActions.ShowBugReportPopover:

                    DreamyDebug.Instance.ShowBugReportSheet();

                    break;

                case Settings.ShortcutActions.ToggleDockedConsole:

                    DreamyDebug.Instance.DockConsole.IsVisible = !DreamyDebug.Instance.DockConsole.IsVisible;

                    break;

                case Settings.ShortcutActions.ToggleDockedProfiler:

                    DreamyDebug.Instance.IsProfilerDocked = !DreamyDebug.Instance.IsProfilerDocked;

                    break;

                default:

                    Debug.LogWarning("[DreamyDebugger] Unhandled keyboard shortcut: " + shortcut.Action);

                    break;
            }
        }


        protected override void Update() {
            base.Update();
            foreach(var shortcut in Settings.Instance.KeyboardShortcuts) {
                if(!InputBridge.Down(shortcut.Key))continue;
                if(shortcut.Control && !InputBridge.IsHeld(KeyCode.LeftControl) && !InputBridge.IsHeld(KeyCode.RightControl))continue;
                if(shortcut.Shift && !InputBridge.IsHeld(KeyCode.LeftShift) && !InputBridge.IsHeld(KeyCode.RightShift))continue;
                if(shortcut.Alt && !InputBridge.IsHeld(KeyCode.LeftAlt) && !InputBridge.IsHeld(KeyCode.RightAlt))continue;
                ExecuteShortcut(shortcut);
            }
        }
    }
}
