using System;
using UnityEngine;

namespace DanglePals
{
    [Serializable]
    public sealed class SaveData
    {
        public int level = 1, coins = 120, streak, winsSinceAd, worldSeen = -1;
        public bool sfx = true, music = true, haptics = true, noAds;
        public int stockUndo, stockHint, stockRope;

        const string Key = "danglepals.save.v1";

        public static SaveData Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json)) return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            }
            catch (Exception e) { Debug.LogWarning("[DanglePals] Save unreadable, starting fresh: " + e.Message); }
            return new SaveData();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public int Stock(string id) => id == "undo" ? stockUndo : id == "hint" ? stockHint : id == "rope" ? stockRope : 0;
        public void AddStock(string id, int n)
        {
            if (id == "undo") stockUndo += n; else if (id == "hint") stockHint += n; else if (id == "rope") stockRope += n;
        }
    }
}
