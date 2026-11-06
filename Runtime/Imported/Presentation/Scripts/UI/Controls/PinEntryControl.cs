namespace Dreamy.Debugger.Presentation.UI.Controls
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Dreamy.Debugger.Foundation;
    using UnityEngine;
    using UnityEngine.UI;

    public delegate void PinEntryControlCallback(IList<int> result, bool didCancel);

    public class PinEntryControl : ComponentBehaviour
    {
        private bool _isVisible = true;
        private List<int> _numbers = new List<int>(4);

        [RequiredField] public Image Background;

        public bool CanCancel = true;

        [RequiredField] public UnityEngine.UI.Button CancelButton;

        [RequiredField] public Text CancelButtonText;

        [RequiredField] public CanvasGroup CanvasGroup;

        [RequiredField] public Animator DotAnimator;

        public UnityEngine.UI.Button[] NumberButtons;
        public Toggle[] NumberDots;

        [RequiredField] public Text PromptText;

        public event PinEntryControlCallback Complete;
        public event System.Action<string> CredentialSubmitted;
        public Text LengthStatus;
        private bool passphraseMode;
        private RectTransform responsiveLayout;
        private Vector2 previousViewport;
        private Rect previousSafeArea;
        private bool layoutDirty = true;

        public InputField CredentialInput;
        public Button CredentialSubmit;
        public Button BackspaceButton;
        public void SetPassphraseMode(bool enabled){
            layoutDirty = true;
            passphraseMode=enabled;
            if(BackspaceButton!=null)BackspaceButton.gameObject.SetActive(!enabled);if(LengthStatus!=null)LengthStatus.gameObject.SetActive(!enabled);
            foreach(var button in NumberButtons)button.gameObject.SetActive(!enabled);
            foreach(var dot in NumberDots)dot.gameObject.SetActive(!enabled);
            if(CredentialInput!=null){CredentialInput.gameObject.SetActive(enabled);CredentialInput.text="";}
            if(CredentialSubmit!=null)CredentialSubmit.gameObject.SetActive(enabled);
        }

        protected override void Awake()
        {
            base.Awake();

            for (var i = 0; i < NumberButtons.Length; i++)
            {
                var number = i;

                NumberButtons[i].onClick.AddListener(() => { PushNumber(number); });
            }

            CancelButton.onClick.AddListener(() => { if (CanCancel) OnCancel(); });
            if (BackspaceButton != null) BackspaceButton.onClick.AddListener(DeleteLastDigit);
            if(CredentialSubmit!=null)CredentialSubmit.onClick.AddListener(()=>CredentialSubmitted?.Invoke(CredentialInput.text));

            RefreshState();


        }

        public int RequiredLength { get; private set; } = 4;
        public void ConfigureLength(int length) {
            RequiredLength = Mathf.Clamp(length,4,32);
            if(NumberDots.Length < Mathf.Min(RequiredLength,8)) {
                var dots = new System.Collections.Generic.List<Toggle>(NumberDots);
                while(dots.Count < Mathf.Min(RequiredLength,8)) dots.Add(Instantiate(NumberDots[0],NumberDots[0].transform.parent));
                NumberDots=dots.ToArray();
            }
            for(var i=0;i<NumberDots.Length;i++)NumberDots[i].gameObject.SetActive(i<Mathf.Min(RequiredLength,8));
            Clear();
            layoutDirty = true;
        }
        protected override void Update() {
            base.Update();
            if(!_isVisible)return;
            RefreshResponsiveLayout();
            if(InputBridge.Down(KeyCode.Escape)){if(CanCancel)OnCancel();return;}
            if(passphraseMode)return;
            if(_numbers.Count>0 && (InputBridge.Down(KeyCode.Backspace) || InputBridge.Down(KeyCode.Delete))) {
                _numbers.PopLast(); RefreshState();
            }
            foreach(var character in InputBridge.ReadText())HandleCharacter(character);
        }

        private void HandleCharacter(char i)
        {
            if (!_isVisible)
            {
                return;
            }

            if (!char.IsNumber(i))
            {
                return;
            }

            var num = (int) char.GetNumericValue(i);

            if (num > 9 || num < 0)
            {
                return;
            }

            PushNumber(num);
        }

        public void Show()
        {
            CanvasGroup.alpha = 1f;
            CanvasGroup.blocksRaycasts = CanvasGroup.interactable = true;
            _isVisible = true;
            layoutDirty = true;
            RefreshResponsiveLayout();
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void RefreshResponsiveLayout()
        {
            var viewport = (RectTransform)transform;
            var safe = Screen.safeArea;
            if (!layoutDirty && previousViewport == viewport.rect.size && previousSafeArea == safe) return;
            previousViewport = viewport.rect.size;
            previousSafeArea = safe;
            layoutDirty = false;
            if (responsiveLayout == null)
            {
                responsiveLayout = transform.Find("Layout") as RectTransform;
                if (responsiveLayout == null) return;
                foreach (var group in responsiveLayout.GetComponentsInChildren<LayoutGroup>(true)) group.enabled = false;
                foreach (var fitter in responsiveLayout.GetComponentsInChildren<ContentSizeFitter>(true)) fitter.enabled = false;
            }
            if (viewport.rect.width <= 0 || viewport.rect.height <= 0 || Screen.width <= 0 || Screen.height <= 0) return;
            var safeMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            var safeMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            var available = Vector2.Scale(viewport.rect.size, safeMax - safeMin);
            var landscape = available.x > available.y * 1.2f;
            var size = passphraseMode ? new Vector2(360, 176) : new Vector2(280, 332);
            responsiveLayout.anchorMin = responsiveLayout.anchorMax = (safeMin + safeMax) * .5f;
            responsiveLayout.pivot = new Vector2(.5f, .5f);
            responsiveLayout.anchoredPosition = Vector2.zero;
            responsiveLayout.sizeDelta = size;
            responsiveLayout.localScale = Vector3.one * Mathf.Max(.1f,
                Mathf.Min(1, Mathf.Min(available.x - 24, available.x * .84f) / size.x,
                    Mathf.Min(available.y - 24, available.y * .84f) / size.y));
            var backing = responsiveLayout.Find("Panel backing") as RectTransform;
            if (backing != null)
            {
                backing.anchorMin = Vector2.zero; backing.anchorMax = Vector2.one;
                backing.offsetMin = backing.offsetMax = Vector2.zero;
            }
            var contentWidth = size.x - 32;
            PromptText.gameObject.SetActive(false);
            PromptText.text = "";
            PromptText.fontSize = 18;
            PromptText.alignment = TextAnchor.MiddleCenter;
            PromptText.resizeTextForBestFit = true;
            PromptText.resizeTextMinSize = 12; PromptText.resizeTextMaxSize = 18;
            Place(PromptText.rectTransform, 16, 16, contentWidth, landscape && !passphraseMode ? 72 : 36);
            if (LengthStatus != null) Place(LengthStatus.rectTransform, 16, 38, contentWidth, 20);
            var dots = NumberDots.Length > 0 ? NumberDots[0].transform.parent as RectTransform : null;
            if (dots != null)
            {
                var result = responsiveLayout.Find("Result") as RectTransform;
                if (result != null) Place(result, 16, 16, contentWidth, 24);
                dots.anchorMin = Vector2.zero; dots.anchorMax = Vector2.one;
                dots.offsetMin = dots.offsetMax = Vector2.zero;
                var count = Mathf.Min(RequiredLength, 8);
                for (var i = 0; i < NumberDots.Length; i++)
                    Place((RectTransform)NumberDots[i].transform, (contentWidth - count * 20) * .5f + i * 20 + 2, 4, 16, 16);
            }
            var grid = NumberButtons.Length > 0 ? NumberButtons[0].transform.parent as RectTransform : null;
            if (grid == null) return;
            Place(grid, 16, passphraseMode ? 28 : RequiredLength>8 ? 64 : 52, size.x - 32, passphraseMode ? 132 : 248);
            for (var i = 0; i < NumberButtons.Length; i++)
            {
                var slot = i == 0 ? 10 : i - 1;
                Place((RectTransform)NumberButtons[i].transform, slot % 3 * 84, slot / 3 * 62, 76, 54);
            }
            var cancelSlot = CancelButton.transform.parent as RectTransform;
            if (cancelSlot != null) Place(cancelSlot, 0, passphraseMode ? 72 : 186, passphraseMode ? 148 : 76, 54);
            Place(CancelButton.GetComponent<RectTransform>(),0,0,passphraseMode?148:76,54);
            if(BackspaceButton!=null)Place(BackspaceButton.GetComponent<RectTransform>(),168,186,76,54);
            if (CredentialInput != null) Place(CredentialInput.GetComponent<RectTransform>(), 0, 0, size.x - 32, 48);
            if (CredentialSubmit != null) Place(CredentialSubmit.GetComponent<RectTransform>(), 164, 72, 164, 44);
        }

        public void Hide()
        {
            CanvasGroup.alpha = 0f;
            CanvasGroup.blocksRaycasts = CanvasGroup.interactable = false;
            _isVisible = false;
            Clear();
            if(CredentialInput!=null)CredentialInput.text="";
        }

        public void Clear()
        {
            _numbers.Clear();
            RefreshState();
        }

        public void PlayInvalidCodeAnimation()
        {
            DotAnimator.SetTrigger("Invalid");
        }

        protected void OnComplete()
        {
            if (Complete != null)
            {
                Complete(new ReadOnlyCollection<int>(_numbers), false);
            }
        }

        protected void OnCancel()
        {
            if (Complete != null)
            {
                Complete(new int[] {}, true);
            }
        }

        private void DeleteLastDigit()
        {
            if (!_isVisible || passphraseMode || _numbers.Count == 0) return;
            _numbers.RemoveAt(_numbers.Count - 1);
            RefreshState();
        }

        public void PushNumber(int number)
        {
            if(number < 0 || number > 9 || !_isVisible)return;
            if (_numbers.Count >= RequiredLength)
            {
                Debug.LogWarning("[PinEntry] PIN length exceeded");
                return;
            }

            _numbers.Add(number);

            if (_numbers.Count >= RequiredLength)
            {
                OnComplete();
            }

            RefreshState();
        }

        private void RefreshState()
        {
            if(LengthStatus!=null)LengthStatus.text=RequiredLength>8?_numbers.Count+" / "+RequiredLength+" digits":"";
            for (var i = 0; i < NumberDots.Length; i++)
            {
                NumberDots[i].isOn = i < _numbers.Count;
            }

            CancelButtonText.text = "";
            CancelButton.gameObject.SetActive(CanCancel);
            if (BackspaceButton != null) BackspaceButton.interactable = _numbers.Count > 0;

        }
    }
}
