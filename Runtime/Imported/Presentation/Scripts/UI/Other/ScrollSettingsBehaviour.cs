namespace Dreamy.Debugger.Presentation.UI.Other
{
    using UnityEngine;
    using UnityEngine.UI;

    [RequireComponent(typeof (ScrollRect))]
    public class ScrollSettingsBehaviour : MonoBehaviour
    {
        public const float ScrollSensitivity = 40f;

        private void Awake()
        {
            var scrollRect = GetComponent<ScrollRect>();
            scrollRect.scrollSensitivity = ScrollSensitivity;

            if (!Internal.PresentationUtility.IsMobilePlatform)
            {
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.inertia = false;
            }
        }
    }
}
