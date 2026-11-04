using Dreamy.Debugger.Foundation.Service;

namespace Dreamy.Debugger.Presentation.UI.Tabs
{
    using Services;
    using Other;
    using Dreamy.Debugger.Foundation;
    using UnityEngine;

    public class BugReportTabController : ComponentBehaviour, IEnableTab
    {
        [RequiredField] public BugReportSheetController BugReportSheetPrefab;

        [RequiredField] public RectTransform Container; 

        public bool IsEnabled
        {
            get { return ServiceManager.GetService<IBugReportService>().IsUsable; }
        }
        
        protected override void Start()
        {
            base.Start();

            var sheet = ObjectFactory.Instantiate(BugReportSheetPrefab);
            sheet.IsCancelButtonEnabled = false;

            // Callbacks when taking screenshot will hide the debug panel so it is not present in the image
            sheet.TakingScreenshot = TakingScreenshot;
            sheet.ScreenshotComplete = ScreenshotComplete;

            sheet.CachedTransform.SetParent(Container, false);
        }

        private void TakingScreenshot()
        {
            DreamyDebug.Instance.HideDebugPanel();
        }

        private void ScreenshotComplete()
        {
            DreamyDebug.Instance.ShowDebugPanel(false);
        }
    }
}
