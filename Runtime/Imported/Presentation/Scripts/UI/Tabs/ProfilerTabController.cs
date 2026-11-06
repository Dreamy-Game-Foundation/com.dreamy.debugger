namespace Dreamy.Debugger.Presentation.UI.Tabs
{
    using Dreamy.Debugger.Foundation;
    using UnityEngine;
    using UnityEngine.UI;
    using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

    public class ProfilerTabController : ComponentBehaviour
    {
        // Retained for existing prefab references; the old banner pin control is hidden.
        [RequiredField] public Toggle PinToggle;
        private static readonly string[] HudModes = { "Off", "FpsOnly", "Compact", "Detailed" };
        private readonly Image[] modeImages = new Image[4];
        private string lastHudMode;

        protected override void Start()
        {
            base.Start();
            var content = transform.Find("Viewport/Content");
            if (content == null) return;
            var font = PinToggle.GetComponentInChildren<Text>(true)?.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var row = new GameObject("Dreamy HUD controls", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(content, false);
            row.transform.SetAsFirstSibling();
            row.GetComponent<Image>().color = Theme.Surface;
            var size = row.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 48;
            size.flexibleWidth = 1;
            var group = row.GetComponent<HorizontalLayoutGroup>();
            group.spacing = 6;
            group.padding = new RectOffset(10, 10, 6, 6);
            group.childForceExpandHeight = group.childForceExpandWidth = true;
            var labels = new[] { "HUD Off", "FPS", "Compact", "Detail" };
            for (var i = 0; i < HudModes.Length; i++)
            {
                var mode = HudModes[i];
                var button = new GameObject("HUD " + mode, typeof(RectTransform), typeof(Image), typeof(Button));
                button.transform.SetParent(row.transform, false);
                modeImages[i] = button.GetComponent<Image>();
                button.GetComponent<Button>().onClick.AddListener(() => { PreferenceBridge.Set("hud.mode", mode); RefreshHudControls(); });
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(button.transform, false);
                var rect = (RectTransform)label.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var text = label.GetComponent<Text>();
                text.text = labels[i]; text.font = font; text.fontSize = 12;
                text.alignment = TextAnchor.MiddleCenter; text.color = Theme.Ivory; text.raycastTarget = false;
            }
            PresentationAssets.ApplySkin(row);
            PinToggle.gameObject.SetActive(false);
            RefreshHudControls();
        }

        private void RefreshHudControls()
        {
            lastHudMode = PreferenceBridge.Get("hud.mode", "Off");
            for (var i = 0; i < modeImages.Length; i++)
                if (modeImages[i] != null) modeImages[i].color = lastHudMode == HudModes[i] ? Theme.Accent : Theme.Control;
        }

        protected override void Update()
        {
            base.Update();
            if (lastHudMode != PreferenceBridge.Get("hud.mode", "Off")) RefreshHudControls();
        }
    }
}
