using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Debugger.SceneInspection
{
    public sealed class InspectionPicker : MonoBehaviour
    {
        internal sealed class Option
        {
            public string Label;
            public object Value;
        }

        public Text Title, Error, PageLabel;
        public ScrollRect Scroll;
        public InputField Search;
        public Button Apply, Cancel, Scrim, Previous, Next;
        public Button[] Rows;
        public RectTransform Surface, ColorFields;
        public InputField[] Channels;
        public Slider[] ChannelSliders;
        public Image Swatch;
        private IReadOnlyList<Option> options;
        private readonly List<Option> filtered = new List<Option>();
        private Func<object, string> commit;
        private Func<bool> allowed;
        private object draft;
        private Type flagsType;
        private int page;
        private bool color;

        private void Awake()
        {
            Search.onValueChanged.AddListener(_ => Filter());
            Apply.onClick.AddListener(Submit);
            Cancel.onClick.AddListener(() => Dismiss());
            Scrim.onClick.AddListener(() => Dismiss());
            Previous.onClick.AddListener(() => { page--; Draw(); });
            Next.onClick.AddListener(() => { page++; Draw(); });
            for (var i = 0; i < Channels.Length; i++)
            {
                var index = i;
                Channels[i].onValueChanged.AddListener(_ => RefreshColor());
                ChannelSliders[i].onValueChanged.AddListener(value =>
                {
                    Channels[index].SetTextWithoutNotify(InspectionValues.Format(value));
                    RefreshColor();
                });
            }
            for (var i = 0; i < Rows.Length; i++)
            {
                var index = i;
                Rows[i].onClick.AddListener(() => Select(filtered[page * Rows.Length + index].Value));
            }
        }

        internal void Open(string title, object current, IReadOnlyList<Option> choices, Type flags,
            Func<object, string> onCommit, Func<bool> canWrite, bool colorEditor = false)
        {
            Title.text = title;
            Error.text = "";
            draft = current;
            flagsType = flags;
            options = choices;
            color = colorEditor;
            commit = onCommit;
            allowed = canWrite;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ColorFields.gameObject.SetActive(color);
            Search.gameObject.SetActive(!color);
            Search.SetTextWithoutNotify("");
            if (color && current is Color value)
            {
                for (var i = 0; i < Channels.Length; i++) Channels[i].SetTextWithoutNotify(InspectionValues.Format(value[i]));
                RefreshColor();
            }
            FitSurface();
            Error.rectTransform.anchoredPosition = new Vector2(0, color ? -222 : -340);
            Scroll.content.sizeDelta = new Vector2(0, color ? 262 : 380);
            Scroll.verticalNormalizedPosition = 1;
            Filter();
        }

        private void Filter()
        {
            filtered.Clear();
            if (options != null)
                foreach (var option in options)
                    if (option.Label.IndexOf(Search.text, StringComparison.OrdinalIgnoreCase) >= 0) filtered.Add(option);
            page = 0;
            Draw();
        }

        private void Draw()
        {
            var pages = Math.Max(1, (filtered.Count + Rows.Length - 1) / Rows.Length);
            page = Mathf.Clamp(page, 0, pages - 1);
            Previous.gameObject.SetActive(!color && pages > 1);
            Next.gameObject.SetActive(!color && pages > 1);
            Previous.interactable = page > 0;
            Next.interactable = page + 1 < pages;
            PageLabel.text = color ? "" : filtered.Count == 0 ? "No matches" : $"{page + 1} / {pages}";
            for (var i = 0; i < Rows.Length; i++)
            {
                var index = page * Rows.Length + i;
                Rows[i].gameObject.SetActive(index < filtered.Count);
                if (index >= filtered.Count) continue;
                var option = filtered[index];
                var selected = flagsType == null ? Equals(draft, option.Value) : FlagSelected(option.Value);
                Rows[i].GetComponentInChildren<Text>().text = option.Label;
                Rows[i].targetGraphic.color = selected ? Foundation.UI.DebuggerPalette.Selected : Foundation.UI.DebuggerPalette.Control;
            }
        }

        private static ulong Bits(object value)
        {
            try { return Convert.ToUInt64(value, CultureInfo.InvariantCulture); }
            catch (OverflowException) { return unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)); }
        }

        private bool FlagSelected(object value)
        {
            var bits = Bits(value);
            return bits == 0 ? Bits(draft) == 0 : (Bits(draft) & bits) == bits;
        }

        private void Select(object value)
        {
            if (flagsType == null) draft = value;
            else
            {
                var bits = Bits(value);
                draft = Enum.ToObject(flagsType, bits == 0 ? 0UL : FlagSelected(value) ? Bits(draft) & ~bits : Bits(draft) | bits);
            }
            Draw();
        }

        private void RefreshColor()
        {
            if (!TryColor(out var value)) return;
            Swatch.color = value;
            for (var i = 0; i < Channels.Length; i++) ChannelSliders[i].SetValueWithoutNotify(value[i]);
        }

        private bool TryColor(out Color value)
        {
            value = default;
            for (var i = 0; i < Channels.Length; i++)
            {
                if (!InspectionValues.TryParse(Channels[i].text, typeof(float), out var parsed, out _)) return false;
                value[i] = (float)parsed;
            }
            return true;
        }

        private void Submit()
        {
            if (color)
            {
                if (!TryColor(out var value)) { Error.text = "Enter valid R, G, B and A values."; Scroll.verticalNormalizedPosition = 0; return; }
                draft = value;
            }
            var failure = commit?.Invoke(draft);
            if (failure != null)
            {
                Error.text = failure;
                Canvas.ForceUpdateCanvases();
                Scroll.verticalNormalizedPosition = 0;
            }
            else Dismiss();
        }

        public bool Dismiss()
        {
            if (!gameObject.activeSelf) return false;
            commit = null;
            allowed = null;
            options = null;
            draft = null;
            filtered.Clear();
            gameObject.SetActive(false);
            return true;
        }

        private void FitSurface()
        {
            var bounds = ((RectTransform)transform).rect;
            var size = new Vector2(Mathf.Min(600, Mathf.Max(0, bounds.width - 24)),
                Mathf.Min(color ? 350 : 482, Mathf.Max(0, bounds.height - 16)));
            if (Surface.sizeDelta != size) Surface.sizeDelta = size;
        }

        private void Update()
        {
            if (allowed == null || !allowed()) Dismiss();
            else FitSurface();
        }
    }
}
