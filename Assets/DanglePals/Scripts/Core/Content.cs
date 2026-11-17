using System.Collections.Generic;
using UnityEngine;

namespace DanglePals
{
    public sealed class PalDef
    {
        public string Id, Name;
        public Color Col;      // body colour, used for trails, dots and particles
        public Color Accent;   // a visible tint for very pale pals
        public PalDef(string id, string name, string hex, string accentHex = null)
        {
            Id = id; Name = name;
            ColorUtility.TryParseHtmlString(hex, out Col);
            ColorUtility.TryParseHtmlString(accentHex ?? hex, out Accent);
        }
    }

    public sealed class WorldDef
    {
        public string Id, Name, Tagline;
        public int StartLevel, Theme;
        public string[] NewPals;
        public Color SkyTop, Shelf;
        public WorldDef(string id, string name, string tagline, int start, int theme, string skyTop, string shelf, params string[] pals)
        {
            Id = id; Name = name; Tagline = tagline; StartLevel = start; Theme = theme; NewPals = pals;
            ColorUtility.TryParseHtmlString(skyTop, out SkyTop);
            ColorUtility.TryParseHtmlString(shelf, out Shelf);
        }
    }

    public static class Content
    {
        public static readonly PalDef[] Pals =
        {
            new PalDef("fox", "Fox", "#F7893B"),
            new PalDef("bunny", "Bunny", "#FFF4F1", "#FFB3CC"),
            new PalDef("bear", "Bear Cub", "#B97A4E"),
            new PalDef("raccoon", "Raccoon", "#A7AEBA"),
            new PalDef("pig", "Piglet", "#FFB1C2"),
            new PalDef("chick", "Chick", "#FFD84A"),
            new PalDef("penguin", "Penguin", "#3E5079"),
            new PalDef("panda", "Panda", "#FFFFFF", "#9AA3B5"),
            new PalDef("frog", "Tree Frog", "#8DD35C"),
            new PalDef("kitty", "Kitten", "#BCA4EF"),
        };

        // Each world unlocks by playing: a new backdrop and new pals join the pool.
        public static readonly WorldDef[] Worlds =
        {
            new WorldDef("meadow", "Sunny Meadow", "Where every pal's story begins.", 1, 0, "#8FD3F4", "#DDBB86", "fox", "bunny", "bear", "raccoon"),
            new WorldDef("farm", "Golden Farm", "Warm sunsets and hay-scented breezes.", 16, 1, "#FFB99A", "#DDBB86", "pig", "chick"),
            new WorldDef("snow", "Snowy Peaks", "Fresh snow and cosy igloo nights.", 36, 2, "#9FC9EE", "#BFD6EA", "penguin", "panda"),
            new WorldDef("pond", "Lily Pond", "Lily pads, reeds and dragonflies.", 61, 3, "#7FD6CF", "#C9B98E", "frog"),
            new WorldDef("night", "Moonlit Garden", "Fireflies glow when the moon is up.", 91, 4, "#1E2A5A", "#6E5A86", "kitty"),
            new WorldDef("candy", "Candy Clouds", "Sweet skies for expert sorters.", 126, 5, "#FFC6E0", "#F1B7D2"),
        };

        static Dictionary<string, PalDef> _byId;
        public static PalDef Pal(string id)
        {
            if (_byId == null) { _byId = new Dictionary<string, PalDef>(); foreach (var p in Pals) _byId[p.Id] = p; }
            return _byId[id];
        }

        // After the six hand-made worlds, scenery keeps unlocking forever: every 40 levels a revisited
        // world with a new number (Sunny Meadow II, Golden Farm II, ...), so there is always a next scene.
        const int EndlessLength = 40;
        static readonly Dictionary<int, WorldDef> Endless = new Dictionary<int, WorldDef>();
        static readonly string[] Numerals = { "", "", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

        public static WorldDef World(int index)
        {
            if (index < Worlds.Length) return Worlds[index];
            if (Endless.TryGetValue(index, out var w)) return w;
            var b = Worlds[index % Worlds.Length];
            int round = index / Worlds.Length + 1;
            string num = round < Numerals.Length ? Numerals[round] : round.ToString();
            int start = Worlds[Worlds.Length - 1].StartLevel + (index - (Worlds.Length - 1)) * EndlessLength;
            w = new WorldDef(b.Id + round, b.Name + " " + num, "A brand-new season of puzzles.", start, b.Theme,
                "#" + ColorUtility.ToHtmlStringRGB(b.SkyTop), "#" + ColorUtility.ToHtmlStringRGB(b.Shelf));
            Endless[index] = w;
            return w;
        }

        public static int WorldIndexForLevel(int level)
        {
            var last = Worlds[Worlds.Length - 1];
            if (level >= last.StartLevel) return Worlds.Length - 1 + (level - last.StartLevel) / EndlessLength;
            int w = 0;
            for (int i = 0; i < Worlds.Length; i++) if (level >= Worlds[i].StartLevel) w = i;
            return w;
        }
        public static WorldDef WorldForLevel(int level) => World(WorldIndexForLevel(level));
        public static int WorldLength(int index) => World(index + 1).StartLevel - World(index).StartLevel;

        /// <summary>Pals unlocked by this level, oldest first.</summary>
        public static List<string> UnlockedPals(int level)
        {
            var list = new List<string>();
            foreach (var w in Worlds) if (level >= w.StartLevel) list.AddRange(w.NewPals);
            return list;
        }
    }
}
