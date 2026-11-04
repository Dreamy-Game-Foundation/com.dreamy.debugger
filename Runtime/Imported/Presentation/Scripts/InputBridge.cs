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
        public static void AddModule(GameObject go) {
            if(ConfigureEventSystem!=null){ConfigureEventSystem(go);return;}
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
