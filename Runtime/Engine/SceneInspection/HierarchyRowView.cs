using System;
using UnityEngine;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

namespace Dreamy.Debugger.SceneInspection
{
    public sealed class HierarchyRowView : MonoBehaviour
    {
        public Button Select, Expand;
        public Text Caption, Context;
        public Image Arrow;
        public Image Background;
        public Image Icon;
        public Sprite ObjectIcon, CameraIcon;
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
            Expand.gameObject.SetActive(value.HasChildren);
            if (Arrow != null) Arrow.rectTransform.localEulerAngles = new Vector3(0, 0, isExpanded ? -90 : 0);
            Expand.interactable = value.HasChildren;
            Background.color = isSelected ? Theme.Selected : Theme.Surface;
            var indent = Mathf.Min(value.Depth, 12) * 14;
            var rect = (RectTransform)Expand.transform; rect.anchoredPosition = new Vector2(8 + indent, 0);
            Caption.supportRichText = false; Context.supportRichText = false;
            Context.gameObject.SetActive(search);
            if (Icon != null)
            {
                Icon.sprite = value.Target != null && value.Target.GetComponent<Camera>() != null ? CameraIcon : ObjectIcon;
                Icon.rectTransform.anchoredPosition = new Vector2(42 + indent, 0); Icon.color = value.Active ? Theme.Ivory : Theme.Muted;
            }
            Caption.rectTransform.offsetMin = new Vector2((Icon != null ? 58 : 44) + indent, search ? 18 : 0);
            Context.rectTransform.offsetMin = new Vector2((Icon != null ? 58 : 44) + indent, 2);
        }
        public void Clear() { session = null; node = null; Selected = null; }
    }
}
