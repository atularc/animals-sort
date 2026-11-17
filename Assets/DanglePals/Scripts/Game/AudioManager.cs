using System.Collections.Generic;
using UnityEngine;

namespace DanglePals
{
    /// <summary>
    /// Plays the exported sound set. Every tonal clip sits in C major pentatonic, so overlaps stay pleasant.
    /// The AudioSource pool is authored in the scene; light random pitch keeps repeats from sounding robotic.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public List<AudioSource> pool = new List<AudioSource>();
        public AudioSource music;
        int _next;
        bool _sfxOn = true;
        float _duck = 1f, _duckT;
        const float MusicVol = 0.55f;

        public void SetSfx(bool on) => _sfxOn = on;

        public void SetMusic(bool on)
        {
            if (on) { if (!music.isPlaying) { music.clip = Res.Clip("music_meadow"); music.Play(); } }
            else music.Stop();
        }

        public void Pause(bool paused) => AudioListener.pause = paused;

        /// <summary>Music dips a little under big payoffs.</summary>
        public void Duck(float seconds) { _duckT = seconds; }

        void Update()
        {
            _duckT -= Time.unscaledDeltaTime;
            _duck = Mathf.MoveTowards(_duck, _duckT > 0 ? 0.7f : 1f, Time.unscaledDeltaTime * 2f);
            if (music) music.volume = MusicVol * _duck;
        }

        public void Play(string clip, float volume = 1f, float pitchCents = 25f, float delay = 0f)
        {
            if (!_sfxOn || pool.Count == 0) return;
            var c = Res.Clip(clip);
            if (c == null) return;
            var s = pool[_next];
            _next = (_next + 1) % pool.Count;
            s.clip = c;
            s.volume = volume;
            s.pitch = Mathf.Pow(2f, Random.Range(-pitchCents, pitchCents) / 1200f);
            if (delay > 0) s.PlayDelayed(delay); else s.Play();
        }

        public void Pick(PalDef p) { Play("sfx_pop"); Play("voice_" + p.Id, 1f, 60f, 0.05f); }
        public void Voice(PalDef p, float delay = 0f) => Play("voice_" + p.Id, 1f, 60f, delay);
        public void Drop(int n) => Play("sfx_drop_" + Mathf.Clamp(n, 1, 7), 1f, 8f);
        public void Bad() => Play("sfx_bad", 1f, 10f);
        public void Cheer(int i) => Play("sfx_cheer_" + (i % 6), 1f, 0f);
        public void Shimmer() => Play("sfx_shimmer", 1f, 0f);
        public void Whoosh() => Play("sfx_whoosh");
        public void HomePop(int i) => Play("sfx_homepop_" + Mathf.Clamp(i, 0, 5), 1f, 0f);
        public void HomeFilled() { Play("sfx_homefilled", 1f, 0f); Duck(1.2f); }
        public void Praise(int tier) => Play("sfx_praise_" + Mathf.Clamp(tier, 0, 4), 1f, 0f);
        public void Coin() => Play("sfx_coin", 0.9f, 30f);
        public void Tick() => Play("sfx_tick", 1f, 30f);
        public void Tap() => Play("sfx_tap", 1f, 15f);
        public void Hint() => Play("sfx_hint", 1f, 0f);
        public void Undo() => Play("sfx_undo");
        public void RopeAdded() => Play("sfx_rope");
        public void Stuck() => Play("sfx_stuck", 1f, 0f);
        public void Star(int i) => Play("sfx_star_" + Mathf.Clamp(i, 0, 2), 1f, 0f);
        public void Fanfare() { Play("sfx_fanfare", 1f, 0f); Duck(3.5f); }
    }
}
