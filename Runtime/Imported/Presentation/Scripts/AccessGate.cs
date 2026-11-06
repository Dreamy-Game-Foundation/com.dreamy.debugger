using System;
using Dreamy.Debugger.Access;
namespace Dreamy.Debugger.Presentation {
    public static class AccessGate {
        public static Func<bool> IsUnlocked;
        public static Func<bool> BeginPrompt;
        public static Func<string,UnlockResult> Validate;
        public static bool Passphrase;
        public static Func<bool> DismissModal;
        public static bool Authorized=>IsUnlocked?.Invoke()??false;
        public static void Clear(){IsUnlocked=null;BeginPrompt=null;Validate=null;Passphrase=false;DismissModal=null;}
    }
}
