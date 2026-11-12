using UnityEngine;
using Dreamy.Debugger.Presentation;

namespace Dreamy.Debugger
{
    public sealed partial class ScreenshotMode
    {
        private RestoreHoldFeedbackView holdFeedback;
        public GameObject HoldFeedbackRoot => holdFeedback != null ? holdFeedback.gameObject : null;
        public float RestoreHoldProgress
        {
            get
            {
                if (!IsUiHidden) return 0;
                var now = Time.realtimeSinceStartupAsDouble;
                var corner = cornerStarted < 0 ? 0 : (float)((now - cornerStarted) / 1.5);
                var fingers = touchStarted < 0 ? 0 : (float)((now - touchStarted) / Mathf.Max(.25f, RestoreTouchHoldSeconds));
                return Mathf.Clamp01(Mathf.Max(corner, fingers));
            }
        }
        public bool IsRestoreHoldIndicatorVisible => CanUse && IsUiHidden && visibility != null &&
            visibility.ActiveLeaseCount == 1 && (cornerStarted >= 0 || touchStarted >= 0);

        private void UpdateRestoreHoldIndicator()
        {
            if (IsRestoreHoldIndicatorVisible && holdFeedback == null)
            {
                var prefab = PresentationAssets.Load<GameObject>("DreamyDebugger/ScreenshotHoldFeedback");
                if (prefab != null)
                {
                    // Screen-space overlay stays at the scene root, independent of hidden UI ancestors.
                    holdFeedback = Instantiate(prefab).GetComponent<RestoreHoldFeedbackView>();
                    DontDestroyOnLoad(holdFeedback.gameObject);
                }
            }
            if (holdFeedback != null) holdFeedback.Present(IsRestoreHoldIndicatorVisible, RestoreHoldProgress);
        }
        private void HideRestoreHoldIndicator() { if (holdFeedback != null) holdFeedback.Present(false, 0); }
        private void ReleaseRestoreHoldIndicator()
        {
            if (holdFeedback != null) Destroy(holdFeedback.gameObject);
            holdFeedback = null;
        }
    }
}
