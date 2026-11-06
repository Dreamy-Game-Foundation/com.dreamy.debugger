namespace Dreamy.Debugger.Presentation.UI.Controls
{
    using System;
    using UnityEngine;
    using Dreamy.Debugger.Foundation.Helpers;

    public abstract class DataBoundControl : OptionsControlBase
    {
        private bool _hasStarted;
        private bool subscribed;
        private void Subscribe(){if(!subscribed && _prop!=null){_prop.ValueChanged+=OnValueChanged;subscribed=true;}}
        private void Unsubscribe(){if(subscribed && _prop!=null)_prop.ValueChanged-=OnValueChanged;subscribed=false;}
        private bool refreshing;
        private UnityEngine.GameObject errorBadge;
        private UnityEngine.CanvasGroup errorGroup;
        private bool previousInteractable;
        public string BindingError { get; private set; }
        private void MarkBindingError(Exception error)
        {
            if (BindingError == null) {
                errorGroup = GetComponent<UnityEngine.CanvasGroup>();
                if (errorGroup == null) errorGroup = gameObject.AddComponent<UnityEngine.CanvasGroup>();
                previousInteractable = errorGroup.interactable;
            }
            BindingError = error.GetType().Name;
            errorGroup.interactable = false;
            if (errorBadge == null) {
                errorBadge = new GameObject("Binding unavailable", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                errorBadge.layer = gameObject.layer; errorBadge.transform.SetParent(transform, false);
                var rect = (RectTransform)errorBadge.transform;
                rect.anchorMin = new Vector2(.45f, 0); rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                var bg=errorBadge.GetComponent<UnityEngine.UI.Image>();bg.color=new Color(.2f,.12f,.12f,1);bg.raycastTarget=false;
                var textObject=new GameObject("Message",typeof(RectTransform),typeof(UnityEngine.UI.Text));textObject.layer=gameObject.layer;textObject.transform.SetParent(rect,false);
                var text=textObject.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text="Unavailable";text.fontSize=12;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
                var tr=(RectTransform)textObject.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
            }
            errorBadge.SetActive(true);
        }
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
            Unsubscribe();
            PropertyName = propertyName;
            _prop = prop;

            _isReadOnly = !prop.CanWrite;

            if(isActiveAndEnabled)Subscribe();

            OnBind(propertyName, prop.PropertyType);
            Refresh();
        }

        private void OnValueChanged(PropertyReference property)
        {
            Refresh();
        }

        protected void UpdateValue(object newValue)
        {
            if(refreshing || BindingError!=null)return;
            if(AccessGate.IsUnlocked!=null && !AccessGate.Authorized)return;
            if (Equals(newValue, _prevValue))
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

            object currentValue;
            try { currentValue = _prop.GetValue(); }
            catch (Exception error) { MarkBindingError(error); return; }
            var recovering = BindingError != null;
            BindingError = null;
            if (errorBadge != null) errorBadge.SetActive(false);
            if (recovering && errorGroup != null) errorGroup.interactable = previousInteractable;
            refreshing = true;
            try {
                if (!Equals(currentValue, _prevValue) || recovering) OnValueUpdated(currentValue);
                _prevValue = currentValue;
            } catch (Exception error) { MarkBindingError(error); }
            finally { refreshing = false; }
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

            Subscribe();
            if (_hasStarted) Refresh();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Unsubscribe();
        }

        #endregion
    }
}
