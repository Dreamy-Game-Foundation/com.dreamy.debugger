namespace Dreamy.Debugger.Presentation.Services.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Internal;
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.Service;
    using UI.Controls;
    using UnityEngine;

    [Service(typeof (IPinEntryService))]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Dreamy.Debugger.Presentation.Services.Implementation", sourceAssembly: null, sourceClassName: "PinEntryServiceImpl")]
    public class DreamyPinEntryService : ServiceBase<IPinEntryService>, IPinEntryService
    {
        private PinEntryCompleteCallback _callback;
        private bool _isVisible;
        private PinEntryControl _pinControl;
        private readonly List<int> _requiredPin = new List<int>(4);

        public bool IsShowingKeypad
        {
            get { return _isVisible; }
        }

        public void CancelPinEntry()
        {
            if (!_isVisible) return;
            _isVisible = false;
            if (_pinControl != null) { _pinControl.Hide(); _pinControl.Clear(); }
            var callback = _callback;
            _callback = null;
            callback?.Invoke(false);
        }

        public void ShowPinEntry(IReadOnlyList<int> requiredPin, string message, PinEntryCompleteCallback callback,
            bool allowCancel = true)
        {
            if (_isVisible)
            {
                Debug.LogWarning("[PinEntry] Pin entry is already in progress, ignoring duplicate request");
                return;
            }

            if(AccessGate.BeginPrompt != null && !AccessGate.BeginPrompt()){ callback(false);return; }
            VerifyPin(requiredPin);

            if (_pinControl == null)
            {
                Load();
            }

            if (_pinControl == null)
            {
                Debug.LogWarning("[PinEntry] Pin entry failed loading, executing callback with fail result");
                callback(false);
                return;
            }

            _pinControl.ConfigureLength(requiredPin.Count);
            _pinControl.SetPassphraseMode(AccessGate.Passphrase);
            _pinControl.PromptText.text = message;

            _pinControl.CanCancel = allowCancel;

            _callback = callback;

            _requiredPin.Clear();
            _requiredPin.AddRange(requiredPin);

            _pinControl.Show();

            _isVisible = true;

            PresentationUtility.EnsureEventSystemExists();
        }
        
        protected override void Awake()
        {
            base.Awake();

            CachedTransform.SetParent(Hierarchy.Get("DreamyDebugger"));
        }

        private void Load()
        {
            var prefab = PresentationAssets.Load<PinEntryControl>(PanelPaths.PinEntryPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("[PinEntry] Unable to load pin entry prefab");
                return;
            }

            _pinControl = ObjectFactory.Instantiate(prefab);
            _pinControl.CachedTransform.SetParent(CachedTransform, false);

            _pinControl.Hide();

            _pinControl.Complete += PinControlOnComplete;
            _pinControl.CredentialSubmitted += value=>CredentialResult(value);
        }

        private void PinControlOnComplete(IList<int> result, bool didCancel)
        {
            if(AccessGate.Validate!=null && !didCancel){CredentialResult(string.Concat(result));return;}
            var isValid = _requiredPin.SequenceEqual(result);

            if (!didCancel && !isValid)
            {
                _pinControl.Clear();
                _pinControl.PlayInvalidCodeAnimation();

                return;
            }

            _isVisible = false;
            _pinControl.Hide();

            if (didCancel)
            {
                _callback(false);
                return;
            }

            _callback(isValid);
        }

        private void CredentialResult(string value) {
            var result=AccessGate.Validate(value);
            if(result==Dreamy.Debugger.Access.UnlockResult.Unlocked){_isVisible=false;_pinControl.Hide();_callback(true);return;}
            _pinControl.Clear();_pinControl.PlayInvalidCodeAnimation();
            if(result==Dreamy.Debugger.Access.UnlockResult.Cooldown){_isVisible=false;_pinControl.Hide();_callback(false);}
        }
        private void VerifyPin(IReadOnlyList<int> pin)
        {
            if (pin.Count < 4 || pin.Count > 32)
            {
                throw new ArgumentException("PIN requires 4–32 digits");
            }

            for (var i = 0; i < pin.Count; i++)
            {
                if (pin[i] < 0 || pin[i] > 9)
                {
                    throw new ArgumentException("Pin numbers must be >= 0 && <= 9");
                }
            }
        }
    }
}
