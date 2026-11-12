using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Debugger
{
    public sealed class ScreenshotOverlayView : MonoBehaviour
    {
        public GameObject ResultCard, FullPreview;
        public RawImage Thumbnail, FullImage;
        public Text Summary;
        public Button View, Repeat, Close, ClosePreview;
        private ScreenshotMode mode;

        internal void Configure(ScreenshotMode owner)
        {
            mode = owner;
            View.onClick.AddListener(ShowPreview);
            Repeat.onClick.AddListener(() => { HideAll(); mode.CaptureFromUi(); });
            Close.onClick.AddListener(HideAll);
            ClosePreview.onClick.AddListener(() => { FullPreview.SetActive(false); ResultCard.SetActive(true); });
            HideAll();
        }

        public void HideAll() { ResultCard.SetActive(false); FullPreview.SetActive(false); }
        public void ShowResult()
        {
            if (mode == null || !mode.CanUse || mode.LastCapture == null) return;
            FullPreview.SetActive(false);
            ResultCard.SetActive(true);
            SetImage(Thumbnail);
            View.interactable = mode.Preview != null;
            Summary.text = "Screenshot saved • " + mode.LastCapture.Width + " × " + mode.LastCapture.Height;
        }
        public void ShowPreview()
        {
            if (mode == null || !mode.CanUse || mode.Preview == null) return;
            ResultCard.SetActive(false);
            FullPreview.SetActive(true);
            SetImage(FullImage);
        }
        private void SetImage(RawImage image)
        {
            image.texture = mode.Preview;
            var fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter != null && mode.Preview != null) fitter.aspectRatio = (float)mode.Preview.width / mode.Preview.height;
        }
        private void Update() { if (mode == null || !mode.CanUse) HideAll(); else Repeat.interactable = !mode.IsBusy; }
    }
}
