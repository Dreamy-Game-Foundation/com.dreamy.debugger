using UnityEngine;
namespace Dreamy.Debugger {
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour {
        private Rect previous;private Vector2 resolution;
        private void OnEnable()=>Apply();
        private void Update(){if(previous!=Screen.safeArea || resolution.x!=Screen.width || resolution.y!=Screen.height)Apply();}
        private void Apply(){if(Screen.width<=0 || Screen.height<=0)return;previous=Screen.safeArea;resolution=new Vector2(Screen.width,Screen.height);var t=(RectTransform)transform;t.anchorMin=new Vector2(previous.xMin/Screen.width,previous.yMin/Screen.height);t.anchorMax=new Vector2(previous.xMax/Screen.width,previous.yMax/Screen.height);t.offsetMin=t.offsetMax=Vector2.zero;}
    }
}
