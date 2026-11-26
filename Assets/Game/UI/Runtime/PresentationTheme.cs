using UnityEngine;
using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    public static class PresentationTheme
    {
        public static readonly Color32 Background = new(37, 35, 32, 255);
        public static readonly Color32 Wood = new(89, 67, 51, 255);
        public static readonly Color32 Stone = new(119, 115, 108, 255);
        public static readonly Color32 Parchment = new(216, 199, 165, 255);
        public static readonly Color32 PrimaryText = new(242, 235, 219, 255);
        public static readonly Color32 SecondaryText = new(181, 173, 160, 255);
        public static readonly Color32 Accent = new(197, 162, 104, 255);
        public static readonly Color32 Cooperation = new(100, 137, 166, 255);
        public static readonly Color32 Competition = new(166, 75, 69, 255);
        public static readonly Color32 Warning = new(210, 138, 66, 255);
        public static readonly Color32 Success = new(113, 148, 108, 255);

        public const int TitleSize = 44;
        public const int HeadingSize = 32;
        public const int BodySize = 26;
        public const int CaptionSize = 22;

        internal static ColorBlock Controls(bool field)
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = field ? new Color32(53, 47, 40, 255) : Wood;
            colors.highlightedColor = new Color32(109, 83, 61, 255);
            colors.pressedColor = new Color32(59, 44, 35, 255);
            colors.selectedColor = new Color32(100, 74, 50, 255);
            colors.disabledColor = new Color32(57, 53, 48, 255);
            colors.fadeDuration = .08f;
            return colors;
        }
    }
}
