using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DanglePals
{
    /// <summary>
    /// Every sprite, sound and font the game uses, referenced directly (no Resources folder, no runtime loading).
    /// Filled by "Dangle Pals > Run Full Setup" and referenced by the scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Dangle Pals/Game Art")]
    public sealed class GameArt : ScriptableObject
    {
        [Serializable] public sealed class SpriteEntry { public string key; public Sprite sprite; }
        [Serializable] public sealed class ClipEntry { public string key; public AudioClip clip; }

        public List<SpriteEntry> sprites = new List<SpriteEntry>();
        public List<SpriteEntry> pals = new List<SpriteEntry>();
        public List<ClipEntry> clips = new List<ClipEntry>();
        public TMP_FontAsset fontSemiBold, fontBold;
        [Tooltip("Baked, verified levels (Dangle Pals > Bake And Verify Levels).")]
        public TextAsset levels;

        Dictionary<string, Sprite> _sprites, _pals;
        Dictionary<string, AudioClip> _clips;

        void Index()
        {
            if (_sprites != null) return;
            _sprites = new Dictionary<string, Sprite>();
            _pals = new Dictionary<string, Sprite>();
            _clips = new Dictionary<string, AudioClip>();
            foreach (var e in sprites) _sprites[e.key] = e.sprite;
            foreach (var e in pals) _pals[e.key] = e.sprite;
            foreach (var e in clips) _clips[e.key] = e.clip;
        }

        public Sprite Sprite(string key) { Index(); if (_sprites.TryGetValue(key, out var s)) return s; Debug.LogError("[DanglePals] Missing sprite " + key); return null; }
        public Sprite Pal(string id, string mood) { Index(); if (_pals.TryGetValue(id + "_" + mood, out var s)) return s; Debug.LogError("[DanglePals] Missing pal " + id + "_" + mood); return null; }
        public AudioClip Clip(string key) { Index(); if (_clips.TryGetValue(key, out var c)) return c; Debug.LogError("[DanglePals] Missing clip " + key); return null; }
        public void ResetIndex() { _sprites = null; }
    }
}
