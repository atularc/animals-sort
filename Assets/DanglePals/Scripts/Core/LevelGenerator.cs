using System.Collections.Generic;

namespace DanglePals
{
    public sealed class LevelData
    {
        public int Number, Species, Capacity, Par, Tier;
        public string[] PalIds;   // species used, index = species id in State
        public int[][] State;     // ropes, top -> bottom
        public bool Hard;
    }

    /// <summary>
    /// Endless, deterministic levels whose difficulty rises a little on every level.
    ///
    /// Difficulty = how many moves the solver needs (par). Each level has a TARGET par on a smooth
    /// curve (7 moves at level 1 up to ~49 at level 2000). The generator builds candidates and keeps the
    /// one closest to the target, so level n+1 is, on average, a touch harder than level n.
    /// Every 5th level is a Hard spike (+15%), the one after it a breather (-15%).
    /// Among candidates near the target, the TRICKIEST one wins: the one the solver had to search
    /// hardest for, i.e. the most tempting wrong moves. So late levels keep getting harder to think
    /// through even where adding more moves would only make them longer, not more interesting.
    ///
    /// Kept human: at most 7 animal types (pals stay big enough to tap), at most 7 pals per rope,
    /// always one rope's worth of free space, and the target never goes past MaxTarget moves.
    /// Every level is verified solvable by the solver; the solution length becomes par.
    /// </summary>
    public static class LevelGenerator
    {
        public const int MaxSpecies = 7, MaxCapacity = 7;
        const float MaxTarget = 56f;

        // (level, target par) points; linear in between. Steep early (learning), then a steady climb.
        static readonly float[,] Curve =
        {
            { 1, 7 }, { 5, 11 }, { 15, 16 }, { 35, 22 }, { 60, 27 }, { 90, 31 }, { 125, 34 },
            { 200, 37 }, { 300, 39 }, { 400, 41 }, { 600, 43 }, { 900, 45 }, { 1300, 47 }, { 2000, 49 },
        };

        public static float TargetPar(int n)
        {
            int last = Curve.GetLength(0) - 1;
            if (n >= Curve[last, 0]) return System.Math.Min(MaxTarget, Curve[last, 1] + (n - Curve[last, 0]) / 250f);
            for (int i = 0; i < last; i++)
                if (n <= Curve[i + 1, 0])
                {
                    float t = (n - Curve[i, 0]) / (Curve[i + 1, 0] - Curve[i, 0]);
                    return Curve[i, 1] + (Curve[i + 1, 1] - Curve[i, 1]) * t;
                }
            return Curve[0, 1];
        }

        public static bool IsHard(int n) => n > 4 && n % 5 == 0;
        public static bool IsBreather(int n) => n > 6 && n % 5 == 1;

        /// <summary>Board shape for level n: animal types and pals per rope grow with the level.</summary>
        public static void Shape(int n, out int species, out int capacity)
        {
            species = n <= 2 ? 3 : n <= 15 ? 4 : n <= 25 ? 5 : n <= 60 ? 6 : 7;
            capacity = n <= 4 ? 4 : n <= 50 ? 5 : n <= 400 ? 6 : 7;
            if (IsHard(n) && n > 8) species++;                          // spikes also add a pal type
            species = System.Math.Min(species, System.Math.Min(MaxSpecies, Content.UnlockedPals(n).Count));
            capacity = System.Math.Min(capacity, MaxCapacity);
        }

        /// <summary>Kept for the UI and reports: a 0..9 band of the level's target.</summary>
        public static int TierFor(int n, out bool hard)
        {
            hard = IsHard(n);
            return System.Math.Min(9, (int)((Target(n) - 7) / 6f));
        }

        static float Target(int n)
        {
            float t = TargetPar(n);
            if (IsHard(n)) t *= 1.15f; else if (IsBreather(n)) t *= 0.85f;
            return System.Math.Min(t, MaxTarget);
        }

        /// <summary>
        /// Builds level n. Pure function of n: no clock, no device speed, so every phone gets the same puzzle.
        /// Never returns null: if no candidate solves, it retries with one more spare rope.
        /// </summary>
        public static LevelData Generate(int n) => Generate(n, 0);

