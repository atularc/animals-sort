using System;

namespace DanglePals
{
    /// <summary>Ad network wrapper. The game only talks to this, so the SDK can change without touching gameplay.</summary>
    public interface IAdsService
    {
        void Init();
        bool PrivacyOptionsRequired { get; }
        void ShowPrivacyOptions();
        /// <summary>Calls back true only when the reward was earned.</summary>
        void ShowRewarded(string placement, Action<bool> done);
        void ShowInterstitial(string placement, Action done);
    }

    public struct PurchaseResult { public bool Ok, Cancelled, NoAds; }

    /// <summary>In-app purchase wrapper (Google Play Billing / StoreKit behind it).</summary>
    public interface IIapService
    {
        bool Available { get; }
        void Init(Action<bool> noAdsOwned);
        string Price(string productId);
        void Buy(string productId, Action<PurchaseResult> done);
        void Restore(Action<bool, bool> done); // ok, noAds
    }
}
