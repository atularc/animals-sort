using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanglePals
{
    public enum PalState { Rope, Held, Home, Gone }

    /// <summary>One hanging animal. All positions are in design pixels (390-wide, y down).</summary>
    public sealed class Pal
    {
        public int Id, K;
        public PalDef Sp;
        public float X, Y, Rot, Sx = 1, Sy = 1, SqV, Scale, ScaleT = 1, ScV, Delay, Glow, GlowT, MoodT, Blink, BlinkIn, Hop, Shake, TrailT;
        public string Mood = "idle";
        public PalState State = PalState.Rope;
        public PalFly Fly;
        public HomeFly Hf;
        public Transform Root;
        public SpriteRenderer Img, GlowSr;
    }

    public sealed class PalFly { public float Sx, Sy, T, Dur, Arc; public Func<float, float> Ease; }
    public sealed class HomeFly { public float Sx, Sy, Cx, Cy, Ex, T, Dur; public Home Home; }

    public sealed class Rope
    {
        public int Id;
        public List<Pal> Items = new List<Pal>();
        public float X, Tx, Ang, AngV, Drop, DropDelay, Retract, Flash;
        public bool Retracting, Completing, Dead;
        public Transform Root;
        public SpriteRenderer Line, Bead, Knot, Pill;
    }

    public sealed class Home
    {
        public PalDef Sp;
        public int K, Count;
        public bool Filled;
        public float X, Y, Sq = 1, SqV, FillT;
        public Transform Root, Body;
        public SpriteRenderer PalSr, Glow, Badge;
        public readonly List<SpriteRenderer> Dots = new List<SpriteRenderer>();
    }

    public sealed class Snapshot
    {
        public List<(int id, List<Pal> items)> Ropes = new List<(int, List<Pal>)>();
        public int Moves;
    }
}