        static LevelData Generate(int n, int bonusRopes)
        {
            Shape(n, out int K, out int C);
            bool hard = IsHard(n);
            float target = Target(n);
            int tier = TierFor(n, out _);
            var unlocked = Content.UnlockedPals(n);
            // Baked levels (1..2000) search widely; levels generated on a phone past that stay quick.
            int attempts = n <= 15 ? 30 : n <= 2000 ? 40 : 10;

            LevelData best = null, trickiest = null;
            float bestCost = float.MaxValue;
            int trickiestEffort = -1;
            for (int a = 0; a < attempts; a++)
            {
                var cand = Candidate(n, K, C, 1 + bonusRopes, a, unlocked, out int effort);
                if (cand == null) continue;
                cand.Tier = tier; cand.Hard = hard;
                // Closest to target; undershooting costs more than overshooting.
                float d = cand.Par - target;
                float cost = d >= 0 ? d : -d * 1.6f;
                if (cost < bestCost) { bestCost = cost; best = cand; }
                if (n <= 15) { if (d >= 0 && d <= 2) break; continue; }   // learning levels: just the right length
                bool inBand = d >= -1 && (hard || d <= 3);
                if (inBand && effort > trickiestEffort) { trickiestEffort = effort; trickiest = cand; }
            }
            if (trickiest != null) best = trickiest;
            if (best == null && bonusRopes < 2) return Generate(n, bonusRopes + 1);
            return best;
        }

        /// <summary>One deterministic candidate (attempt a) for level n, or null if trivial or unsolved.</summary>
        public static LevelData Candidate(int n, int K, int C, int extraRopes, int a, List<string> unlocked, out int expanded)
        {
            expanded = 0;
            int R = K + extraRopes;
            var rng = new Mulberry32((uint)(n * 9973 + a * 7919 + 13 + (extraRopes - 1) * 104729));
            string[] ids = PickPals(n, K, unlocked, rng);
            var items = new List<int>();
            for (int k = 0; k < K; k++) for (int c = 0; c < C; c++) items.Add(k);
            rng.Shuffle(items);
            var lens = new int[R];
            for (int i = 0; i < R; i++) lens[i] = C;
            int free = R * C - K * C, guard = 0;
            while (free > 0 && guard++ < 4000)
            {
                int i = rng.Range(R);
                if (lens[i] > 1 || (lens[i] > 0 && rng.Next() < 0.2)) { lens[i]--; free--; }
            }
            var st = new int[R][];
            int p = 0;
            for (int r = 0; r < R; r++)
            {
                st[r] = items.GetRange(p, lens[r]).ToArray();
                p += lens[r];
                if (Puzzle.IsDone(st[r], C)) return null;
            }
            var sol = Puzzle.Solve(st, C, n <= 3 ? 12000 : 60000, out expanded);
            if (sol == null) return null;
            return new LevelData { Number = n, Species = K, Capacity = C, Par = sol.Count, PalIds = ids, State = st };
        }

        static string[] PickPals(int n, int K, List<string> unlocked, Mulberry32 rng)
        {
            var pool = new List<string>(unlocked);
            var chosen = new List<string>();
            // The newest world's pals always feature, so a new unlock is seen straight away.
            var world = Content.WorldForLevel(n);
            foreach (var id in world.NewPals) if (chosen.Count < K) chosen.Add(id);
            rng.Shuffle(pool);
            foreach (var id in pool)
            {
                if (chosen.Count >= K) break;
                if (chosen.Contains(id)) continue;
                if ((id == "bunny" && chosen.Contains("panda")) || (id == "panda" && chosen.Contains("bunny"))) continue; // both white
                chosen.Add(id);
            }
            foreach (var id in pool) { if (chosen.Count >= K) break; if (!chosen.Contains(id)) chosen.Add(id); }
            if (n == 1) return new[] { "fox", "bunny", "bear" };
            rng.Shuffle(chosen);
            return chosen.ToArray();
        }
    }
}
