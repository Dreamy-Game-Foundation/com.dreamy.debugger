namespace Dreamy.Debugger.Presentation.UI.Other
{
    using Dreamy.Debugger.Foundation;
    using UnityEngine;
    using UnityEngine.UI;
    using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

    public class CategoryGroup : ComponentBehaviour
    {
        public Button CollapseButton;
        public Text CollapseIndicator;
        [RequiredField] public RectTransform Container;
        [RequiredField] public Text Header;
        [RequiredField] public GameObject Background;
        [RequiredField] public Toggle SelectionToggle;

        public GameObject[] EnabledDuringSelectionMode = new GameObject[0];

        public Button PinButton { get; private set; }
        private Graphic pinIndicator;
        private Text pinCaption;

        public void ConfigurePinButton(Sprite sprite, UnityEngine.Events.UnityAction clicked)
        {
            if (PinButton != null) return;
            var header = Header.transform.parent;
            var go = new GameObject("Pin category", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(header, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
            rect.pivot = new Vector2(1, .5f); rect.anchoredPosition = new Vector2(-40, 0); rect.sizeDelta = new Vector2(40, 40);
            var background = go.GetComponent<Image>(); background.color = Theme.Control;
            PinButton = go.GetComponent<Button>(); PinButton.targetGraphic = background; PinButton.onClick.AddListener(clicked);
            var offset = Header.rectTransform.offsetMax; offset.x = -88; Header.rectTransform.offsetMax = offset;
            var icon = new GameObject("Pin state", typeof(RectTransform)); icon.transform.SetParent(go.transform, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = Vector2.zero; iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(9, 9); iconRect.offsetMax = new Vector2(-9, -9);
            if (sprite != null)
            {
                var image = icon.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; pinIndicator = image;
            }
            else
            {
                pinCaption = icon.AddComponent<Text>(); pinCaption.font = Header.font; pinCaption.fontSize = 10;
                pinCaption.alignment = TextAnchor.MiddleCenter; pinIndicator = pinCaption;
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            }
            pinIndicator.raycastTarget = false;
        }

        public void SetPinState(int count, int total)
        {
            if (PinButton == null) return;
            PinButton.interactable = total > 0;
            pinIndicator.color = count == 0 ? Theme.Muted : count == total ? Theme.Accent : Theme.Warning;
            if (pinCaption != null) pinCaption.text = count == total && total > 0 ? "Unpin" : "Pin";
            // The legacy selection wash stays hidden; only the small icon changes.
            if (SelectionToggle.graphic != null) SelectionToggle.graphic.CrossFadeAlpha(0, 0, true);
        }

        private bool _selectionModeEnabled = true;

        public bool IsSelected
        {
            get
            {
                return SelectionToggle.isOn;
            }
            set
            {
                SelectionToggle.isOn = value;

                if (SelectionToggle.graphic != null)
                {
                    SelectionToggle.graphic.CrossFadeAlpha(value ? _selectionModeEnabled ? 1.0f : 0.2f : 0f, 0, true);
                }
            }
        }

        public bool SelectionModeEnabled
        {
            get { return _selectionModeEnabled; }

            set
            {
                if (value == _selectionModeEnabled)
                {
                    return;
                }

                _selectionModeEnabled = value;

                for (var i = 0; i < EnabledDuringSelectionMode.Length; i++)
                {
                    EnabledDuringSelectionMode[i].SetActive(_selectionModeEnabled);
                }
            }
        }

    }
}
