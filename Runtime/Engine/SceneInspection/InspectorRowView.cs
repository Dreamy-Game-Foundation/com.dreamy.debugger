using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dreamy.Debugger.SceneInspection
{
    public sealed class InspectorRowView : MonoBehaviour, ICancelHandler
    {
        public sealed class Entry
        {
            public string Label, Hint, Draft, Error;
            public InspectionMember Member;
            public Action Action;
            public object Baseline;
            public bool Drafting;
            public Func<bool> Available;
            public Func<string> ReadLabel;
            public float Height => Member == null ? 76 : 108;
        }
        public Text Caption, Hint;
        public InputField Value;
        public Toggle Boolean;
        public Text BooleanLabel;
        public Dropdown Enumeration;
        public Image ColorPreview;
        public Button Action, Apply, Cancel;
        public Text ActionLabel;
        public RectTransform Actions;
        private Entry entry;
        private Func<bool> policy;
        private bool binding;
        private Type enumType;
        private string enumUnknown;
        private void Awake()
        {
            Value.onValueChanged.AddListener(text => { if (!binding && entry != null && Editable) { entry.Drafting = true; entry.Draft = text; } });
            Value.onEndEdit.AddListener(text =>
            {
                // Pointer blur and the mobile keyboard's Done button never write.
                if (!binding && (Dreamy.Debugger.Presentation.InputBridge.Down(KeyCode.Return) || Dreamy.Debugger.Presentation.InputBridge.Down(KeyCode.KeypadEnter))) Commit();
            });
            Action.onClick.AddListener(() => entry?.Action?.Invoke());
            Boolean.onValueChanged.AddListener(value => CommitChoice(value));
            Enumeration.onValueChanged.AddListener(index =>
            {
                if (binding || entry?.Member == null || !entry.Member.ValueType.IsEnum) return;
                var names = Enum.GetNames(entry.Member.ValueType);
                if (index < names.Length) CommitChoice(Enum.Parse(entry.Member.ValueType, names[index]));
            });
            Apply.onClick.AddListener(Commit);
            Cancel.onClick.AddListener(CancelDraft);
        }
        private bool Editable => entry?.Member != null && entry.Member.Writable && InspectionValues.IsEditable(entry.Member.ValueType) && policy();
        private bool wasEditable;
        public void Bind(Entry value, Func<bool> canEdit, bool refresh = true)
        {
            var changed = !ReferenceEquals(entry, value);
            if (changed && Value.isFocused) { binding = true; Value.DeactivateInputField(); binding = false; }
            entry = value; policy = canEdit;
            if (changed || refresh || wasEditable != Editable || entry.Drafting) Refresh();
            wasEditable = Editable;
        }
        public void Refresh()
        {
            if (entry == null) return;
            var label = entry.ReadLabel != null ? entry.ReadLabel() : entry.Label;
            Caption.text = label;
            Caption.gameObject.SetActive(entry.Member != null || entry.Action == null);
            Actions.offsetMin = new Vector2(8, entry.Member == null ? -48 : -72);
            Actions.offsetMax = new Vector2(-8, entry.Member == null ? -4 : -28);
            Action.gameObject.SetActive(entry.Action != null); ActionLabel.text = entry.Member == null ? label : "Reveal";
            Action.interactable = entry.Available == null || entry.Available();
            var type = entry.Member?.ValueType;
            var isBoolean = type == typeof(bool);
            var isEnum = type != null && type.IsEnum && !type.IsDefined(typeof(FlagsAttribute), false);
            Boolean.gameObject.SetActive(isBoolean); Boolean.interactable = Editable;
            Enumeration.gameObject.SetActive(isEnum); Enumeration.interactable = Editable;
            ColorPreview.gameObject.SetActive(type == typeof(Color));
            Value.gameObject.SetActive(entry.Member != null && !isBoolean && !isEnum);
            Value.interactable = true; Value.readOnly = !Editable;
            if (!Editable && entry.Drafting) { entry.Drafting = false; entry.Draft = null; Value.DeactivateInputField(); }
            if (entry.Member != null && !entry.Drafting && !Value.isFocused && entry.Error == null)
            {
                if (entry.Member.TryRead(out var current, out var error))
                {
                    entry.Baseline = current;
                    RefreshChoice(current);
                    var text = InspectionValues.Format(current);
                    if (text.Length > 4096) text = text.Substring(0, 256) + " ... [truncated]";
                    binding = true; Value.SetTextWithoutNotify(text); binding = false;
                }
                else entry.Error = error;
            }
            else if (entry.Drafting && !Value.isFocused) Value.SetTextWithoutNotify(entry.Draft ?? "");
            Apply.gameObject.SetActive(Editable && entry.Drafting);
            Cancel.gameObject.SetActive(entry.Drafting || entry.Error != null);
            Hint.text = entry.Error ?? entry.Hint ?? (entry.Member != null ? entry.Member.ValueType.Name + (entry.Member.Writable ? "" : " / readonly") : "");
            Hint.color = entry.Error != null ? Dreamy.Debugger.Foundation.UI.DebuggerPalette.Warning : Dreamy.Debugger.Foundation.UI.DebuggerPalette.Muted;
        }
        private void RefreshChoice(object current)
        {
            binding = true;
            try
            {
                if (current is bool flag) { Boolean.SetIsOnWithoutNotify(flag); BooleanLabel.text = flag ? "True" : "False"; }
                if (current is Color color) ColorPreview.color = color;
                if (current != null && current.GetType().IsEnum && !current.GetType().IsDefined(typeof(FlagsAttribute), false))
                {
                    var type = current.GetType(); var names = Enum.GetNames(type);
                    var name = Enum.GetName(type, current); var unknown = name == null ? "Unknown: " + current : null;
                    if (enumType != type || enumUnknown != unknown)
                    {
                        Enumeration.ClearOptions(); var labels = new System.Collections.Generic.List<string>(names);
                        if (unknown != null) labels.Add(unknown);
                        Enumeration.AddOptions(labels); enumType = type; enumUnknown = unknown;
                    }
                    Enumeration.SetValueWithoutNotify(name == null ? names.Length : Array.IndexOf(names, name));
                }
            }
            finally { binding = false; }
        }
        private void CommitChoice(object value)
        {
            if (binding || entry == null || !Editable) return;
            if (!entry.Member.TryCommit(entry.Baseline, value, policy, out var error)) entry.Error = error;
            if (entry.Member.TryRead(out var actual, out var readError)) { entry.Baseline = actual; RefreshChoice(actual); }
            else entry.Error = readError;
            Refresh();
        }
        private void Commit()
        {
            if (entry == null || !Editable || !entry.Drafting) return;
            if (!InspectionValues.TryParse(entry.Draft ?? "", entry.Member.ValueType, out var parsed, out var error) ||
                !entry.Member.TryCommit(entry.Baseline, parsed, policy, out error)) entry.Error = error;
            else { entry.Drafting = false; entry.Draft = null; entry.Error = null; Value.DeactivateInputField(); }
            Refresh();
        }
        public void CancelDraft()
        {
            if (entry == null) return;
            entry.Drafting = false; entry.Draft = null; entry.Error = null; Value.DeactivateInputField(); Refresh();
        }
        public void OnCancel(BaseEventData data) { CancelDraft(); data.Use(); }
        public void Clear() { binding = true; Value.DeactivateInputField(); binding = false; entry = null; policy = null; }
    }
}
