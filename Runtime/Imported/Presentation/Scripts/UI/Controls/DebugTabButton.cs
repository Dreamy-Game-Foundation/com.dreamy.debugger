namespace Dreamy.Debugger.Presentation.UI.Controls
{
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.UI;
    using UnityEngine;
    using UnityEngine.UI;

    public class DebugTabButton : ComponentBehaviour
    {
        [RequiredField] public Behaviour ActiveToggle;

        [RequiredField] public UnityEngine.UI.Button Button;

        [RequiredField] public RectTransform ExtraContentContainer;

        [RequiredField] public StyleComponent IconStyleComponent;

        [RequiredField] public Text TitleText;

        public bool IsActive
        {
            get { return ActiveToggle.enabled; }
            set { ActiveToggle.enabled = value; }
        }
    }
}
