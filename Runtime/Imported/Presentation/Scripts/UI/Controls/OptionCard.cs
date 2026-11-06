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
            if (control is NumberControl number)
            {
                foreach(var style in number.GetComponentsInChildren<Dreamy.Debugger.Foundation.UI.StyleComponent>(true))style.enabled=false;
                foreach(var image in number.GetComponentsInChildren<Image>(true)){image.sprite=PresentationAssets.Load<Sprite>("DreamyDebugger/Theme/Panel");image.type=Image.Type.Sliced;image.color=Color.clear;}
                number.Title.text=role==CheatCardRole.Balance?"Balance":"Amount";
                number.Title.fontSize=14;number.Title.color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Muted;
                number.EmphasizeReadOnly=role==CheatCardRole.Balance;
                var title=number.Title.rectTransform;title.anchorMin=Vector2.zero;title.anchorMax=new Vector2(0,1);title.pivot=new Vector2(0,.5f);title.offsetMin=new Vector2(0,0);title.offsetMax=new Vector2(70,0);
                var input=(RectTransform)number.NumberSpinner.transform;input.anchorMin=Vector2.zero;input.anchorMax=Vector2.one;input.offsetMin=new Vector2(76,0);input.offsetMax=new Vector2(-4,0);
                number.NumberSpinner.textComponent.fontSize=role==CheatCardRole.Balance?20:16;
                number.NumberSpinner.textComponent.alignment=role==CheatCardRole.Balance?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter;
                number.NumberSpinner.textComponent.resizeTextForBestFit=true;number.NumberSpinner.textComponent.resizeTextMinSize=12;number.NumberSpinner.textComponent.resizeTextMaxSize=role==CheatCardRole.Balance?20:16;
                number.UpNumberButton.gameObject.SetActive(false);number.DownNumberButton.gameObject.SetActive(false);
                if(role!=CheatCardRole.Balance){var background=number.NumberSpinner.GetComponent<Image>();if(background!=null)background.color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Background;}
                var colors=ColorBlock.defaultColorBlock;colors.normalColor=Color.white;colors.disabledColor=Color.white;number.NumberSpinner.colors=colors;
            }
            if (role == CheatCardRole.Owned) {
                owned = control.Option.Property;
                if (control is BoolControl toggle) { toggle.Title.text = "Ownership"; toggle.Toggle.gameObject.SetActive(false); }
                owned.ValueChanged += OnOwnedChanged;
                OwnedLabel.gameObject.SetActive(true);
            }
            if (control is ActionControl action)
            {
                grant = action;
                foreach(var style in action.GetComponentsInChildren<Dreamy.Debugger.Foundation.UI.StyleComponent>(true))style.enabled=false;
                foreach(var image in action.GetComponentsInChildren<Image>(true))image.color=Color.clear;
                if(action.Button.targetGraphic!=null)action.Button.targetGraphic.color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Accent;
                var colors=ColorBlock.defaultColorBlock;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.08f,1.08f,1.08f);colors.pressedColor=new Color(.8f,.8f,.8f);action.Button.colors=colors;
                action.Title.color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Ivory;action.Title.fontSize=16;
            }
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
