using UnityEngine;
namespace Dreamy.Debugger.Foundation.UI {
    /// <summary>Shared view colors; contains no settings, services or gameplay state.</summary>
    public static class DebuggerPalette {
        public static readonly Color Background = Hex(0x101216);
        public static readonly Color Surface = Hex(0x1C1F25);
        public static readonly Color Control = Hex(0x272C34);
        public static readonly Color Border = Hex(0x363E49);
        public static readonly Color Ivory = Hex(0xEEF0F4);
        public static readonly Color Muted = Hex(0x99A3B2);
        public static readonly Color Brand = Hex(0x1C1F25);
        public static readonly Color Accent = Hex(0xCB465D);
        public static readonly Color Selected = Hex(0x39212C);
        public static readonly Color FpsSeries = Hex(0x60C8AA);
        public static readonly Color MemorySeries = Hex(0xC878D8);
        public static readonly Color Warning = Hex(0xE7B65A);
        public static Color Hex(uint value) => new Color(((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f);
    }
}
