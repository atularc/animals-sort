namespace DanglePals
{
    public sealed class ProductDef
    {
        public string Id, Title, Desc;
        public bool NoAds, Best, Consumable;
        public int Coins, Undo, Hint, Rope;
    }

    /// <summary>Business rules in one place. Store/ad IDs live in the service implementations.</summary>
    public static class GameConfig
    {
        public const int InterstitialFromLevel = 15;
        public const int InterstitialEvery = 3;
        public const int RopeUnlockLevel = 3;
        public const int FreeUndo = 3, FreeHint = 3, FreeRope = 1;
        public const int PriceUndo = 40, PriceHint = 60, PriceRope = 90;
        public const int CoinsPerHome = 10;

        public static int Price(string id) => id == "undo" ? PriceUndo : id == "hint" ? PriceHint : PriceRope;

        public static readonly ProductDef[] Products =
        {
            new ProductDef { Id = "starter_pack", Title = "Starter Pack", Desc = "No Ads, 5 Undo, 5 Hint, 3 Ropes, 500 coins", NoAds = true, Coins = 500, Undo = 5, Hint = 5, Rope = 3, Best = true },
            new ProductDef { Id = "no_ads", Title = "No Ads", Desc = "Removes forced ads. Reward videos stay optional.", NoAds = true },
            new ProductDef { Id = "coins_500", Title = "Pouch of Coins", Desc = "500 coins", Coins = 500, Consumable = true },
            new ProductDef { Id = "coins_1500", Title = "Basket of Coins", Desc = "1,500 coins", Coins = 1500, Consumable = true },
            new ProductDef { Id = "coins_5000", Title = "Chest of Coins", Desc = "5,000 coins", Coins = 5000, Consumable = true },
        };
    }
}
