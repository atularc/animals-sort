using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanglePals
{
    /// <summary>Pooled sprite particles: hearts, petals, sparks, dust, rings, trails and flying coins.</summary>
    public sealed class Fx : MonoBehaviour
    {
        enum Kind { Heart, Petal, Spark, Dust, Ring, Trail, CoinFly }

        sealed class P
        {
            public Kind K; public float X, Y, Vx, Vy, G, Life, T, Size, Rot, Vr, Drag;
            public Color Col;
            public float X0, Y0, Cx, Cy; public Func<Vector2> Target; public Action OnArrive;
            public SpriteRenderer Sr;
        }

        public Func<float, float, Vector3> ToWorld;   // design px -> world
        public float Quality = 1f;                     // 0.35 with reduced motion
        // Authored in the scene by the setup tool: sprites and a pre-made pool of renderers.
        public Sprite heart, petal, spark, soft, ring, coin;
        public List<SpriteRenderer> pool = new List<SpriteRenderer>();
        readonly List<P> _live = new List<P>();
        readonly Stack<SpriteRenderer> _free = new Stack<SpriteRenderer>();
        Sprite _heart, _petal, _spark, _soft, _ring, _coin;

        void Awake()
        {
            _heart = heart; _petal = petal; _spark = spark; _soft = soft; _ring = ring; _coin = coin;
            foreach (var sr in pool) { sr.gameObject.SetActive(false); _free.Push(sr); }
        }

        SpriteRenderer Get()
        {
            if (_free.Count > 0) { var sr = _free.Pop(); sr.gameObject.SetActive(true); return sr; }
            // Pool exhausted: recycle the oldest live particle instead of allocating.
            var oldest = _live[0];
            _live.RemoveAt(0);
            return oldest.Sr;
        }

        public void Burst(float x, float y, int n, string[] types, Color[] cols, float speed = 160, float life = 0.9f, float size = 6, float g = 280, float dir = -Mathf.PI / 2, float spread = Mathf.PI * 2, float drag = 1.6f)
        {
            int count = Mathf.RoundToInt(n * Quality);
            for (int i = 0; i < count; i++)
            {
                float a = dir + (UnityEngine.Random.value - 0.5f) * spread;
                float sp = speed * (0.5f + UnityEngine.Random.value * 0.7f);
                var kind = Parse(types[i % types.Length]);
                Spawn(new P
                {
                    K = kind, X = x, Y = y, Vx = Mathf.Cos(a) * sp, Vy = Mathf.Sin(a) * sp, G = g,
                    Life = life * (0.7f + UnityEngine.Random.value * 0.5f), Size = size * (0.7f + UnityEngine.Random.value * 0.6f),
                    Col = cols[i % cols.Length], Rot = UnityEngine.Random.value * Mathf.PI * 2, Vr = (UnityEngine.Random.value - 0.5f) * 10f, Drag = drag,
                });
            }
        }

        public void Ring(float x, float y, Color col, float radius) => Spawn(new P { K = Kind.Ring, X = x, Y = y, Life = 0.5f, Size = radius, Col = col });

        public void Trail(float x, float y, Color col, float radius) => Spawn(new P { K = Kind.Trail, X = x, Y = y, Life = 0.24f, Size = radius, Col = col });

        /// <summary>Coins arc from a board point to a HUD target, then call back (for the counter bump and ding).</summary>
        public void Coins(float x, float y, int n, Func<Vector2> target, Action onArrive)
        {
            for (int i = 0; i < n; i++)
                Spawn(new P
                {
                    K = Kind.CoinFly, X0 = x + (UnityEngine.Random.value - 0.5f) * 30, Y0 = y - 10, X = x, Y = y, T = -i * 0.07f, Life = 0.75f, Size = 9,
                    Cx = x + (UnityEngine.Random.value - 0.5f) * 120, Cy = y - 170 - UnityEngine.Random.value * 60, Target = target, OnArrive = onArrive, Col = Color.white,
                });
        }

        static Kind Parse(string s) => s switch { "heart" => Kind.Heart, "petal" => Kind.Petal, "dust" => Kind.Dust, _ => Kind.Spark };

        void Spawn(P p)
        {
            p.Sr = Get();
            p.Sr.sprite = p.K switch { Kind.Heart => _heart, Kind.Petal => _petal, Kind.Dust => _soft, Kind.Trail => _soft, Kind.Ring => _ring, Kind.CoinFly => _coin, _ => _spark };
            p.Sr.sortingOrder = p.K == Kind.Trail ? 65 : 90;
            p.Sr.enabled = p.T >= 0;
            _live.Add(p);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.T += dt;
                if (p.K == Kind.CoinFly)
                {
                    if (p.T < 0) continue;
                    p.Sr.enabled = true;
                    float u = Mathf.Clamp01(p.T / p.Life), e = Ease.InOut(u);
                    var tg = p.Target != null ? p.Target() : new Vector2(290, 40);
                    p.X = (1 - e) * (1 - e) * p.X0 + 2 * (1 - e) * e * p.Cx + e * e * tg.x;
                    p.Y = (1 - e) * (1 - e) * p.Y0 + 2 * (1 - e) * e * p.Cy + e * e * tg.y;
                    p.Sr.transform.position = ToWorld(p.X, p.Y);
                    p.Sr.transform.localScale = Vector3.one * (p.Size / 12f);
                    if (u >= 1) { p.OnArrive?.Invoke(); Kill(i); }
                    continue;
                }
                if (p.T >= p.Life) { Kill(i); continue; }
                p.Vy += p.G * dt;
                p.Vx *= Mathf.Exp(-p.Drag * dt);
                p.Vy *= Mathf.Exp(-p.Drag * 0.5f * dt);
                p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Rot += p.Vr * dt;
                float uu = p.T / p.Life, alpha = uu < 0.7f ? 1f : 1f - (uu - 0.7f) / 0.3f;
                float scale;
                switch (p.K)
                {
                    case Kind.Dust: scale = p.Size * (0.6f + uu) / 12f; alpha *= 0.7f; break;
                    case Kind.Ring: scale = p.Size * Ease.OutCubic(uu) / 20f; alpha = 1f - uu; break;
                    case Kind.Trail: scale = p.Size * (1f - uu) / 12f; alpha = 0.5f * (1f - uu); break;
                    default: scale = p.Size / 10f; break;
                }
                var c = p.Col; c.a *= Mathf.Clamp01(alpha);
                p.Sr.color = c;
                var t = p.Sr.transform;
                t.position = ToWorld(p.X, p.Y);
                t.rotation = Quaternion.Euler(0, 0, -p.Rot * Mathf.Rad2Deg);
                t.localScale = new Vector3(scale, scale, 1);
            }
        }

        void Kill(int i)
        {
            var p = _live[i];
            p.Sr.gameObject.SetActive(false);
            _free.Push(p.Sr);
            _live.RemoveAt(i);
        }
    }
}
