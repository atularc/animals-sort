using TMPro;
using UnityEngine;

namespace DanglePals
{
    /// <summary>Lookup facade over the scene-referenced <see cref="GameArt"/> library. Nothing is loaded from disk at runtime.</summary>
    public static class Res
    {
        public static GameArt Art;

        public static Sprite Sprite(string name) => Art.Sprite(name);
        public static Sprite Pal(string id, string mood) => Art.Pal(id, mood);
        public static AudioClip Clip(string name) => Art.Clip(name);
        public static TMP_FontAsset Font(bool bold) => bold ? Art.fontBold : Art.fontSemiBold;
        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    }
}
