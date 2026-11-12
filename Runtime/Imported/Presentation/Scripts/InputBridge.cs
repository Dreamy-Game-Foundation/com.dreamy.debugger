using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Dreamy.Debugger.Presentation
{
    public static class InputBridge
    {
        public static Func<KeyCode,bool> Pressed;
        public static Func<KeyCode,bool> Held;
        public static Func<string> Text;
        public static Func<int> TouchCount;
        public static Func<Vector2?> PrimaryPointer;
        public static Action<GameObject> ConfigureEventSystem;
        public static bool Down(KeyCode key) {
            if(Pressed!=null)return Pressed(key);
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }
        public static bool IsHeld(KeyCode key) {
            if(Held!=null)return Held(key);
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }
        public static string ReadText() {
            if(Text!=null)return Text() ?? "";
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.inputString;
#else
            return "";
#endif
        }
        public static int ActiveTouchCount() {
            if(TouchCount!=null)return TouchCount();
#if ENABLE_LEGACY_INPUT_MANAGER
            var count=0;
            for(var i=0;i<Input.touchCount;i++){
                var phase=Input.GetTouch(i).phase;
                if(phase!=TouchPhase.Ended && phase!=TouchPhase.Canceled)count++;
            }
            return count;
#else
            return 0;
#endif
        }
        public static Vector2? HeldPointerPosition() {
            if(PrimaryPointer!=null)return PrimaryPointer();
#if ENABLE_LEGACY_INPUT_MANAGER
            for(var i=0;i<Input.touchCount;i++){
                var touch=Input.GetTouch(i);
                if(touch.phase!=TouchPhase.Ended && touch.phase!=TouchPhase.Canceled)return touch.position;
            }
            if(Input.GetMouseButton(0))return (Vector2)Input.mousePosition;
#endif
            return null;
        }
        public static void AddModule(GameObject go) {
            if(ConfigureEventSystem!=null){ConfigureEventSystem(go);return;}
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
