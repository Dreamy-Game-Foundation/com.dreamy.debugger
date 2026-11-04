namespace Dreamy.Debugger.Presentation.UI.Other
{
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.UI.Layout;
    using UnityEngine;
    using UnityEngine.UI;

    public class PinnedUIRoot : ComponentBehaviour
    {
        [RequiredField] public Canvas Canvas;

        [RequiredField] public RectTransform Container;

        [RequiredField] public DockConsoleController DockConsoleController;

        [RequiredField] public GameObject Options;

        [RequiredField] public FlowLayoutGroup OptionsLayoutGroup;

        [RequiredField] public GameObject Profiler;

        [RequiredField] public HandleManager ProfilerHandleManager;

        [RequiredField] public VerticalLayoutGroup ProfilerVerticalLayoutGroup;
    }
}
