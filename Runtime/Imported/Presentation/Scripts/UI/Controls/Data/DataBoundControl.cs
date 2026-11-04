namespace Dreamy.Debugger.Presentation.UI.Controls
{
    using System;
    using UnityEngine;
    using Dreamy.Debugger.Foundation.Helpers;

    public abstract class DataBoundControl : OptionsControlBase
    {
        private bool _hasStarted;
        private bool _isReadOnly;
        private object _prevValue;
        private Dreamy.Debugger.Foundation.Helpers.PropertyReference _prop;

        public Dreamy.Debugger.Foundation.Helpers.PropertyReference Property
        {
            get { return _prop; }
        }

        public bool IsReadOnly
        {
            get { return _isReadOnly; }
        }

        public string PropertyName { get; private set; }

        #region Data Binding

        public void Bind(string propertyName, Dreamy.Debugger.Foundation.Helpers.PropertyReference prop)
        {
            PropertyName = propertyName;
            _prop = prop;

            _isReadOnly = !prop.CanWrite;

            prop.ValueChanged += OnValueChanged;

            OnBind(propertyName, prop.PropertyType);
            Refresh();
        }

        private void OnValueChanged(PropertyReference property)
        {
            Refresh();
        }

        protected void UpdateValue(object newValue)
        {
            if(AccessGate.IsUnlocked!=null && !AccessGate.Authorized)return;
            if (newValue == _prevValue)
            {
                return;
            }

            if (IsReadOnly)
            {
                return;
            }

            _prop.SetValue(newValue);
            _prevValue = newValue;
        }

        public override void Refresh()
        {
            if(AccessGate.IsUnlocked!=null && !AccessGate.Authorized)return;
            if (_prop == null)
            {
                return;
            }

            var currentValue = _prop.GetValue();

            if (currentValue != _prevValue)
            {
                try
                {
                    OnValueUpdated(currentValue);
                }
                catch (Exception e)
                {
                    Debug.LogError("[DebugOptions] Error refreshing binding.");
                    Debug.LogException(e);
                }
            }

            _prevValue = currentValue;
        }

        protected virtual void OnBind(string propertyName, Type t) {}
        protected abstract void OnValueUpdated(object newValue);

        public abstract bool CanBind(Type type, bool isReadOnly);

        #endregion

        #region Unity

        protected override void Start()
        {
            base.Start();

            Refresh();
            _hasStarted = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_hasStarted)
            {
                if (_prop != null)
                {
                    _prop.ValueChanged += OnValueChanged;
                }

                Refresh();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_prop != null)
            {
                _prop.ValueChanged -= OnValueChanged;
            }
        }

        #endregion
    }
}
