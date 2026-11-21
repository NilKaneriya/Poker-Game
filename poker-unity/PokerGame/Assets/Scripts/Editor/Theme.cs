using UnityEngine;
using static Poker.Core.Constants;

namespace Poker.EditorTools
{
    /// <summary>Colours used to build the scenes. Colours the game code also uses come from Constants.UIColors.</summary>
    public static class Theme
    {
        public static readonly Color Background = Hex("0E1726");
        public static readonly Color Panel = Hex("1B2740");
        public static readonly Color PanelLight = UIColors.PanelLight;
        public static readonly Color Input = Hex("111B2E");
        public static readonly Color Text = UIColors.Text;
        public static readonly Color Muted = Hex("94A3B8");
        public static readonly Color Gold = UIColors.Gold;
        public static readonly Color Green = UIColors.Green;
        public static readonly Color Blue = UIColors.Blue;
        public static readonly Color Red = Hex("E5484D");
        public static readonly Color Orange = Hex("F59E0B");
        public static readonly Color CardBack = Hex("2F4CB3");
        public static readonly Color Dark = UIColors.Dark;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
