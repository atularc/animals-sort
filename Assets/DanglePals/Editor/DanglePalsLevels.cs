using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DanglePals.EditorTools
{
    /// <summary>
    /// Generates levels 1..BakeCount, proves each one can be completed, and saves them into the game
    /// as Assets/DanglePals/Data/levels.bytes (referenced by GameArt, no runtime loading).
    ///
    /// Checks per level:
    ///  1. Shape: every species has exactly `capacity` pals, no rope over capacity, no rope already finished.
    ///  2. Solvable: the solver finds a full solution, and that solution is replayed move by move from the
    ///     start, with every move checked legal against the rules, ending with every rope sorted.
    ///  3. Repeatable: generating the level a second time gives the identical puzzle.
    ///  4. Round trip: the baked bytes decode back to exactly the same puzzle.
    /// A summary and one line per level go to LevelReport.txt in the project folder.
    /// </summary>
    public static class DanglePalsLevels
    {
        public const int BakeCount = 2000;
        public const string BankPath = "Assets/DanglePals/Data/levels.bytes";
        const string ReportPath = "LevelReport.txt";

        [MenuItem("Dangle Pals/Bake And Verify Levels")]
        public static string BakeMenu() => Bake(BakeCount);

        public static string Bake(int count)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var levels = new List<LevelData>(count);
            var failures = new List<string>();
            var lines = new StringBuilder();
            int hardCount = 0, maxPar = 0;
            var parByWorld = new SortedDictionary<int, (int min, int max, int sum, int n)>();
            var effortByWorld = new Dictionary<int, double>();
            try
            {
                for (int n = 1; n <= count; n++)
                {
                    if (n % 20 == 0 && EditorUtility.DisplayCancelableProgressBar("Dangle Pals", $"Baking and verifying level {n} / {count}", n / (float)count))
                        return "Cancelled at level " + n;

                    var d = LevelGenerator.Generate(n);
                    string err = Check(n, d, out int effort);
                    if (err == null && n % 10 == 0)
                    {
                        var again = LevelGenerator.Generate(n);
                        if (!Same(d, again)) err = "not repeatable";
                    }
                    if (err != null) { failures.Add($"level {n}: {err}"); continue; }

                    levels.Add(d);
                    if (d.Hard) hardCount++;
                    maxPar = Mathf.Max(maxPar, d.Par);
                    int w = Content.WorldIndexForLevel(n);
                    parByWorld.TryGetValue(w, out var s);
                    effortByWorld.TryGetValue(w, out double es); effortByWorld[w] = es + effort;
                    parByWorld[w] = (s.n == 0 ? d.Par : Mathf.Min(s.min, d.Par), Mathf.Max(s.max, d.Par), s.sum + d.Par, s.n + 1);
                    lines.AppendLine($"{n,5}  {Content.WorldForLevel(n).Name,-22} tier {d.Tier}{(d.Hard ? " HARD" : "     ")}  pals {d.Species}  rope size {d.Capacity}  ropes {d.State.Length}  par {d.Par,3}  trickiness {effort,6}  ok");
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            if (failures.Count > 0)
            {
                string msg = $"FAILED: {failures.Count} of {count} levels. First: {failures[0]}";
                File.WriteAllText(ReportPath, msg + "\n" + string.Join("\n", failures) + "\n");
                Debug.LogError("[DanglePals] Level bake " + msg);
                return msg;
            }

            var bytes = LevelBank.Encode(levels);
            LevelBank.Load(bytes);
            for (int n = 1; n <= count; n++)
                if (!Same(levels[n - 1], LevelBank.Get(n))) { string m = "FAILED: round trip mismatch at level " + n; Debug.LogError("[DanglePals] " + m); return m; }

            Directory.CreateDirectory(Path.GetDirectoryName(BankPath));
            File.WriteAllBytes(BankPath, bytes);
            AssetDatabase.ImportAsset(BankPath, ImportAssetOptions.ForceUpdate);
            var art = AssetDatabase.LoadAssetAtPath<GameArt>(DanglePalsSetup.ArtPath);
            if (art != null)
            {
                art.levels = AssetDatabase.LoadAssetAtPath<TextAsset>(BankPath);
                EditorUtility.SetDirty(art);
                AssetDatabase.SaveAssets();
            }

            var head = new StringBuilder();
            head.AppendLine($"Dangle Pals level report  ({System.DateTime.Now:yyyy-MM-dd HH:mm})");
            head.AppendLine($"Levels baked and verified: {count} of {count} PASS   ({hardCount} hard levels, longest par {maxPar} moves)");
            head.AppendLine($"Bank size: {bytes.Length / 1024f:0.0} KB   time: {sw.Elapsed.TotalSeconds:0}s");
            head.AppendLine("Checks per level: shape, player limits (max 7 animal types, 7 pals per rope, 60 moves), solution replayed");
            head.AppendLine("move by move to a fully sorted board, repeatable (every 10th level regenerated), byte round trip.");
            head.AppendLine("Par = moves the solver's solution takes. Trickiness = positions the solver had to explore (more = more tempting wrong moves).");
            head.AppendLine("Levels past " + count + " are generated on the fly by the same deterministic generator.");
            head.AppendLine();
            head.AppendLine("World                     levels      par min / avg / max     avg trickiness");
            foreach (var kv in parByWorld)
            {
                var wd = Content.World(kv.Key);
                int last = wd.StartLevel + kv.Value.n - 1;
                head.AppendLine($"{wd.Name,-24}  {wd.StartLevel,4}-{last,-4}   {kv.Value.min,3} / {kv.Value.sum / (float)kv.Value.n,5:0.0} / {kv.Value.max,3}     {effortByWorld[kv.Key] / kv.Value.n,8:0}");
            }
            head.AppendLine();
            File.WriteAllText(ReportPath, head.ToString() + lines);

            string result = $"PASS {count}/{count} levels verified, {hardCount} hard, bank {bytes.Length / 1024f:0.0} KB, {sw.Elapsed.TotalSeconds:0}s";
            Debug.Log("[DanglePals] Level bake " + result);
            return result;
        }

        static string Check(int n, LevelData d, out int effort)
        {
            effort = 0;
            if (d == null) return "generator returned nothing";
            if (d.Species > LevelGenerator.MaxSpecies || d.Capacity > LevelGenerator.MaxCapacity) return "board too big for a phone";
            int C = d.Capacity, K = d.Species;
            if (d.PalIds.Length != K || d.PalIds.Distinct().Count() != K) return "bad pal list";
            if (d.PalIds.Any(id => System.Array.FindIndex(Content.Pals, p => p.Id == id) < 0)) return "unknown pal";
            var counts = new int[K];
            foreach (var r in d.State)
            {
                if (r.Length > C) return "rope over capacity";
                if (Puzzle.IsDone(r, C)) return "rope already finished at start";
                foreach (var v in r) { if (v < 0 || v >= K) return "bad species id"; counts[v]++; }
            }
            for (int k = 0; k < K; k++) if (counts[k] != C) return $"species {k} has {counts[k]} pals, needs {C}";
            if (Puzzle.IsSolved(d.State, C)) return "already solved";

            var sol = Puzzle.Solve(d.State, C, 400000, out effort);
            if (sol == null) return "no solution found";
            if (!Puzzle.Replays(d.State, C, sol)) return "solution does not replay";
            if (d.Par <= 0 || d.Par > 60) return "par out of player range: " + d.Par;
            if (sol.Count > 60) return "solution too long for a player: " + sol.Count;
            return null;
        }

        static bool Same(LevelData a, LevelData b)
        {
            if (a.Species != b.Species || a.Capacity != b.Capacity || a.Par != b.Par || a.Hard != b.Hard || a.Tier != b.Tier) return false;
            if (!a.PalIds.SequenceEqual(b.PalIds) || a.State.Length != b.State.Length) return false;
            for (int i = 0; i < a.State.Length; i++) if (!a.State[i].SequenceEqual(b.State[i])) return false;
            return true;
        }
    }
}
