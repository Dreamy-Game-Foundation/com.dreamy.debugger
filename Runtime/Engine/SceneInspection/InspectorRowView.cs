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
            public Action Action, Pick, Pin, Copy;
            public string Key, CompareKey, CompareName, Group;
            public object CompareValue;
            public bool Pinned, PickerOnly;
            public object Baseline;
            public bool Drafting;
            public Func<bool> Available;
            public Func<string> ReadLabel;
            public string[] ChoiceLabels;
            public object[] ChoiceValues;
            public bool NoScrub;
            public bool Inline, Dense;
            public bool? Expanded;
            public string IconKey;
            public Sprite CustomIcon;
            public float Height => Dense ? (Member == null || Inline || Member.ValueType == typeof(bool) ? 46 : 76) + (CompareName == null ? 0 : 22) : Member == null ? 44 : (!string.IsNullOrEmpty(Error) || CompareName != null) ? (Inline ? 72 : 100) : Member.ValueType == typeof(bool) ? 48 : Inline ? 52 : 76;
        }
        public Text Caption, Hint;
        public TouchNumberEditor TouchEditor;
        internal InspectionHistory History;
        public Button PickerButton, PinButton, CopyButton;
        public Text PickerLabel;
        public GameObject SectionDivider;
        internal bool CompactWorkspace, ShowPropertyActions;
        internal Func<Entry, bool> WorkspaceDraftAllowed;
        private bool WorkspaceOwnsDraft => WorkspaceDraftAllowed != null;
        public InputField Value;
        public Toggle Boolean;
        public Text BooleanLabel;
        public Dropdown Enumeration;
        public Image ColorPreview;
        public Button Action, Apply, Cancel;
        public Text ActionLabel;
        public Image FoldoutIcon, ComponentIcon;
        public Sprite[] ComponentSprites;
        public RectTransform Actions;
        public RectTransform VectorControls;
        public InputField[] AxisInputs;
        public NumericDragHandle[] AxisHandles;
        public NumericDragHandle LabelHandle;
        public Button StepDown, StepUp;
        private Entry entry;
        private Func<bool> policy;
        private bool binding;
        private Type enumType;
        private string enumUnknown;
        private string changedIconKey;
        private bool choicesInitialized;
        private LayoutElement valueLayout;
        private object dragSource;
        private Entry dragEntry;
        private int dragAxis;
        private void Awake()
        {
            PickerButton?.onClick.AddListener(() => entry?.Pick?.Invoke());
            PinButton?.onClick.AddListener(() => entry?.Pin?.Invoke());
            valueLayout = Value.GetComponent<LayoutElement>();
            BindTouch(Value, -1);
            Value.onValueChanged.AddListener(text =>
            {
                if (binding || !Editable) return;
                if (!MayDraft()) { Value.SetTextWithoutNotify(InspectionValues.Format(entry.Baseline)); return; }
                entry.Drafting = true;
                entry.Draft = text;
            });
            Value.onEndEdit.AddListener(text =>
            {
                // Pointer blur and the mobile keyboard's Done button never write.
                if (!binding && (Dreamy.Debugger.Presentation.InputBridge.Down(KeyCode.Return) || Dreamy.Debugger.Presentation.InputBridge.Down(KeyCode.KeypadEnter))) Commit();
            });
            CopyButton?.onClick.AddListener(() => entry?.Copy?.Invoke());
            Action.onClick.AddListener(() => entry?.Action?.Invoke());
            Boolean.onValueChanged.AddListener(value => CommitChoice(value));
            Enumeration.onValueChanged.AddListener(index =>
            {
                if (binding || entry?.Member == null) return;
                if (entry.ChoiceValues != null) { if (index < entry.ChoiceValues.Length) CommitChoice(entry.ChoiceValues[index]); return; }
                if (!entry.Member.ValueType.IsEnum) return;
                var names = Enum.GetNames(entry.Member.ValueType);
                if (index < names.Length) CommitChoice(Enum.Parse(entry.Member.ValueType, names[index]));
            });
            Apply.onClick.AddListener(Commit);
            Cancel.onClick.AddListener(CancelDraft);
            StepDown?.onClick.AddListener(() => Step(-1)); StepUp?.onClick.AddListener(() => Step(1));
            if (LabelHandle != null) HookDrag(LabelHandle, -1);
            if (AxisInputs != null) for (var i = 0; i < AxisInputs.Length; i++)
            {
                var axis = i;
                BindTouch(AxisInputs[i], axis);
                AxisInputs[i].onValueChanged.AddListener(text => SetAxisDraft(axis, text));
                AxisInputs[i].onEndEdit.AddListener(_ => { if (!binding && (Dreamy.Debugger.Presentation.InputBridge.Down(KeyCode.Return) || Dreamy.Debugger.Presentation.InputBridge.Down(KeyCode.KeypadEnter))) Commit(); });
                if (AxisHandles != null && i < AxisHandles.Length) HookDrag(AxisHandles[i], axis);
            }
        }
        private void Step(int direction)
        {
            if (!Editable || entry.NoScrub || entry.Drafting || !InspectionNumbers.Scalar(entry.Member.ValueType)) return;
            if (!entry.Member.TryRead(out var current, out var error)) { entry.Error = error; Refresh(); return; }
            entry.Baseline = current;
            if (!InspectionNumbers.TryOffset(current, -1, direction * (InspectionNumbers.Integral(entry.Member.ValueType) ? 6 : 5), out var value, out error))
            { entry.Error = error; Refresh(); return; }
            CommitChoice(value);
        }
        private void HookDrag(NumericDragHandle handle, int axis)
        {
            handle.Begin = () => BeginDrag(axis);
            handle.Preview = PreviewDrag;
            handle.End = apply => { if (dragEntry == entry && dragEntry != null) { if (apply && entry.Error == null) Commit(); else if (!apply) CancelDraft(); } dragEntry = null; dragSource = null; };
        }
        private bool MayDraft() => WorkspaceDraftAllowed == null || WorkspaceDraftAllowed(entry);
        private bool BeginDrag(int axis)
        {
            if (CompactWorkspace || !MayDraft() || !Editable || entry.NoScrub || entry.Drafting || (axis < 0 && !InspectionNumbers.Scalar(entry.Member.ValueType))) return false;
            if (!entry.Member.TryRead(out dragSource, out var error)) { entry.Error = error; return false; }
            entry.Baseline = dragSource; dragEntry = entry; dragAxis = axis; return true;
        }
        private void PreviewDrag(float distance)
        {
            if (dragEntry != entry || !Editable) { AbortDrags(); return; }
            if (!InspectionNumbers.TryOffset(dragSource, dragAxis, distance, out var candidate, out var error))
            { entry.Error = error; Refresh(); return; }
            entry.Draft = InspectionValues.Format(candidate); entry.Drafting = true; entry.Error = null;
            Refresh();
        }
        private void AbortDrags()
        {
            LabelHandle?.Abort();
            if (AxisHandles != null) foreach (var handle in AxisHandles) handle?.Abort();
        }
        private bool Vector => entry?.Member != null && AxisInputs != null && AxisInputs.Length >= InspectionNumbers.Axes(entry.Member.ValueType) && InspectionNumbers.Axes(entry.Member.ValueType) > 0;
        private void SetAxisDraft(int axis, string text)
        {
            if (binding || !Editable || !Vector) return;
            if (!MayDraft())
            {
                AxisInputs[axis].SetTextWithoutNotify(InspectionNumbers.AxisText(entry.Baseline, axis));
                return;
            }
            // Keep incomplete text in the per-axis inputs. The compound draft is parsed only at commit.
            var parts = new string[InspectionNumbers.Axes(entry.Member.ValueType)];
            for (var i = 0; i < parts.Length; i++) parts[i] = AxisInputs[i].text;
            entry.Draft = string.Join(", ", parts); entry.Drafting = true;
            if (!WorkspaceOwnsDraft) { Apply.gameObject.SetActive(true); Cancel.gameObject.SetActive(true); }
        }
        private void BindTouch(InputField input, int axis)
        {
            if (input is TouchNumberField field)
                {
                field.UseEditor = () => TouchEditor != null && Editable && (axis >= 0 || InspectionNumbers.Scalar(entry.Member.ValueType));
                field.OpenEditor = () =>
                {
                    if (TouchEditor == null || !Editable || entry.Drafting || !MayDraft()) return;
                    if (axis < 0 && !InspectionNumbers.Scalar(entry.Member.ValueType)) return;
                    TouchEditor.Open(entry.Member, entry.Label, axis, policy, History);
                };
            }
        }
        private bool Editable => entry?.Member != null && !entry.PickerOnly && entry.Member.Writable && InspectionValues.IsEditable(entry.Member.ValueType) && policy();
        private bool wasEditable, propertyActionsShown;
        public void Bind(Entry value, Func<bool> canEdit, bool refresh = true)
        {
            var changed = !ReferenceEquals(entry, value);
            if (changed)
            {
                AbortDrags(); enumType = null; enumUnknown = null; choicesInitialized = false;
                binding = true; if (AxisInputs != null) foreach (var field in AxisInputs) field.DeactivateInputField(); binding = false;
            }
            if (changed && Value.isFocused) { binding = true; Value.DeactivateInputField(); binding = false; }
            entry = value; policy = canEdit;
            if (changed || refresh || wasEditable != Editable || entry.Drafting || propertyActionsShown != ShowPropertyActions) Refresh();
            propertyActionsShown = ShowPropertyActions;
            wasEditable = Editable;
        }
        public void Refresh()
        {
            if (entry == null) return;
            var label = entry.ReadLabel != null ? entry.ReadLabel() : entry.Label;
            var singleLine = entry.Inline || entry.Member?.ValueType == typeof(bool);
            var iconHeader = entry.Member == null && (entry.CustomIcon != null || entry.IconKey != null);
            if (SectionDivider != null) SectionDivider.SetActive(iconHeader);
            Caption.text = label;
            Caption.gameObject.SetActive(entry.Member != null || entry.Action == null && !iconHeader);
            Caption.rectTransform.anchorMin = Caption.rectTransform.anchorMax = new Vector2(0, 1);
            Caption.rectTransform.anchorMax = new Vector2(singleLine ? .3f : 1, 1);
            Caption.rectTransform.offsetMin = new Vector2(8, singleLine ? -44 : -26);
            Caption.rectTransform.offsetMax = new Vector2(-4, -2);
            Actions.anchorMin = new Vector2(singleLine && entry.Member != null ? .3f : 0, 1); Actions.anchorMax = Vector2.one;
            Actions.offsetMin = new Vector2(8, entry.Member == null ? -40 : -68);
            Actions.offsetMax = new Vector2(-8, entry.Member == null ? -4 : -28);
            if (singleLine && entry.Member != null) { Actions.offsetMin = new Vector2(8,-44); Actions.offsetMax = new Vector2(-8,-4); }
            if (entry.Dense)
            {
                Caption.color = entry.Member == null ? Foundation.UI.DebuggerPalette.Ivory : Foundation.UI.DebuggerPalette.Muted;
                Caption.rectTransform.offsetMin = new Vector2(14, singleLine || entry.Member == null ? -46 : -26);
                Actions.offsetMin = new Vector2(entry.Member == null ? 4 : 12, entry.Member == null || singleLine ? -44 : -73);
                Actions.offsetMax = new Vector2(-12, entry.Member == null || singleLine ? 0 : -29);
            }
            Action.gameObject.SetActive(entry.Action != null || iconHeader);
            ActionLabel.text = entry.Member == null ? label : "Reveal";
            Action.interactable = entry.Action != null && (entry.Available == null || entry.Available());
            if (entry.Member == null && (entry.Action != null || iconHeader))
            {
                ActionLabel.alignment = TextAnchor.MiddleLeft;
                ActionLabel.rectTransform.offsetMin = new Vector2(entry.IconKey == null && entry.CustomIcon == null ? 28 : 52, 0);
            }
            else
            {
                ActionLabel.alignment = TextAnchor.MiddleCenter;
                ActionLabel.rectTransform.offsetMin = Vector2.zero;
            }
            if (FoldoutIcon != null)
            {
                FoldoutIcon.enabled = entry.Expanded.HasValue;
                FoldoutIcon.rectTransform.localEulerAngles = new Vector3(0, 0, entry.Expanded == true ? -90 : 0);
            }
            if (ComponentIcon != null)
            {
                if (entry.CustomIcon != null) ComponentIcon.sprite = entry.CustomIcon;
                else if (entry.IconKey == null) ComponentIcon.sprite = null;
                else if ((changedIconKey != entry.IconKey || ComponentIcon.sprite == null || ComponentIcon.sprite.name != "inspector-" + entry.IconKey) && ComponentSprites != null)
                    ComponentIcon.sprite = Array.Find(ComponentSprites, sprite => sprite != null && sprite.name == "inspector-" + entry.IconKey);
                ComponentIcon.enabled = ComponentIcon.sprite != null;
                changedIconKey = entry.IconKey;
            }
            var type = entry.Member?.ValueType;
            if (valueLayout != null)
            {
                var shortNumber = entry.Dense && InspectionNumbers.Scalar(type);
                valueLayout.flexibleWidth = shortNumber ? 0 : 4;
                valueLayout.preferredWidth = shortNumber ? 166 : -1;
            }
            var isBoolean = type == typeof(bool);
            var isEnum = entry.ChoiceValues != null || type != null && type.IsEnum && !type.IsDefined(typeof(FlagsAttribute), false);
            Boolean.gameObject.SetActive(isBoolean); Boolean.interactable = Editable;
            Enumeration.gameObject.SetActive(isEnum); Enumeration.interactable = Editable;
            ColorPreview.gameObject.SetActive(type == typeof(Color));
            var contextualPicker = entry.Dense && entry.Pick != null && entry.Member.Writable && policy() && !entry.Drafting;
            Value.gameObject.SetActive(entry.Member != null && !isBoolean && !isEnum && !Vector && !contextualPicker);
            if (VectorControls != null) VectorControls.gameObject.SetActive(Vector);
            if (AxisInputs != null) for (var i = 0; i < AxisInputs.Length; i++)
            { AxisInputs[i].gameObject.SetActive(Vector && i < InspectionNumbers.Axes(type)); AxisInputs[i].readOnly = !Editable; }
            if (PickerButton != null)
            {
                PickerButton.gameObject.SetActive(entry.Pick != null && entry.Member.Writable && policy() && !entry.Drafting);
                if (PickerLabel != null) PickerLabel.text = entry.Dense ? InspectionValues.Format(entry.Baseline) : "Pick";
            }
            if (PinButton != null)
            {
                PinButton.gameObject.SetActive(entry.Pin != null && !entry.Drafting && (!entry.Dense || ShowPropertyActions));
                PinButton.targetGraphic.color = entry.Pinned ? Foundation.UI.DebuggerPalette.Selected : Foundation.UI.DebuggerPalette.Control;
            }
            if (CopyButton != null) CopyButton.gameObject.SetActive(entry.Copy != null && !entry.Drafting && (!entry.Dense || ShowPropertyActions));
            Value.interactable = true; Value.readOnly = !Editable;
            var step = !entry.Dense && !Application.isMobilePlatform && Editable && !entry.NoScrub && !entry.Drafting && InspectionNumbers.Scalar(type);
            if (StepDown != null) StepDown.gameObject.SetActive(step);
            if (StepUp != null) StepUp.gameObject.SetActive(step);
            if (!Editable) { AbortDrags(); if (entry.Drafting) { entry.Drafting = false; entry.Draft = null; Value.DeactivateInputField(); } }
            if (entry.Member != null && !entry.Drafting && !FieldFocused && entry.Error == null)
            {
                if (entry.Member.TryRead(out var current, out var error))
                {
                    entry.Baseline = current;
                    RefreshChoice(current);
                    var text = InspectionValues.Format(current);
                    if (contextualPicker && PickerLabel != null) PickerLabel.text = text;
                    if (text.Length > 4096) text = text.Substring(0, 256) + " ... [truncated]";
                    binding = true; Value.SetTextWithoutNotify(text); binding = false;
                    if (Vector) SetVectorText(text);
                }
                else entry.Error = error;
            }
            else if (entry.Drafting && !Value.isFocused) Value.SetTextWithoutNotify(entry.Draft ?? "");
            if (Vector && entry.Drafting) SetVectorText(entry.Draft ?? "");
            Apply.gameObject.SetActive(!WorkspaceOwnsDraft && Editable && entry.Drafting);
            Cancel.gameObject.SetActive(!WorkspaceOwnsDraft && (entry.Drafting || entry.Error != null));
            var comparison = entry.CompareName != null && !Equals(entry.CompareValue, entry.Baseline)
                ? entry.CompareName + "  ·  " + InspectionValues.Format(entry.CompareValue) : null;
            Hint.text = (entry.Dense ? comparison : entry.Error ?? comparison) ?? entry.Hint ?? (entry.Member != null && !entry.Member.Writable ? "Read only" : "");
            Hint.gameObject.SetActive(comparison != null || !entry.Dense && (!string.IsNullOrEmpty(entry.Error) || entry.Member != null && !entry.Member.Writable));
            Hint.color = entry.Error != null ? Dreamy.Debugger.Foundation.UI.DebuggerPalette.Warning : Dreamy.Debugger.Foundation.UI.DebuggerPalette.Muted;
        }
        private bool FieldFocused
        {
            get { if (Value.isFocused) return true; if (AxisInputs != null) foreach (var field in AxisInputs) if (field.isFocused) return true; return false; }
        }
        private void SetVectorText(string text)
        {
            var parts = text.Split(','); binding = true;
            for (var i = 0; i < AxisInputs.Length && i < parts.Length; i++)
                if (!AxisInputs[i].isFocused) AxisInputs[i].SetTextWithoutNotify(parts[i].Trim());
            binding = false;
        }
        private void RefreshChoice(object current)
        {
            binding = true;
            try
            {
                if (entry.ChoiceValues != null)
                {
                    var labels = new System.Collections.Generic.List<string>(entry.ChoiceLabels);
                    var index = Array.FindIndex(entry.ChoiceValues, candidate => Equals(candidate, current));
                    var unknown = index < 0 ? "Unknown: " + current : null;
                    if (index < 0) { labels.Add(unknown); index = labels.Count - 1; }
                    if (!choicesInitialized || enumUnknown != unknown)
                    { Enumeration.ClearOptions(); Enumeration.AddOptions(labels); choicesInitialized = true; enumUnknown = unknown; }
                    Enumeration.SetValueWithoutNotify(index); return;
                }
                if (current is bool flag) { Boolean.SetIsOnWithoutNotify(flag); BooleanLabel.text = flag ? "✓" : ""; }
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
        private bool Write(object value, out string error) => History != null
            ? History.Write(entry.Member, entry.Baseline, value, policy, out error)
            : entry.Member.TryCommit(entry.Baseline, value, policy, out error);

        private void CommitChoice(object value)
        {
            if (binding || entry == null || !Editable) return;
            entry.Error = Write(value, out var error) ? null : error;
            if (entry.Member.TryRead(out var actual, out var readError)) { entry.Baseline = actual; RefreshChoice(actual); }
            else entry.Error = readError;
            Refresh();
        }
        private void Commit()
        {
            if (entry == null || !Editable || !entry.Drafting) return;
            if (!InspectionValues.TryParse(entry.Draft ?? "", entry.Member.ValueType, out var parsed, out var error) ||
                !Write(parsed, out error)) entry.Error = error;
            else { entry.Drafting = false; entry.Draft = null; entry.Error = null; Value.DeactivateInputField(); }
            Refresh();
        }
        public void CancelDraft()
        {
            if (entry == null) return;
            entry.Drafting = false; entry.Draft = null; entry.Error = null; Value.DeactivateInputField(); Refresh();
        }
        public void OnCancel(BaseEventData data) { CancelDraft(); data.Use(); }
        private void OnDisable() { AbortDrags(); }
        public void Clear() { AbortDrags(); binding = true; Value.DeactivateInputField(); if (AxisInputs != null) foreach (var field in AxisInputs) field.DeactivateInputField(); binding = false; entry = null; policy = null; }
    }
}
