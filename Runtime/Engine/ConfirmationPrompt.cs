using System;
using UnityEngine;
using UnityEngine.UI;
namespace Dreamy.Debugger {
    public sealed class ConfirmationPrompt : MonoBehaviour {
        public Text Title;public Button Confirm;public Button Cancel;
        public void Configure(string title,Action accepted){Title.text=title;Confirm.onClick.AddListener(()=>{accepted();Destroy(gameObject);});Cancel.onClick.AddListener(()=>Destroy(gameObject));}
    }
}
