using UnityEngine;
namespace Dreamy.Debugger {
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour {
        private Rect previous;private Vector2 resolution;
        private void OnEnable()=>Apply();
        private void Update(){if(previous!=Screen.safeArea || resolution.x!=Screen.width || resolution.y!=Screen.height)Apply();}
        private void Apply()
        {
            var width=Screen.width;var height=Screen.height;
            if(width<=0 || height<=0)return;
            previous=Screen.safeArea;resolution=new Vector2(width,height);
            var t=(RectTransform)transform;
            // Simulator/Game View render callbacks can report a safe rect from another
            // viewport. Anchors must remain inside this canvas even during that transition.
            t.anchorMin=new Vector2(Mathf.Clamp01(previous.xMin/width),Mathf.Clamp01(previous.yMin/height));
            t.anchorMax=new Vector2(Mathf.Clamp01(previous.xMax/width),Mathf.Clamp01(previous.yMax/height));
            t.offsetMin=t.offsetMax=Vector2.zero;
        }
    }
}
