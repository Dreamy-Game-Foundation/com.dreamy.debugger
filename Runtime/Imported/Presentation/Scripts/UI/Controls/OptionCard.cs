using System;
using System.Linq;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Foundation.Helpers;
using Dreamy.Debugger.Presentation.UI.Controls.Data;
using UnityEngine;
using UnityEngine.UI;
namespace Dreamy.Debugger.Presentation.UI.Controls
{
    /// <summary>Authored task-card slots hosting the standard bound controls. No gameplay authority.</summary>
    public sealed class OptionCard : MonoBehaviour
    {
        public Text Title, OwnedLabel;
        public RectTransform BalanceSlot, AmountSlot, ActionSlot, OwnedSlot;
        private PropertyReference owned;
        private ActionControl grant;
        private bool selectionMode;
        public string CardId { get; private set; }
        public void Configure(CheatCard metadata) { CardId = metadata.Id; Title.text = metadata.Title; }
        public void Attach(OptionsControlBase control)
        {
            var role = control.Option.Card.Role;
            var slot = role == CheatCardRole.Balance ? BalanceSlot : role == CheatCardRole.Amount ? AmountSlot : role == CheatCardRole.Owned ? OwnedSlot : ActionSlot;
            slot.gameObject.SetActive(true); control.transform.SetParent(slot, false);
            if (control is NumberControl number) {
                number.Title.text = role == CheatCardRole.Balance ? "Balance" : "Grant amount";
                if (role == CheatCardRole.Balance) {
                    number.EmphasizeReadOnly=true;number.NumberSpinner.textComponent.fontSize=number.NumberSpinner.text.Length>14?14:24;
                    var title=number.Title.rectTransform;title.anchorMin=Vector2.zero;title.anchorMax=new Vector2(.35f,1);title.offsetMin=new Vector2(8,0);title.offsetMax=Vector2.zero;
                    var input=(RectTransform)number.NumberSpinner.transform;input.anchorMin=new Vector2(.35f,0);input.anchorMax=Vector2.one;input.offsetMin=Vector2.zero;input.offsetMax=new Vector2(-8,0);
                } else {
                    var title=number.Title.rectTransform;title.anchorMin=new Vector2(0,1);title.anchorMax=Vector2.one;title.offsetMin=new Vector2(4,-22);title.offsetMax=new Vector2(-4,0);
                    var input=(RectTransform)number.NumberSpinner.transform;input.anchorMin=Vector2.zero;input.anchorMax=Vector2.one;input.offsetMin=new Vector2(48,0);input.offsetMax=new Vector2(-48,-24);
                    var down=(RectTransform)number.DownNumberButton.transform;down.anchorMin=Vector2.zero;down.anchorMax=new Vector2(0,1);down.offsetMin=Vector2.zero;down.offsetMax=new Vector2(46,-24);
                    var up=(RectTransform)number.UpNumberButton.transform;up.anchorMin=new Vector2(1,0);up.anchorMax=Vector2.one;up.offsetMin=new Vector2(-46,0);up.offsetMax=new Vector2(0,-24);
                }
            }
            if (role == CheatCardRole.Owned) {
                owned = control.Option.Property;
                if (control is BoolControl toggle) { toggle.Title.text = "Ownership"; toggle.Toggle.gameObject.SetActive(false); }
                owned.ValueChanged += OnOwnedChanged;
                OwnedLabel.gameObject.SetActive(true);
            }
            if (control is ActionControl action) grant = action;
            RefreshOwned();
        }
        private void OnOwnedChanged(PropertyReference property) { RefreshOwned(); }
        public void SetSelectionMode(bool value) { selectionMode = value; RefreshOwned(); }
        private void RefreshOwned()
        {
            if (owned == null || AccessGate.IsUnlocked != null && !AccessGate.Authorized) return;
            try {
                var value = (bool)owned.GetValue();
                OwnedLabel.text = value ? "Owned" : "Not owned";
                OwnedLabel.color = value ? new Color(.6f,.85f,.65f) : Dreamy.Debugger.Foundation.UI.DebuggerPalette.Muted;
                if (grant != null) { grant.Button.interactable = !value; ActionSlot.gameObject.SetActive(!value || selectionMode); }
            } catch (Exception) { OwnedLabel.text = "Unavailable"; if (grant != null) grant.Button.interactable = false; }
        }
        private void OnEnable() { if (owned != null) { owned.ValueChanged += OnOwnedChanged; RefreshOwned(); } }
        private void OnDisable() { if (owned != null) owned.ValueChanged -= OnOwnedChanged; }
        private void OnDestroy() { if (owned != null) owned.ValueChanged -= OnOwnedChanged; }
    }
}
