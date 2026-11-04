namespace Dreamy.Debugger.Presentation.UI.Other
{
    using Dreamy.Debugger.Foundation;

    public class SetLayerFromSettings : BaseBehaviour
    {
        private void Start()
        {
            gameObject.SetLayerRecursive(Settings.Instance.DebugLayer);
        }
    }
}
