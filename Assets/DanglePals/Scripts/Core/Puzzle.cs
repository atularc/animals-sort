using System.Collections.Generic;
using System.Text;

namespace DanglePals
{
    public struct Move
    {
        public int Src, Idx, Dst;
        public Move(int s, int i, int d) { Src = s; Idx = i; Dst = d; }
    }

    /// <summary>
    /// The rules, with no Unity dependencies.
    /// A rope is a list of species ids, index 0 hangs at the top, the last item is the bottom.
    /// Any pal can be grabbed; it and everything below it move as a chain onto an empty rope
    /// or onto a rope whose bottom pal matches the chain's top pal, if the chain fits.
    /// </summary>
    public static class Puzzle
    {
        public static bool IsDone(IReadOnlyList<int> r, int capacity)
        {
            if (r.Count != capacity) return false;
            for (int i = 1; i < r.Count; i++) if (r[i] != r[0]) return false;
            return true;
        }

        public static bool IsSolved(int[][] st, int capacity)
        {
            foreach (var r in st) if (r.Length != 0 && !IsDone(r, capacity)) return false;
            return true;
        }

        public static List<Move> GenMoves(int[][] st, int capacity)
        {
            var output = new List<Move>();
            for (int s = 0; s < st.Length; s++)
            {
                var r = st[s];
                if (r.Length == 0 || IsDone(r, capacity)) continue;
                for (int i = r.Length - 1; i >= 0; i--)
                {
                    int top = r[i], len = r.Length - i;
                    for (int d = 0; d < st.Length; d++)
                    {
                        if (d == s) continue;
                        var t = st[d];
                        if (t.Length + len > capacity) continue;
                        if (t.Length == 0) { if (i == 0) continue; }
                        else if (t[t.Length - 1] != top) continue;
                        output.Add(new Move(s, i, d));
                    }
                }
            }
            return output;
        }

        public static int[][] Apply(int[][] st, Move m)
        {
            var next = (int[][])st.Clone();
            var src = st[m.Src];
            var a = new int[m.Idx];
            System.Array.Copy(src, a, m.Idx);
            var chainLen = src.Length - m.Idx;
            var b = new int[st[m.Dst].Length + chainLen];
            System.Array.Copy(st[m.Dst], b, st[m.Dst].Length);
            System.Array.Copy(src, m.Idx, b, st[m.Dst].Length, chainLen);
            next[m.Src] = a;
            next[m.Dst] = b;
            return next;
        }

        static float Heuristic(int[][] st, int capacity)
        {
            float b = 0;
            foreach (var r in st)
            {
                if (r.Length == 0) continue;
                for (int i = 1; i < r.Length; i++) if (r[i] != r[i - 1]) b++;
                if (!IsDone(r, capacity)) b += 0.6f;
            }
            return b;
        }

        static string Key(int[][] st)
        {
            var parts = new string[st.Length];
            var sb = new StringBuilder();
            for (int i = 0; i < st.Length; i++)
            {
                sb.Clear();
                foreach (var v in st[i]) { sb.Append((char)('a' + v)); }
                parts[i] = sb.ToString();
            }
            System.Array.Sort(parts, System.StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        sealed class Node { public int[][] St; public int Parent; public Move Mv; public int G; }

        /// <summary>Weighted A*. Returns a move list, or null if none found within the node budget.</summary>
        public static List<Move> Solve(int[][] start, int capacity, int nodeLimit, out int expanded)
            => Solve(start, capacity, nodeLimit, out expanded, out _);

        /// <summary>
        /// As above. <paramref name="exhausted"/> is true when every reachable position was visited,
        /// so a null result then proves the position is a dead end (no sequence of moves can win).
        /// </summary>
        public static List<Move> Solve(int[][] start, int capacity, int nodeLimit, out int expanded, out bool exhausted)
        {
            expanded = 0; exhausted = false;
            var nodes = new List<Node> { new Node { St = start, Parent = -1 } };
            var seen = new HashSet<string> { Key(start) };
            var heap = new MinHeap();
            heap.Push(0, Heuristic(start, capacity));
            while (heap.Count > 0 && expanded < nodeLimit)
            {
                int idx = heap.Pop();
                var nd = nodes[idx];
                expanded++;
                if (IsSolved(nd.St, capacity))
                {
                    var path = new List<Move>();
                    for (int j = idx; nodes[j].Parent >= 0; j = nodes[j].Parent) path.Add(nodes[j].Mv);
                    path.Reverse();
                    return path;
                }
                foreach (var mv in GenMoves(nd.St, capacity))
                {
                    var st = Apply(nd.St, mv);
                    if (!seen.Add(Key(st))) continue;
                    nodes.Add(new Node { St = st, Parent = idx, Mv = mv, G = nd.G + 1 });
                    heap.Push(nodes.Count - 1, nd.G + 1 + 2.2f * Heuristic(st, capacity));
                }
            }
            exhausted = heap.Count == 0;
            return null;
        }

        /// <summary>Plays a move list from the start and reports whether it ends solved, checking every move is legal.</summary>
        public static bool Replays(int[][] start, int capacity, List<Move> moves)
        {
            var st = start;
            foreach (var m in moves)
            {
                bool legal = false;
                foreach (var g in GenMoves(st, capacity)) if (g.Src == m.Src && g.Idx == m.Idx && g.Dst == m.Dst) { legal = true; break; }
                if (!legal) return false;
                st = Apply(st, m);
            }
            return IsSolved(st, capacity);
        }

        sealed class MinHeap
        {
            readonly List<(float f, int i)> _h = new List<(float, int)>();
            public int Count => _h.Count;
            public void Push(int i, float f)
            {
                _h.Add((f, i));
                int k = _h.Count - 1;
                while (k > 0)
                {
                    int p = (k - 1) >> 1;
                    if (_h[p].f <= _h[k].f) break;
                    (_h[p], _h[k]) = (_h[k], _h[p]);
                    k = p;
                }
            }
            public int Pop()
            {
                var top = _h[0];
                var last = _h[_h.Count - 1];
                _h.RemoveAt(_h.Count - 1);
                if (_h.Count > 0)
                {
                    _h[0] = last;
                    int k = 0;
                    while (true)
                    {
                        int l = 2 * k + 1, r = l + 1, m = k;
                        if (l < _h.Count && _h[l].f < _h[m].f) m = l;
                        if (r < _h.Count && _h[r].f < _h[m].f) m = r;
                        if (m == k) break;
                        (_h[m], _h[k]) = (_h[k], _h[m]);
                        k = m;
                    }
                }
                return top.i;
            }
        }
    }
}
