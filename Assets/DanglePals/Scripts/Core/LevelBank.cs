using System;
using System.Collections.Generic;
using System.IO;

namespace DanglePals
{
    /// <summary>
    /// The baked level list: levels 1..Count were generated and verified solvable in the Editor
    /// ("Dangle Pals > Bake And Verify Levels") and ship inside the game as one small binary file.
    /// Past the end, levels come from the same deterministic generator, so the game never runs out.
    ///
    /// Format: "DPL1", int32 count, then per level:
    ///   byte species, byte capacity, byte ropes, uint16 par, byte tier, byte hard,
    ///   species x byte pal index (into Content.Pals), ropes x (byte length, length x byte species id).
    /// </summary>
    public static class LevelBank
    {
        static byte[] _data;
        static int[] _offsets;

        public static int Count => _offsets?.Length ?? 0;

        public static void Load(byte[] bytes)
        {
            _data = null; _offsets = null;
            if (bytes == null || bytes.Length < 8 || bytes[0] != 'D' || bytes[1] != 'P' || bytes[2] != 'L' || bytes[3] != '1') return;
            int count = BitConverter.ToInt32(bytes, 4);
            var offsets = new int[count];
            int p = 8;
            for (int i = 0; i < count; i++)
            {
                offsets[i] = p;
                int k = bytes[p], r = bytes[p + 2];
                p += 7 + k;
                for (int j = 0; j < r; j++) p += 1 + bytes[p];
            }
            _data = bytes; _offsets = offsets;
        }

        /// <summary>Level n (1-based). Baked when available, generated otherwise. Never null.</summary>
        public static LevelData Get(int n)
        {
            if (n < 1) n = 1;
            if (_offsets != null && n <= _offsets.Length) return Read(n, _offsets[n - 1]);
            return LevelGenerator.Generate(n);
        }

        static LevelData Read(int n, int p)
        {
            var b = _data;
            int k = b[p], c = b[p + 1], r = b[p + 2];
            var d = new LevelData
            {
                Number = n, Species = k, Capacity = c,
                Par = b[p + 3] | (b[p + 4] << 8), Tier = b[p + 5], Hard = b[p + 6] != 0,
                PalIds = new string[k], State = new int[r][],
            };
            p += 7;
            for (int i = 0; i < k; i++) d.PalIds[i] = Content.Pals[b[p++]].Id;
            for (int j = 0; j < r; j++)
            {
                int len = b[p++];
                var rope = new int[len];
                for (int i = 0; i < len; i++) rope[i] = b[p++];
                d.State[j] = rope;
            }
            return d;
        }

        /// <summary>Encodes levels in the format above (used by the Editor bake).</summary>
        public static byte[] Encode(IList<LevelData> levels)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((byte)'D'); w.Write((byte)'P'); w.Write((byte)'L'); w.Write((byte)'1');
            w.Write(levels.Count);
            foreach (var d in levels)
            {
                w.Write((byte)d.Species); w.Write((byte)d.Capacity); w.Write((byte)d.State.Length);
                w.Write((ushort)d.Par); w.Write((byte)d.Tier); w.Write((byte)(d.Hard ? 1 : 0));
                foreach (var id in d.PalIds) w.Write((byte)Array.FindIndex(Content.Pals, x => x.Id == id));
                foreach (var rope in d.State) { w.Write((byte)rope.Length); foreach (var v in rope) w.Write((byte)v); }
            }
            w.Flush();
            return ms.ToArray();
        }
    }
}
