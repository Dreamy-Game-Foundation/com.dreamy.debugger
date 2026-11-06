using System;
using Dreamy.Debugger.Presentation;
using UnityEngine;
using UnityEngine.UI;
namespace Dreamy.Debugger {
    public sealed class ConfirmationPrompt : MonoBehaviour {
        public Text Title,Details;public Button Confirm;public Button Cancel;
        private bool decided;
        private void OnEnable(){AccessGate.DismissModal=Dismiss;}
        private void OnDisable(){if(AccessGate.DismissModal==Dismiss)AccessGate.DismissModal=null;}
        private bool Dismiss(){if(decided)return false;decided=true;Destroy(gameObject);return true;}
        private void Update(){if(AccessGate.IsUnlocked!=null && !AccessGate.Authorized)Dismiss();}
        public void Configure(string title,Action accepted,Func<bool> validate=null){
            PresentationAssets.ApplyModalSkin(gameObject);
            Title.fontSize=20;Title.fontStyle=FontStyle.Bold;
            if(Details!=null){Details.color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Muted;Details.fontSize=14;}
            if(Confirm.targetGraphic is Image primary)primary.color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Accent;
            if(Cancel.targetGraphic is Image secondary)secondary.color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Control;
            Title.text=title+"?";
            Confirm.onClick.AddListener(()=>{
                if(decided)return;
                try {
                    if(AccessGate.IsUnlocked!=null && !AccessGate.Authorized){Dismiss();return;}
                    if(validate!=null && !validate()){Title.text="Values changed";if(Details!=null)Details.text="Close and review the action before confirming again.";Confirm.interactable=false;return;}
                    decided=true;accepted();Destroy(gameObject);
                } catch(Exception error){decided=false;Title.text="Action failed";if(Details!=null)Details.text=error.GetType().Name+". Review the game state before retrying.";Confirm.interactable=false;Debug.LogException(error);}
            });
            Cancel.onClick.AddListener(()=>Dismiss());
        }
    }
}
