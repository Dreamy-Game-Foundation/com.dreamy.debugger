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
        public InputField CredentialInput;
        public Button CredentialSubmit;
        public void SetPassphraseMode(bool enabled){
            passphraseMode=enabled;if(LengthStatus!=null)LengthStatus.gameObject.SetActive(!enabled);
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

            CancelButton.onClick.AddListener(CancelButtonPressed);
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
        }
        protected override void Update() {
            base.Update();
            if(!_isVisible)return;
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
        }

        public void Hide()
        {
            CanvasGroup.alpha = 0f;
            CanvasGroup.blocksRaycasts = CanvasGroup.interactable = false;
            _isVisible = false;
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

        private void CancelButtonPressed()
        {
            if (_numbers.Count > 0)
            {
                _numbers.PopLast();
            }
            else
            {
                OnCancel();
            }

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

            if (_numbers.Count > 0)
            {
                CancelButtonText.text = "Delete";
            }
            else
            {
                CancelButtonText.text = CanCancel ? "Cancel" : "";
            }
        }
    }
}
