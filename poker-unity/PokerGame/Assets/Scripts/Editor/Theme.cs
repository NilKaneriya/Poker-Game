using UnityEngine;

namespace Poker.EditorTools
{
    /// <summary>Colours shared by all scenes.</summary>
    public static class Theme
    {
        public static readonly Color Background = Hex("0E1726");
        public static readonly Color Panel = Hex("1B2740");
        public static readonly Color PanelLight = Hex("26355A");
        public static readonly Color Input = Hex("111B2E");
        public static readonly Color Text = Hex("F3F6FB");
        public static readonly Color Muted = Hex("94A3B8");
        public static readonly Color Gold = Hex("F6C744");
        public static readonly Color Green = Hex("22C55E");
        public static readonly Color Blue = Hex("3B82F6");
        public static readonly Color Red = Hex("E5484D");
        public static readonly Color Orange = Hex("F59E0B");
        public static readonly Color CardBack = Hex("2F4CB3");
        public static readonly Color Dark = Hex("0B1220");

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
