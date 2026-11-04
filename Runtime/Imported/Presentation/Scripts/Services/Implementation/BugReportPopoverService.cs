namespace Dreamy.Debugger.Presentation.Services.Implementation
{
    using System;
    using System.Collections;
    using Internal;
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.Service;
    using UI.Other;
    using UnityEngine;

    [Service(typeof (BugReportPopoverService))]
    public class BugReportPopoverService : ServiceBase<BugReportPopoverService>
    {
        private BugReportCompleteCallback _callback;
        private bool _isVisible;
        private BugReportPopoverRoot _popover;
        private BugReportSheetController _sheet;

        public bool IsShowingPopover
        {
            get { return _isVisible; }
        }

        public void ShowBugReporter(BugReportCompleteCallback callback, bool takeScreenshotFirst = true,
            string descriptionText = null)
        {
            if (_isVisible)
            {
                throw new InvalidOperationException("Bug report popover is already visible.");
            }

            if (_popover == null)
            {
                Load();
            }

            if (_popover == null)
            {
                Debug.LogWarning("[DreamyDebugger] Bug report popover failed loading, executing callback with fail result");
                callback(false, "Resource load failed");
                return;
            }

            _callback = callback;

            _isVisible = true;
            PresentationUtility.EnsureEventSystemExists();

            StartCoroutine(OpenCo(takeScreenshotFirst, descriptionText));
        }

        private IEnumerator OpenCo(bool takeScreenshot, string descriptionText)
        {
            if (takeScreenshot)
            {
                // Wait for screenshot to be captured
                yield return StartCoroutine(BugReportScreenshotUtil.ScreenshotCaptureCo());
            }
            _popover.CachedGameObject.SetActive(true);

            yield return new WaitForEndOfFrame();

            if (!string.IsNullOrEmpty(descriptionText))
            {
                _sheet.DescriptionField.text = descriptionText;
            }
        }

        private void SubmitComplete(bool didSucceed, string errorMessage)
        {
            OnComplete(didSucceed, errorMessage, false);
        }

        private void CancelPressed()
        {
            OnComplete(false, "User Cancelled", true);
        }

        private void OnComplete(bool success, string errorMessage, bool close)
        {
            if (!_isVisible)
            {
                Debug.LogWarning("[DreamyDebugger] Received callback at unexpected time. ???");
                return;
            }

            if (!success && !close)
            {
                return;
            }

            _isVisible = false;

            // Destroy it all so it doesn't linger in the scene using memory
            _popover.gameObject.SetActive(false);
            Destroy(_popover.gameObject);

            _popover = null;
            _sheet = null;

            BugReportScreenshotUtil.ScreenshotData = null;

            _callback(success, errorMessage);
        }

        private void TakingScreenshot()
        {
            if (!IsShowingPopover)
            {
                Debug.LogWarning("[DreamyDebugger] Received callback at unexpected time. ???");
                return;
            }

            _popover.CanvasGroup.alpha = 0f;
        }

        private void ScreenshotComplete()
        {
            if (!IsShowingPopover)
            {
                Debug.LogWarning("[DreamyDebugger] Received callback at unexpected time. ???");
                return;
            }

            _popover.CanvasGroup.alpha = 1f;
        }

        protected override void Awake()
        {
            base.Awake();

            CachedTransform.SetParent(Hierarchy.Get("DreamyDebugger"));
        }

        private void Load()
        {
            var popoverPrefab = PresentationAssets.Load<BugReportPopoverRoot>(PanelPaths.BugReportPopoverPath);
            var sheetPrefab = PresentationAssets.Load<BugReportSheetController>(PanelPaths.BugReportSheetPath);

            if (popoverPrefab == null)
            {
                Debug.LogError("[DreamyDebugger] Unable to load bug report popover prefab");
                return;
            }

            if (sheetPrefab == null)
            {
                Debug.LogError("[DreamyDebugger] Unable to load bug report sheet prefab");
                return;
            }

            _popover = ObjectFactory.Instantiate(popoverPrefab);
            _popover.CachedTransform.SetParent(CachedTransform, false);

            _sheet = ObjectFactory.Instantiate(sheetPrefab);
            _sheet.CachedTransform.SetParent(_popover.Container, false);

            _sheet.SubmitComplete = SubmitComplete;
            _sheet.CancelPressed = CancelPressed;

            _sheet.TakingScreenshot = TakingScreenshot;
            _sheet.ScreenshotComplete = ScreenshotComplete;

            _popover.CachedGameObject.SetActive(false);
        }
    }
}
