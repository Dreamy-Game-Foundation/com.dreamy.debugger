using UnityEngine;
using Dreamy.Debugger.Presentation;

namespace Dreamy.Debugger
{
    public sealed partial class ScreenshotMode
    {
        private RestoreHoldFeedbackView holdFeedback;
        private double shutterFeedbackUntil;
        private string shutterFeedbackTitle, shutterFeedbackHint;
        private float shutterFeedbackProgress;
        public bool IsShutterFeedbackVisible => CanUse && (HiddenShutterEnabled || IsUiHidden) && visibility != null && visibility.ActiveLeaseCount == (IsUiHidden ? 1 : 0) && Time.realtimeSinceStartupAsDouble < shutterFeedbackUntil;
        private void ShowShutterFeedback(string title, string hint, double seconds, float progress)
        {
            shutterFeedbackTitle = title; shutterFeedbackHint = hint; shutterFeedbackProgress = progress;
            shutterFeedbackUntil = Time.realtimeSinceStartupAsDouble + seconds;
        }
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
            if ((IsRestoreHoldIndicatorVisible || IsShutterFeedbackVisible) && holdFeedback == null)
            {
                var prefab = PresentationAssets.Load<GameObject>("DreamyDebugger/ScreenshotHoldFeedback");
                if (prefab != null)
                {
                    // Screen-space overlay stays at the scene root, independent of hidden UI ancestors.
                    holdFeedback = Instantiate(prefab).GetComponent<RestoreHoldFeedbackView>();
                    DontDestroyOnLoad(holdFeedback.gameObject);
                }
            }
            if (holdFeedback != null)
            {
                if (IsShutterFeedbackVisible) holdFeedback.PresentShutter(shutterFeedbackTitle, shutterFeedbackHint, shutterFeedbackProgress);
                else holdFeedback.Present(IsRestoreHoldIndicatorVisible, RestoreHoldProgress);
            }
        }
        private void HideRestoreHoldIndicator() { if (holdFeedback != null) holdFeedback.Present(false, 0); }
        private void ReleaseRestoreHoldIndicator()
        {
            if (holdFeedback != null) Destroy(holdFeedback.gameObject);
            holdFeedback = null;
        }
    }
}
