using System.Globalization;

namespace Dreamy.Debugger.Presentation.UI.Controls.Data
{
    using System;
    using System.Collections.Generic;
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.UI;
    using UnityEngine;
    using UnityEngine.UI;

    public class NumberControl : DataBoundControl
    {
        private static readonly Type[] IntegerTypes =
        {
            typeof (int), typeof (long), typeof (short), typeof (byte), typeof (sbyte), typeof (uint), typeof (ushort)
        };

        private static readonly Type[] DecimalTypes =
        {
            typeof (float), typeof (double)
        };

        public static readonly Dictionary<Type, ValueRange> ValueRanges = new Dictionary<Type, ValueRange>
        {
            {typeof (int), new ValueRange {MaxValue = int.MaxValue, MinValue = int.MinValue}},
            {typeof (long), new ValueRange {MaxValue = long.MaxValue, MinValue = long.MinValue}},
            {typeof (short), new ValueRange {MaxValue = short.MaxValue, MinValue = short.MinValue}},
            {typeof (byte), new ValueRange {MaxValue = byte.MaxValue, MinValue = byte.MinValue}},
            {typeof (sbyte), new ValueRange {MaxValue = sbyte.MaxValue, MinValue = sbyte.MinValue}},
            {typeof (uint), new ValueRange {MaxValue = uint.MaxValue, MinValue = uint.MinValue}},
            {typeof (ushort), new ValueRange {MaxValue = ushort.MaxValue, MinValue = ushort.MinValue}},
            {typeof (float), new ValueRange {MaxValue = float.MaxValue, MinValue = float.MinValue}},
            {typeof (double), new ValueRange {MaxValue = double.MaxValue, MinValue = double.MinValue}}
        };

        internal bool EmphasizeReadOnly;
        private string _lastValue;
        private Type _type;
        public GameObject[] DisableOnReadOnly;
        public DebugNumberButton DownNumberButton;

        [RequiredField] public DebugNumberSpinner NumberSpinner;

        [RequiredField] public Text Title;

        public DebugNumberButton UpNumberButton;

        protected override void Start()
        {
            base.Start();
            NumberSpinner.onEndEdit.AddListener(OnValueChanged);
        }

        private void OnValueChanged(string newValue)
        {
            try
            {
                var num = Convert.ChangeType(newValue, _type, CultureInfo.InvariantCulture);
                UpdateValue(num);
                OnValueUpdated(Property.GetValue());
            }
            catch (Exception)
            {
                NumberSpinner.text = _lastValue;
            }

            LayoutRebuilder.MarkLayoutForRebuild(GetComponent<RectTransform>());
        }

        protected override void OnBind(string propertyName, Type t)
        {
            base.OnBind(propertyName, t);
            Title.text = propertyName;

            if (IsIntegerType(t))
            {
                NumberSpinner.contentType = InputField.ContentType.IntegerNumber;
            }
            else if (IsDecimalType(t))
            {
                NumberSpinner.contentType = InputField.ContentType.DecimalNumber;
            }
            else
            {
                throw new ArgumentException("Type must be one of expected types", "t");
            }

            var rangeAttrib = Property.GetAttribute<NumberRangeAttribute>();

            NumberSpinner.MaxValue = GetMaxValue(t);
            NumberSpinner.MinValue = GetMinValue(t);
            NumberSpinner.ExactInt64 = t == typeof(long);
            if (NumberSpinner.ExactInt64) NumberSpinner.characterLimit = 20;

            if (rangeAttrib != null)
            {
                NumberSpinner.MaxValue = Math.Min(rangeAttrib.Max, NumberSpinner.MaxValue);
                NumberSpinner.MinValue = Math.Max(rangeAttrib.Min, NumberSpinner.MinValue);
            }

            var incrementAttribute = Property.GetAttribute<IncrementAttribute>();

            if (incrementAttribute != null)
            {
                if (UpNumberButton != null)
                {
                    UpNumberButton.Amount = incrementAttribute.Increment;
                }

                if (DownNumberButton != null)
                {
                    DownNumberButton.Amount = -incrementAttribute.Increment;
                }
            }

            _type = t;

            NumberSpinner.interactable = !IsReadOnly;

            if (DisableOnReadOnly != null)
            {
                foreach (var childControl in DisableOnReadOnly)
                {
                    childControl.SetActive(!IsReadOnly);
                }
            }
        }

        protected override void OnValueUpdated(object newValue)
        {
            var value = Convert.ToDecimal(newValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            if (value != _lastValue)
            {
                NumberSpinner.text = value;
            }

            if(EmphasizeReadOnly)NumberSpinner.textComponent.fontSize=value.Length>14?14:24;
            _lastValue = value;
        }

        public override bool CanBind(Type type, bool isReadOnly)
        {
            return IsDecimalType(type) || IsIntegerType(type);
        }

        protected static bool IsIntegerType(Type t)
        {
            for (var i = 0; i < IntegerTypes.Length; i++)
            {
                if (IntegerTypes[i] == t)
                {
                    return true;
                }
            }

            return false;
        }

        protected static bool IsDecimalType(Type t)
        {
            for (var i = 0; i < DecimalTypes.Length; i++)
            {
                if (DecimalTypes[i] == t)
                {
                    return true;
                }
            }

            return false;
        }

        protected double GetMaxValue(Type t)
        {
            ValueRange value;
            if (ValueRanges.TryGetValue(t, out value))
            {
                return value.MaxValue;
            }

            Debug.LogWarning("[NumberControl] No MaxValue stored for type {0}".Fmt(t));

            return double.MaxValue;
        }

        protected double GetMinValue(Type t)
        {
            ValueRange value;
            if (ValueRanges.TryGetValue(t, out value))
            {
                return value.MinValue;
            }

            Debug.LogWarning("[NumberControl] No MinValue stored for type {0}".Fmt(t));

            return double.MinValue;
        }

        public struct ValueRange
        {
            public double MaxValue;
            public double MinValue;
        }
    }
}
