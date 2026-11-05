namespace Dreamy.Debugger.Presentation.UI
{
    using Other;
    using Dreamy.Debugger.Foundation;
    using UnityEngine;
    using UnityEngine.UI;

    // Same sliding content owner as SR, with an explicit reversible toggle at all widths.
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
        private float target;
        private float previousWidth;
        private Vector2 originalSize, originalPivot, originalPosition;
        public float PeekAmount => _peekAmount;
        public float MaxMenuWidth => _maxMenuWidth;
        public bool IsOpen { get; private set; }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Menu == null || Content == null) return;
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
            SetOpen(((RectTransform)Content.parent).rect.width >= 600);
            ApplyOffset(target);
        }

        protected override void OnDisable()
        {
            if (TabController != null) TabController.ActiveTabChanged -= OnTabChanged;
            if (Menu != null) Menu.GetComponent<LayoutElement>().ignoreLayout = false;
            if (Content != null) { Content.pivot = originalPivot; Content.sizeDelta = originalSize; Content.anchoredPosition = originalPosition; }
            base.OnDisable();
        }

        private void Resize()
        {
            previousWidth = ((RectTransform)Content.parent).rect.width;
            Menu.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Clamp(previousWidth - PeekAmount, 0, MaxMenuWidth));
            target = IsOpen ? Menu.rect.width : 0;
        }

        protected override void Update()
        {
            base.Update();
            if (Menu == null || Content == null) return;
            if (!Mathf.Approximately(previousWidth, ((RectTransform)Content.parent).rect.width)) Resize();
            var from = Content.anchoredPosition.x;
            var x = Mathf.Abs(target - from) < .5f ? target :
                DebugMath.SpringLerp(from, target, 15f, Time.unscaledDeltaTime);
            ApplyOffset(x);
        }

        private void ApplyOffset(float x)
        {
            // Keep the right edge and close/pin buttons on screen while the rail slides in.
            Content.sizeDelta = new Vector2(originalSize.x - x, originalSize.y);
            Content.anchoredPosition = new Vector2(originalPosition.x + x, originalPosition.y);
        }

        private void OnTabChanged(DebugTabController controller, DebugTab tab) { Close(); }
        private void SetOpen(bool value)
        {
            IsOpen = value; target = value ? Menu.rect.width : 0;
            // One visible brand mark: left corner of the rail or of the collapsed header.
            if (HeaderLogo != null) HeaderLogo.SetActive(!value);
            if (SidebarLogo != null) SidebarLogo.SetActive(value);
        }
        public void Toggle() { SetOpen(!IsOpen); }
        [ContextMenu("Open")] public void Open() { SetOpen(true); }
        [ContextMenu("Close")] public void Close() { SetOpen(false); }
    }
}
