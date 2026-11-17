using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DanglePals.EditorTools
{
    /// <summary>
    /// Editor-only test: in Play mode, plays real levels through the real GameController (its own
    /// TryMove, rope completion, parades, homes and win screen), choosing each move with the solver,
    /// exactly as a player tapping would. Restores the player's save afterwards.
    /// Start with Run(levels) while playing; read progress from Status.
    /// </summary>
    public static class DanglePalsAutoPlay
    {
        public static string Status = "idle";
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static GameController _gc;
        static Queue<int> _queue;
        static int _level, _moves;
        static double _lastMove, _levelStart;
        static string _savedJson;
        static readonly StringBuilder Log = new StringBuilder();
        static int _passed, _failed;

        public static string Run(string levelsCsv)
        {
            if (!EditorApplication.isPlaying) return "Enter Play mode first";
            _gc = Object.FindFirstObjectByType<GameController>();
            if (_gc == null) return "No GameController";
            _savedJson = PlayerPrefs.GetString("danglepals.save.v1", null);
            _queue = new Queue<int>();
            foreach (var s in levelsCsv.Split(',')) _queue.Enqueue(int.Parse(s.Trim()));
            Log.Clear(); _passed = _failed = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            StartNext();
            return "started " + (_queue.Count + 1) + " levels";
        }

        static T Get<T>(string name) => (T)typeof(GameController).GetField(name, F).GetValue(_gc);
        static object Call(string name, params object[] args) => typeof(GameController).GetMethod(name, F).Invoke(_gc, args);

        static void StartNext()
        {
            if (_queue.Count == 0) { Finish(); return; }
            _level = _queue.Dequeue();
            var ui = Get<GameUi>("_ui");
            ui.HideWin(); ui.HideHome(); ui.HideSheet();
            Call("NewGame", _level, true);
            Call("StartLevel");
            _moves = 0;
            _levelStart = EditorApplication.timeSinceStartup;
            _lastMove = _levelStart + 1.0;
            Status = $"playing level {_level} ({_passed} passed, {_failed} failed)";
        }

        static void Fail(string why)
        {
            _failed++;
            Log.AppendLine($"level {_level}: FAIL {why}");
            StartNext();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || _gc == null) { EditorApplication.update -= Tick; Status = "stopped: left Play mode. " + Log; return; }
            double now = EditorApplication.timeSinceStartup;
            if (Get<bool>("_won"))
            {
                if (now - _lastMove < 1.5) return;   // let the win screen appear
                int par = Get<int>("_par");
                _passed++;
                Log.AppendLine($"level {_level}: WON in {Get<int>("_moves")} moves (par {par}), {now - _levelStart:0}s");
                StartNext();
                return;
            }
            if (now - _levelStart > 240) { Fail("timed out"); return; }
            var ui = Get<GameUi>("_ui");
            if (ui.SheetOpen && ui.SheetKind == "stuck") { Fail("stuck sheet appeared on a solvable board"); return; }
            if (now - _lastMove < 0.45) return;

            var ropes = Get<IList>("_ropes");
            if (Get<IList>("_flying").Count > 0) return;
            foreach (var r in ropes) { var t = r.GetType(); if ((bool)t.GetField("Completing").GetValue(r) || (bool)t.GetField("Retracting").GetValue(r)) return; }

            var st = (int[][])Call("Ints");
            int c = Get<int>("_c");
            if (Puzzle.IsSolved(st, c)) return;   // last parade still on its way home
            var sol = Puzzle.Solve(st, c, 400000, out _);
            if (sol == null || sol.Count == 0) { Fail("solver found no way from the in-game board"); return; }
            var m = sol[0];
            object src = ropes[m.Src], dst = ropes[m.Dst];
            var items = (IList)src.GetType().GetField("Items").GetValue(src);
            var chain = items.GetType().GetMethod("GetRange").Invoke(items, new object[] { m.Idx, items.Count - m.Idx });
            bool ok = (bool)Call("TryMove", src, m.Idx, dst, chain);
            if (!ok) { Fail($"game rejected a legal move {m.Src}/{m.Idx}->{m.Dst}"); return; }
            _moves++;
            _lastMove = now;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            if (_savedJson != null) PlayerPrefs.SetString("danglepals.save.v1", _savedJson); else PlayerPrefs.DeleteKey("danglepals.save.v1");
            PlayerPrefs.Save();
            Status = $"DONE {_passed} passed, {_failed} failed\n{Log}";
            Debug.Log("[DanglePals] AutoPlay " + Status);
        }
    }
}
