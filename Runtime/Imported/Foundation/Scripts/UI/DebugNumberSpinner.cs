namespace Dreamy.Debugger.Foundation.UI
{
    using System;
    using System.Globalization;
    using Internal;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    [AddComponentMenu(ComponentMenuPaths.NumberSpinner)]
    public class DebugNumberSpinner : InputField
    {
        private double _currentValue;
        private double _dragStartAmount;
        private double _dragStep;
        public float DragSensitivity = 0.01f;
        public double MaxValue = double.MaxValue;
        public double MinValue = double.MinValue;
        public bool ExactInt64 { get; set; }

        // Int64 uses text/arrows, not the legacy double-based horizontal drag path.
        public bool TryStepInt64(double amount)
        {
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var current)) return false;
            try
            {
                var next = (decimal)current + decimal.Truncate((decimal)amount);
                var min = Math.Max((decimal)long.MinValue, (decimal)MinValue);
                var max = Math.Min((decimal)long.MaxValue, (decimal)MaxValue);
                next = Math.Max(min, Math.Min(max, next));
                text = ((long)next).ToString(CultureInfo.InvariantCulture);
                return true;
            }
            catch (OverflowException) { return false; }
        }

        protected override void Awake()
        {
            base.Awake();

            if (contentType != ContentType.IntegerNumber && contentType != ContentType.DecimalNumber)
            {
                Debug.LogError("[DebugNumberSpinner] contentType must be integer or decimal. Defaulting to integer");
                contentType = ContentType.DecimalNumber;
            }
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            //Debug.Log("OnPointerClick (isFocused: {0}, isUsed: {1}, isDragging: {2})".Fmt(isFocused, eventData.used, eventData.dragging));

            if (!interactable)
            {
                return;
            }

            if (eventData.dragging)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(gameObject, eventData);

            base.OnPointerClick(eventData);

            if ((m_Keyboard == null || !m_Keyboard.active))
            {
                OnSelect(eventData);
            }
            else
            {
                UpdateLabel();
                eventData.Use();
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            //Debug.Log("OnPointerDown (isFocused: {0}, isUsed: {1})".Fmt(isFocused, eventData.used));

            //base.OnPointerDown(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            //Debug.Log("OnPointerUp (isFocused: {0}, isUsed: {1})".Fmt(isFocused, eventData.used));

            //base.OnPointerUp(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (!interactable)
            {
                return;
            }

            //Debug.Log("OnBeginDrag (isFocused: {0}, isUsed: {1}, delta: {2})".Fmt(isFocused, eventData.used, eventData.delta));

            // Pass event to parent if it is a vertical drag
            if (Mathf.Abs(eventData.delta.y) > Mathf.Abs(eventData.delta.x))
            {
                //Debug.Log("Passing To Parent");

                var parent = transform.parent;

                if (parent != null)
                {
                    eventData.pointerDrag = ExecuteEvents.GetEventHandler<IBeginDragHandler>(parent.gameObject);

                    if (eventData.pointerDrag != null)
                    {
                        ExecuteEvents.Execute(eventData.pointerDrag, eventData, ExecuteEvents.beginDragHandler);
                    }
                }

                return;
            }
            if (ExactInt64) return;
            eventData.Use();

            _dragStartAmount = double.Parse(text);
            _currentValue = _dragStartAmount;

            var minStep = 1f;

            // Use a larger minimum step for integer numbers, since there are no fractional values
            if (contentType == ContentType.IntegerNumber)
            {
                minStep *= 10;
            }

            _dragStep = Math.Max(minStep, _dragStartAmount*0.05f);

            if (isFocused)
            {
                DeactivateInputField();
            }
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (ExactInt64) return;
            if (!interactable)
            {
                return;
            }

            //Debug.Log("OnDrag (isFocused: {0}, isUsed: {1})".Fmt(isFocused, eventData.used));

            var diff = eventData.delta.x;

            _currentValue += Math.Abs(_dragStep)*diff*DragSensitivity;
            _currentValue = Math.Round(_currentValue, 2);

            if (_currentValue > MaxValue)
            {
                _currentValue = MaxValue;
            }

            if (_currentValue < MinValue)
            {
                _currentValue = MinValue;
            }

            if (contentType == ContentType.IntegerNumber)
            {
                text = ((int) Math.Round(_currentValue)).ToString();
            }
            else
            {
                text = _currentValue.ToString();
            }
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (ExactInt64) return;
            if (!interactable)
            {
                return;
            }

            //Debug.Log("OnEndDrag (isFocused: {0}, isUsed: {1})".Fmt(isFocused, eventData.used));

            //base.OnEndDrag(eventData);

            eventData.Use();

            if (_dragStartAmount != _currentValue)
            {
                DeactivateInputField();
                SendOnSubmit();
            }
        }
    }
}
