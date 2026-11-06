using System;

namespace Dreamy.Debugger.Presentation
{
    /// <summary>UI-only preferences adapter; no dependency from presentation to Engine.</summary>
    public static class PreferenceBridge
    {
        public static Func<string,string,string> Read;
        public static Action<string,string> Write;
        public static event Action Restored;
        public static string Get(string key, string fallback) => Read?.Invoke(key, fallback) ?? fallback;
        public static void Set(string key, string value) => Write?.Invoke(key, value);
        public static void NotifyRestored() => Restored?.Invoke();
        public static void Clear() { Read = null; Write = null; Restored = null; }
    }
}
