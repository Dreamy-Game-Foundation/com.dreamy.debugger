namespace Dreamy.Debugger.Presentation.UI.Other
{
    using Controls;
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.UI;
    using UnityEngine;
    using UnityEngine.Serialization;

    public class TriggerRoot : ComponentBehaviour
    {
        [RequiredField] public Canvas Canvas;

        [RequiredField] public LongPressButton TapHoldButton;

        [RequiredField] public RectTransform TriggerTransform;

        [RequiredField] public ErrorNotifier ErrorNotifier;

        [RequiredField] [FormerlySerializedAs("TriggerButton")] public MultiTapButton TripleTapButton;
    }
}
