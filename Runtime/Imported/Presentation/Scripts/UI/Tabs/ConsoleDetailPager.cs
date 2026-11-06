using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

namespace Dreamy.Debugger.Presentation.UI.Tabs
{
    /// <summary>Keep full details available without exceeding a single uGUI Text mesh budget.</summary>
    public sealed class ConsoleDetailPager
    {
        public const int PageCharacters = 12000;
        private readonly Text output, status;
        private readonly ScrollRect scroll;
        private readonly GameObject toolbar;
        private readonly Button previous, next;
        private readonly List<int> offsets = new List<int>();
        private string content = "";
        public int PageIndex { get; private set; }
        public int PageCount => offsets.Count > 0 ? offsets.Count - 1 : 0;

        public ConsoleDetailPager(Text output, ScrollRect scroll)
        {
            this.output = output; this.scroll = scroll;
            output.supportRichText = false;
            output.horizontalOverflow = HorizontalWrapMode.Wrap;
            output.verticalOverflow = VerticalWrapMode.Overflow;
            toolbar = new GameObject("Dreamy detail pages", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            toolbar.transform.SetParent(scroll.transform.parent, false);
            toolbar.transform.SetSiblingIndex(scroll.transform.GetSiblingIndex());
            var size = toolbar.GetComponent<LayoutElement>(); size.minHeight = size.preferredHeight = 32;
            var layout = toolbar.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 8; layout.childForceExpandWidth = true;
            previous = CreateButton("Previous", () => ShowPage(PageIndex - 1));
            status = CreateText("Page", "");
            next = CreateButton("Next", () => ShowPage(PageIndex + 1));
            toolbar.SetActive(false);
        }

        private Text CreateText(string name, string value)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(toolbar.transform, false);
            var text = go.GetComponent<Text>();
            text.font = output.font; text.fontSize = 12; text.text = value;
            text.color = Theme.Ivory; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string label, UnityEngine.Events.UnityAction clicked)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(toolbar.transform, false); go.GetComponent<Image>().color = Theme.Control;
            var text = CreateText(label + " label", label); text.transform.SetParent(go.transform, false);
            var rect = text.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var button = go.GetComponent<Button>(); button.onClick.AddListener(clicked); return button;
        }

        public void SetContent(string value)
        {
            content = value ?? ""; offsets.Clear(); offsets.Add(0);
            for (var start = 0; start < content.Length;)
            {
                var end = Mathf.Min(start + PageCharacters, content.Length);
                if (end < content.Length)
                {
                    var newline = content.LastIndexOf('\n', end - 1, end - start);
                    if (newline > start + PageCharacters / 2) end = newline + 1;
                    if (end > start && char.IsHighSurrogate(content[end - 1]) && char.IsLowSurrogate(content[end])) end--;
                }
                offsets.Add(end); start = end;
            }
            toolbar.SetActive(PageCount > 1); ShowPage(0);
        }

        public void ShowPage(int index)
        {
            PageIndex = Mathf.Clamp(index, 0, Mathf.Max(0, PageCount - 1));
            output.text = PageCount == 0 ? "" : content.Substring(offsets[PageIndex], offsets[PageIndex + 1] - offsets[PageIndex]);
            status.text = (PageIndex + 1) + " / " + PageCount;
            previous.interactable = PageIndex > 0; next.interactable = PageIndex + 1 < PageCount;
            scroll.verticalNormalizedPosition = 1;
        }
    }
}
