using System;
using UnityEngine;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

namespace Dreamy.Debugger.SceneInspection
{
    public sealed class HierarchyRowView : MonoBehaviour
    {
        public Button Select, Expand;
        public Text Caption, Context, Arrow;
        public Image Background;
        private SceneInspectionSession session;
        private SceneInspectionSession.Node node;
        private void Awake()
        {
            Select.onClick.AddListener(() => { if (node != null && node.Target != null) { session.Select(node.Target); Selected?.Invoke(); } });
            Expand.onClick.AddListener(() => { if (node != null) session.Toggle(node); });
        }
        public Action Selected;
        private bool wasExpanded, wasSelected, wasSearch;
        public void Bind(SceneInspectionSession owner, SceneInspectionSession.Node value, bool search)
        {
            var isExpanded = owner.IsExpanded(value); var isSelected = owner.Selected == value.Target;
            if (session == owner && ReferenceEquals(node, value) && wasExpanded == isExpanded && wasSelected == isSelected && wasSearch == search) return;
            session = owner; node = value;
            wasExpanded = isExpanded; wasSelected = isSelected; wasSearch = search;
            Caption.text = value.Name;
            Caption.color = value.Active ? Theme.Ivory : Theme.Muted;
            Context.text = search ? value.SceneName + " / " + value.Path : value.Parent < 0 ? value.SceneName : "";
            Arrow.text = value.HasChildren ? isExpanded ? "v" : ">" : "";
            Expand.interactable = value.HasChildren;
            Background.color = isSelected ? Theme.Selected : Theme.Surface;
            var indent = Mathf.Min(value.Depth, 12) * 14;
            var rect = (RectTransform)Expand.transform; rect.anchoredPosition = new Vector2(8 + indent, 0);
            Caption.rectTransform.offsetMin = new Vector2(44 + indent, 20);
            Context.rectTransform.offsetMin = new Vector2(44 + indent, 2);
        }
        public void Clear() { session = null; node = null; Selected = null; }
    }
}
