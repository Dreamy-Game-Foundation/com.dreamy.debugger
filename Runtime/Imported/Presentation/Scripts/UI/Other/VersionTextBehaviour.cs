namespace Dreamy.Debugger.Presentation.UI.Other
{
    using Dreamy.Debugger.Foundation;
    using UnityEngine.UI;

    public class VersionTextBehaviour : ComponentBehaviour
    {
        public string Format = "DreamyDebugger {0}";

        [RequiredField] public Text Text;

        protected override void Start()
        {
            base.Start();

            Text.text = string.Format(Format, DreamyDebug.Version);
        }
    }
}
