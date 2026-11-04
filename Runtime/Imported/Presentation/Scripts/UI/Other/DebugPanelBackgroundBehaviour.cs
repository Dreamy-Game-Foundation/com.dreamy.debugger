namespace Dreamy.Debugger.Presentation.UI.Other
{
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.UI;
    using UnityEngine;

    [RequireComponent(typeof (StyleComponent))]
    public class DebugPanelBackgroundBehaviour : BaseBehaviour
    {
        private StyleComponent _styleComponent;
        public string TransparentStyleKey = "";

        [SerializeField]
        private StyleSheet _styleSheet;

        private void Awake()
        {
            _styleComponent = GetComponent<StyleComponent>();

            if (Settings.Instance.EnableBackgroundTransparency)
            {
                // Update transparent style to have the transparency set in the settings menu.
                Style style = _styleSheet.GetStyle(TransparentStyleKey);
                // Theme authoring assets remain immutable; opacity is applied to this view only.
                var image=GetComponent<UnityEngine.UI.Image>();
                if(image!=null){var own=image.color;own.a=Settings.Instance.BackgroundTransparency;image.color=own;}

            }
        }
    }
}
