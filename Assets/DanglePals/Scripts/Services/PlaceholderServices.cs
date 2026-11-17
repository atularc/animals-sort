using System;
using UnityEngine;

namespace DanglePals
{
    /// <summary>
    /// Stand-in ads until the LevelPlay SDK is connected: shows a short "test ad" card and grants the reward,
    /// so every ad-gated flow (refills, double coins, interstitial pacing) can be played end to end.
    /// </summary>
    public sealed class PlaceholderAds : IAdsService
    {
        readonly Action<string, Action> _showCard;
        public PlaceholderAds(Action<string, Action> showCard) { _showCard = showCard; }
        public void Init() { }
        public bool PrivacyOptionsRequired => false;
        public void ShowPrivacyOptions() { }
        public void ShowRewarded(string placement, Action<bool> done) => _showCard("Reward video", () => done(true));
        public void ShowInterstitial(string placement, Action done) => _showCard("Interstitial", done);
    }

    /// <summary>
    /// Stand-in store until Unity IAP is connected. In the Editor it "sells" instantly so the shop can be tested;
    /// in a device build it reports unavailable and the shop shows no prices.
    /// </summary>
    public sealed class PlaceholderIap : IIapService
    {
        public bool Available => Application.isEditor;
        public void Init(Action<bool> noAdsOwned) => noAdsOwned(false);
        public string Price(string productId) => Application.isEditor ? productId switch
        {
            "starter_pack" => "$2.99", "no_ads" => "$3.99", "coins_500" => "$0.99", "coins_1500" => "$2.49", "coins_5000" => "$6.99", _ => null,
        } : null;
        public void Buy(string productId, Action<PurchaseResult> done) => done(new PurchaseResult { Ok = Application.isEditor });
        public void Restore(Action<bool, bool> done) => done(Application.isEditor, false);
    }
}
