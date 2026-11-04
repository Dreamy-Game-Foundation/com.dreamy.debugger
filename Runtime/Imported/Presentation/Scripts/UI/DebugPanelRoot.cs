namespace Dreamy.Debugger.Presentation.UI
{
    using Scripts;
    using Services;
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.Service;
    using UnityEngine;

    public class DebugPanelRoot : ComponentBehaviour
    {
        [RequiredField] public Canvas Canvas;

        [RequiredField] public CanvasGroup CanvasGroup;

        [RequiredField] public DebuggerTabController TabController;

        public void Close()
        {
            if (Settings.Instance.UnloadOnClose)
            {
                ServiceManager.GetService<IDebugService>().DestroyDebugPanel();
            }
            else
            {
                ServiceManager.GetService<IDebugService>().HideDebugPanel();
            }
        }

        public void CloseAndDestroy()
        {
            ServiceManager.GetService<IDebugService>().DestroyDebugPanel();
        }
    }
}
