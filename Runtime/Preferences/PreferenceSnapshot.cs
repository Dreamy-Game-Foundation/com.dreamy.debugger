using System;
using System.Collections.Generic;

namespace Dreamy.Debugger.Preferences
{
    [Serializable]
    public sealed class PreferenceEntry
    {
        public string key;
        public string value;
        public PreferenceEntry(string key, string value) { this.key = key; this.value = value; }
    }

    /// <summary>Explicit UI allowlist. Never serialize config, credentials, log text or game values.</summary>
    [Serializable]
    public sealed class PreferenceSnapshot
    {
        public int schema = 1;
        public PreferenceEntry[] entries = new PreferenceEntry[0];
        public const int MaximumBytes = 32768;
        private static readonly HashSet<string> Keys = new HashSet<string>(StringComparer.Ordinal)
        {
            "hud.mode", "hud.position", "hud.x", "hud.y", "hud.scale", "hud.memory", "hud.audio", "hud.drag",
            "fps.good", "fps.caution", "fps.good-color", "fps.caution-color", "fps.critical-color",
            "fps.warning", "fps.threshold", "fps.sustain", "fps.cooldown",
            "console.log", "console.warning", "console.error", "console.collapse"
        };
        public static bool Allowed(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 160 &&
            (Keys.Contains(key) || key.StartsWith("pin.", StringComparison.Ordinal) || key.StartsWith("category.", StringComparison.Ordinal));

        public Dictionary<string,string> ValidEntries()
        {
            var result = new Dictionary<string,string>(StringComparer.Ordinal);
            if (schema != 1 || entries == null) return result;
            foreach (var entry in entries)
            {
                if (result.Count >= 160) break;
                if (entry != null && Allowed(entry.key) && entry.value != null && entry.value.Length <= 256)
                    result[entry.key] = entry.value;
            }
            return result;
        }
    }
}
