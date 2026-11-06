namespace Dreamy.Debugger.Presentation.UI
{
    using Other;
    using Dreamy.Debugger.Foundation;
    using UnityEngine;
    using UnityEngine.UI;

    // Reversible navigation drawer, closed on every panel open.
    public class MobileMenuController : ComponentBehaviour
    {
        [SerializeField] private float _maxMenuWidth = 144f;
        [SerializeField] private float _peekAmount = 45f;
        [RequiredField] public RectTransform Content;
        [RequiredField] public RectTransform Menu;
        [RequiredField] public Button OpenButton;
        [RequiredField] public DebugTabController TabController;
        public GameObject HeaderLogo;
        public GameObject SidebarLogo;
        public Button Scrim;
        public Button DrawerClose;
        private static MobileMenuController active;
        public bool IsNarrow => Content != null && ((RectTransform)Content.parent).rect.width < 600;
        public static bool DismissDrawer() { if (active == null || !active.isActiveAndEnabled || !active.IsNarrow || !active.IsOpen) return false; active.Close(); return true; }
        private float target;
        private float currentOffset;
        private float previousWidth, previousHeight;
        private Vector2 originalSize, originalPivot, originalPosition;
        public float PeekAmount => _peekAmount;
        public float MaxMenuWidth => _maxMenuWidth;
        public bool IsOpen { get; private set; }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Menu == null || Content == null) return;
            active = this;
            if (Scrim != null) Scrim.onClick.AddListener(Close);
            if (DrawerClose != null) DrawerClose.onClick.AddListener(Close);
            originalSize = Content.sizeDelta; originalPivot = Content.pivot; originalPosition = Content.anchoredPosition;
            Content.pivot = new Vector2(0, originalPivot.y);
            Menu.GetComponent<LayoutElement>().ignoreLayout = true;
            Menu.anchorMin = new Vector2(0, 0);
            Menu.anchorMax = new Vector2(0, 1);
            Menu.pivot = new Vector2(1, .5f);
            Menu.offsetMin = Menu.offsetMax = Vector2.zero;
            Resize();
            Menu.anchoredPosition = Vector2.zero;
            OpenButton.gameObject.SetActive(true);
            TabController.ActiveTabChanged += OnTabChanged;
            SetOpen(false);
            ApplyOffset(target);
        }

        protected override void OnDisable()
        {
            if (active == this) active = null;
            if (Scrim != null) { Scrim.onClick.RemoveListener(Close); Scrim.gameObject.SetActive(false); }
            if (DrawerClose != null) DrawerClose.onClick.RemoveListener(Close);
            if (TabController != null) TabController.ActiveTabChanged -= OnTabChanged;
            if (Menu != null) Menu.GetComponent<LayoutElement>().ignoreLayout = false;
            if (Content != null) { Content.pivot = originalPivot; Content.sizeDelta = originalSize; Content.anchoredPosition = originalPosition; }
            base.OnDisable();
        }

        private void Resize()
        {
            previousWidth = ((RectTransform)Content.parent).rect.width;
            previousHeight = ((RectTransform)Content.parent).rect.height;
            var tabs = Menu.GetComponentsInChildren<Controls.DebugTabButton>(true);
            var rowHeight = Mathf.Clamp((previousHeight - 64) / Mathf.Max(1, tabs.Length), 32, 56);
            foreach (var tab in tabs)
            {
                var row = (RectTransform)tab.transform;
                var rowGroup = tab.gameObject.GetComponent<LayoutGroup>();
                if (rowGroup != null) rowGroup.enabled = false;
                var adaptive = tab.gameObject.GetComponent<Dreamy.Debugger.Foundation.UI.ResponsiveEnable>();
                if (adaptive != null) adaptive.enabled = false;
                var titleParent = tab.TitleText.transform.parent as RectTransform;
                if (titleParent != null && titleParent != row)
                {
                    var titleGroup = titleParent.GetComponent<LayoutGroup>();
                    if (titleGroup != null) titleGroup.enabled = false;
                    titleParent.anchorMin = Vector2.zero; titleParent.anchorMax = Vector2.one;
                    titleParent.offsetMin = titleParent.offsetMax = Vector2.zero;
                }
                var layout = tab.gameObject.GetComponent<LayoutElement>();
                if (layout == null) layout = tab.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = layout.preferredHeight = rowHeight;
                row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);
                var icon = (RectTransform)tab.IconStyleComponent.transform;
                icon.anchorMin = icon.anchorMax = new Vector2(0, .5f);
                icon.pivot = new Vector2(.5f, .5f); icon.sizeDelta = new Vector2(22, 22); icon.anchoredPosition = new Vector2(24, 0);
                var title = tab.TitleText.rectTransform;
                title.anchorMin = Vector2.zero; title.anchorMax = Vector2.one;
                title.offsetMin = new Vector2(40, 4); title.offsetMax = new Vector2(tab.ExtraContentContainer!=null && tab.ExtraContentContainer.childCount>0?-48:-12, -4);
                tab.TitleText.alignment = TextAnchor.MiddleLeft; tab.TitleText.fontSize = 14;
                tab.TitleText.resizeTextForBestFit = true; tab.TitleText.resizeTextMinSize = 11; tab.TitleText.resizeTextMaxSize = 14;
                var extra = tab.ExtraContentContainer;
                if (extra != null)
                {
                    extra.anchorMin = new Vector2(1, 0); extra.anchorMax = Vector2.one; extra.pivot = new Vector2(1, .5f);
                    extra.sizeDelta = new Vector2(40, -8); extra.anchoredPosition = new Vector2(-4, 0);
                }
            }
            Menu.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Clamp(previousWidth - PeekAmount, 0, MaxMenuWidth));
            target = IsOpen ? Menu.rect.width : 0;
        }

        protected override void Update()
        {
            base.Update();
            if (Menu == null || Content == null) return;
            if (!Mathf.Approximately(previousWidth, ((RectTransform)Content.parent).rect.width) ||
                !Mathf.Approximately(previousHeight, ((RectTransform)Content.parent).rect.height)) Resize();
            var from = currentOffset;
            var x = Mathf.Abs(target - from) < .5f ? target :
                DebugMath.SpringLerp(from, target, 15f, Time.unscaledDeltaTime);
            ApplyOffset(x);
        }

        private void ApplyOffset(float x)
        {
            currentOffset = x;
            // Keep the right edge and close/pin buttons on screen while the rail slides in.
            var offset = IsNarrow ? 0 : x;
            Content.sizeDelta = new Vector2(originalSize.x - offset, originalSize.y);
            Content.anchoredPosition = new Vector2(originalPosition.x + offset, originalPosition.y);
            Menu.anchoredPosition = new Vector2(IsNarrow ? x : 0, 0);
            if (Scrim != null) {
                var show = IsNarrow && IsOpen;
                if (Scrim.gameObject.activeSelf != show) Scrim.gameObject.SetActive(show);
                // Unity does not retain overrideSorting on an inactive nested canvas.
                // Restore it after activation so tab canvases cannot cover the scrim.
                if (show) {
                    var scrimCanvas = Scrim.GetComponent<Canvas>();
                    if (scrimCanvas != null && !scrimCanvas.overrideSorting) {
                        scrimCanvas.overrideSorting = true;
                        var shellCanvas = Content.GetComponentInParent<Canvas>();
                        scrimCanvas.sortingOrder = shellCanvas.sortingOrder + 2;
                    }
                }
            }
            if (DrawerClose != null) DrawerClose.gameObject.SetActive(IsNarrow);
        }

        private void OnTabChanged(DebugTabController controller, DebugTab tab) { if (IsNarrow) Close(); }
        private void SetOpen(bool value)
        {
            IsOpen = value; Resize(); target = value ? Menu.rect.width : 0;
            // One visible brand mark: left corner of the rail or of the collapsed header.
            if (HeaderLogo != null) HeaderLogo.SetActive(!value);
            if (SidebarLogo != null) SidebarLogo.SetActive(value);
        }
        public void Toggle() { SetOpen(!IsOpen); }
        [ContextMenu("Open")] public void Open() { SetOpen(true); }
        [ContextMenu("Close")] public void Close() { SetOpen(false); }
    }
}
