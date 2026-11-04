namespace Dreamy.Debugger.Presentation.UI.Controls.Data
{
    using System;
    using Dreamy.Debugger.Foundation;
    using UnityEngine;
    using UnityEngine.UI;

    public class ActionControl : OptionsControlBase
    {
        private Dreamy.Debugger.Foundation.Helpers.MethodReference _method;

        [RequiredField] public UnityEngine.UI.Button Button;

        [RequiredField] public Text Title;

        public Dreamy.Debugger.Foundation.Helpers.MethodReference Method
        {
            get { return _method; }
        }

        protected override void Start()
        {
            base.Start();
            Button.onClick.AddListener(ButtonOnClick);
        }

        private void ButtonOnClick()
        {
            if(AccessGate.IsUnlocked!=null && !AccessGate.Authorized)return;
            if (_method == null)
            {
                Debug.LogWarning("[DreamyDebugger.Options] No method set for action control", this);
                return;
            }

            try
            {
                _method.Invoke(null);
            }
            catch (Exception e)
            {
                Debug.LogError("[DreamyDebugger] Exception thrown while executing action.");
                Debug.LogException(e);
            }
        }

        public void SetMethod(string methodName, Dreamy.Debugger.Foundation.Helpers.MethodReference method)
        {
            _method = method;
            Title.text = methodName;
        }
    }
}
