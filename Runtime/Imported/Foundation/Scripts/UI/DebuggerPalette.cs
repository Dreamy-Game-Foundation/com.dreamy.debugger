using UnityEngine;
namespace Dreamy.Debugger.Foundation.UI {
    /// <summary>Shared view colors; contains no settings, services or gameplay state.</summary>
    public static class DebuggerPalette {
        public static readonly Color Background = Hex(0x16171A);
        public static readonly Color Surface = Hex(0x202126);
        public static readonly Color Control = Hex(0x292B31);
        public static readonly Color Border = Hex(0x303238);
        public static readonly Color Ivory = Hex(0xF4EFEA);
        public static readonly Color Muted = Hex(0xA5A3A2);
        public static readonly Color Brand = Hex(0x9E1008);
        public static readonly Color Accent = Hex(0xE95345);
        public static readonly Color Selected = Hex(0x572A2A);
        public static readonly Color FpsSeries = Hex(0x60C8AA);
        public static readonly Color MemorySeries = Hex(0xC878D8);
        public static readonly Color Warning = Hex(0xE7B65A);
        public static Color Hex(uint value) => new Color(((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f);
    }
}
