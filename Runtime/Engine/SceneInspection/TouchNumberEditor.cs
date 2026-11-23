using System;
using Dreamy.Debugger.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dreamy.Debugger.SceneInspection
{
    public sealed class TouchNumberEditor : MonoBehaviour, ICancelHandler
    {
        public RectTransform Surface;
        public Text Title, Error, StepLabel;
        public InputField Number;
        public Button Apply, Cancel, Minus, Plus, Step, Scrim;
        public Button[] Axes;
        public Slider Range;
        public bool SingleAxisLayout;
        private InspectionMember member;
        private InspectionHistory history;
        private object baseline, draft;
        private Func<bool> writable;
        private string label;
        private int axis, stepIndex;
        private bool syncing;

        private void Awake()
        {
            Apply.onClick.AddListener(Commit);
            Cancel.onClick.AddListener(() => Dismiss());
            Scrim.onClick.AddListener(() => Dismiss());
            Minus.onClick.AddListener(() => Change(-1));
            Plus.onClick.AddListener(() => Change(1));
            Step.onClick.AddListener(() => { stepIndex = (stepIndex + 1) % 3; RefreshStep(); });
            for (var i = 0; i < Axes.Length; i++)
            {
                var index = i;
                Axes[i].onClick.AddListener(() =>
                {
                    if (!ReadNumber(out var value)) return;
                    draft = value;
                    axis = index;
                    RefreshNumber();
                });
            }
            Range.onValueChanged.AddListener(value => { if (!syncing) Number.text = InspectionValues.Format(value); });
            Number.onValueChanged.AddListener(text =>
            {
                if (Range.gameObject.activeSelf && member != null &&
                    InspectionValues.TryParse(text, typeof(float), out var value, out _))
                    Range.SetValueWithoutNotify((float)value);
            });
        }

        internal void Open(InspectionMember target, string caption, int selectedAxis, Func<bool> canWrite, InspectionHistory editHistory)
        {
            if (!canWrite() || !target.TryRead(out var original, out _)) return;
            member = target;
            history = editHistory;
            baseline = draft = original;
            writable = canWrite;
            axis = selectedAxis;
            stepIndex = 1;
            label = caption;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var count = InspectionNumbers.Axes(target.ValueType);
            for (var i = 0; i < Axes.Length; i++) Axes[i].gameObject.SetActive(!SingleAxisLayout && i < count);
            Step.gameObject.SetActive(!SingleAxisLayout);
            if (SingleAxisLayout) Step.transform.parent.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            Range.gameObject.SetActive(axis < 0 && target.NormalizedRange);
            RefreshNumber();
        }

        private void Update()
        {
            if (writable == null || !writable()) { Dismiss(); return; }
            var available = ((RectTransform)transform).rect;
            var keyboard = TouchScreenKeyboard.area.height;
            var keyboardInset = Screen.height > 0 ? keyboard / Screen.height * available.height : 0;
            var desiredHeight = SingleAxisLayout ? (Range.gameObject.activeSelf ? 224 : 178) :
                Range.gameObject.activeSelf ? 288 : axis >= 0 ? 234 : 180;
            Surface.sizeDelta = new Vector2(Mathf.Clamp(available.width - 12, 0, 440),
                Mathf.Clamp(available.height - keyboardInset - 12, 0, desiredHeight));
            var availableAboveKeyboard = available.height - keyboardInset;
            var y = SingleAxisLayout ? (availableAboveKeyboard - Surface.sizeDelta.y) * .5f : 6;
            Surface.anchoredPosition = new Vector2(0, keyboardInset + Mathf.Max(6, y));
        }

        private void RefreshNumber()
        {
            syncing = true;
            Number.SetTextWithoutNotify(axis < 0 ? InspectionValues.Format(draft) : InspectionNumbers.AxisText(draft, axis));
            if (Range.gameObject.activeSelf && draft is float value) Range.SetValueWithoutNotify(value);
            syncing = false;
            Title.text = axis < 0 ? label : label + " · " + "XYZW"[axis];
            for (var i = 0; i < Axes.Length; i++)
                if (Axes[i].targetGraphic != null)
                    Axes[i].targetGraphic.color = i == axis
                        ? Foundation.UI.DebuggerPalette.Selected
                        : Foundation.UI.DebuggerPalette.Control;
            PlaceBelowTitle();
            Error.text = "";
            RefreshStep();
        }

        private void PlaceBelowTitle()
        {
            var controls = (RectTransform)Number.transform.parent;
            controls.anchoredPosition = new Vector2(0, SingleAxisLayout || axis < 0 ? -30 : -84);
            var rangeRect = (RectTransform)Range.transform;
            rangeRect.anchoredPosition = new Vector2(0, -84);
            var errorTop = Range.gameObject.activeSelf || !SingleAxisLayout && axis >= 0 ? 138 : 84;
            Error.rectTransform.anchoredPosition = new Vector2(0, -errorTop);
            ((RectTransform)Title.transform.parent).sizeDelta = new Vector2(0, errorTop + 36);
        }

        private bool IsIntegral => axis < 0 ? InspectionNumbers.Integral(member.ValueType) :
            member.ValueType == typeof(Vector2Int) || member.ValueType == typeof(Vector3Int);
        private void RefreshStep() => StepLabel.text = "Step " + (IsIntegral
            ? new[] { "1", "10", "100" }[stepIndex]
            : new[] { "0.01", "0.1", "1" }[stepIndex]);

        private bool ReadNumber(out object value)
        {
            string error;
            var ok = axis < 0
                ? InspectionValues.TryParse(Number.text, member.ValueType, out value, out error)
                : InspectionNumbers.TryAxis(draft, axis, Number.text, out value, out error);
            if (!ok) Error.text = error;
            return ok;
        }

        private void Change(int direction)
        {
            if (!ReadNumber(out var value)) return;
            var distance = IsIntegral ? new[] { 6f, 60f, 600f }[stepIndex] : new[] { .5f, 5f, 50f }[stepIndex];
            if (!InspectionNumbers.TryOffset(value, axis, direction * distance, out var next, out var error)) { Error.text = error; return; }
            draft = next;
            RefreshNumber();
        }

        private void Commit()
        {
            if (!ReadNumber(out var value)) return;
            if (member.NormalizedRange && value is float normalized && (normalized < 0 || normalized > 1))
            {
                Error.text = "Enter a value from 0 to 1.";
                return;
            }
            string error;
            var applied = history == null ? member.TryCommit(baseline, value, writable, out error) : history.Write(member, baseline, value, writable, out error);
            if (!applied) { Error.text = error; return; }
            Dismiss();
        }
        public bool Dismiss()
        {
            if (!gameObject.activeSelf) return false;
            Number.DeactivateInputField();
            member = null;
            history = null;
            baseline = draft = null;
            writable = null;
            gameObject.SetActive(false);
            return true;
        }
        public void OnCancel(BaseEventData e) { Dismiss(); e.Use(); }
    }
}
