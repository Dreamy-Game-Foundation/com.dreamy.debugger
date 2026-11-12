using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using Dreamy.Debugger.Presentation;
namespace Dreamy.Debugger.InputSystemSupport {
    internal static class InputSystemAdapter {
        private static InputAction tapAction;
        private static Vector2 tapOrigin;
        private static Vector2 PointerPosition(InputDevice device) {
            var touch=device as Touchscreen;
            if(touch!=null)return touch.primaryTouch.position.ReadValue();
            var pointer=device as Pointer;
            return pointer!=null?pointer.position.ReadValue():Vector2.zero;
        }
        private static void DisposeTap() { tapAction?.Dispose();tapAction=null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() {
            DisposeTap();
            tapAction=new InputAction("Debugger pointer tap",InputActionType.Button,interactions:"tap(duration=0.5)");
            tapAction.AddBinding("<Mouse>/leftButton");
            tapAction.AddBinding("<Touchscreen>/primaryTouch/press");
            tapAction.started+=context=>tapOrigin=PointerPosition(context.control.device);
            tapAction.performed+=context=>InputBridge.ReportPointerTap(tapOrigin,PointerPosition(context.control.device));
            tapAction.Enable();
            Application.quitting-=DisposeTap;Application.quitting+=DisposeTap;
            InputBridge.Pressed=key=>Control(key)?.wasPressedThisFrame ?? false;
            InputBridge.Held=key=>Control(key)?.isPressed ?? false;
            InputBridge.ConfigureEventSystem=go=>go.AddComponent<InputSystemUIInputModule>();
            // Keypad itself supports touch; physical PIN input is handled by key polling without subscriptions.
            InputBridge.Text=ReadDigits;
            InputBridge.TouchCount=()=>{
                var screen=Touchscreen.current;if(screen==null)return 0;
                var count=0;foreach(var touch in screen.touches)if(touch.press.isPressed)count++;
                return count;
            };
            InputBridge.PrimaryPointer=()=>{
                var screen=Touchscreen.current;
                if(screen!=null)foreach(var touch in screen.touches)if(touch.press.isPressed)return (Vector2?)touch.position.ReadValue();
                var mouse=Mouse.current;
                return mouse!=null && mouse.leftButton.isPressed?(Vector2?)mouse.position.ReadValue():null;
            };
        }
        private static readonly Key[] Digits={Key.Digit0,Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8,Key.Digit9};
        private static string ReadDigits() {
            var keyboard=Keyboard.current;if(keyboard==null)return "";
            for(var n=0;n<10;n++)if(keyboard[Digits[n]].wasPressedThisFrame || keyboard[(Key)((int)Key.Numpad0+n)].wasPressedThisFrame)return n.ToString();
            return "";
        }
        private static KeyControl Control(KeyCode code) {
            var k=Keyboard.current;if(k==null)return null;
            switch(code){
                case KeyCode.LeftControl:return k.leftCtrlKey;case KeyCode.RightControl:return k.rightCtrlKey;
                case KeyCode.LeftShift:return k.leftShiftKey;case KeyCode.RightShift:return k.rightShiftKey;
                case KeyCode.LeftAlt:return k.leftAltKey;case KeyCode.RightAlt:return k.rightAltKey;
            }
            if(Enum.TryParse<Key>(code.ToString(),true,out var key) && key!=Key.None)return k[key];
            return null;
        }
    }
}
