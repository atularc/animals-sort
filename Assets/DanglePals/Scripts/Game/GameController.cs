using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DanglePals
{
    /// <summary>
    /// The game: rules, board, animation, effects, flow between screens.
    /// Positions are kept in "design pixels" (390 wide, y down from the top of the safe area), the same
    /// space the prototype used, and converted to world units only when drawing. 1 world unit = 100 design px.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const float W = 390f;
        static readonly string[] Moods = { "idle", "blink", "happy", "wow" };

        [Header("Scene references (authored by Dangle Pals > Run Full Setup)")]
        public GameArt art;
        public GameUi ui;
        public AudioManager audioManager;
        public Fx fx;
        [Header("Scenery")]
        public SpriteRenderer backdrop;
        public SpriteRenderer groundFill, branchSr, handSr;
        public SpriteRenderer[] cloudSrs = new SpriteRenderer[0];
        public SpriteRenderer[] moteSrs = new SpriteRenderer[0];

        [Header("Level limit")]
        [Tooltip("When ticked, only levels 1 to 10 can be played. After level 10 the player stays on level 10.")]
        public bool limitToTenLevels;
        public const int LimitedMaxLevel = 10;
        int MaxPlayable => limitToTenLevels ? LimitedMaxLevel : int.MaxValue;

        Camera _cam;
        GameUi _ui;
        AudioManager _audio;
        Fx _fx;
        SaveData _save;
        IAdsService _ads;
        IIapService _iap;

        // ---- layout (design px)
        float _hs = 844, _vw = 390, _h = 760, _insetTop, _ox;
        float _branchY = 158, _topY = 164, _homeY = 566;
        Vector2Int _lastScreen;
        Rect _lastSafe;

        // ---- static scene
        Transform _boardRoot;
        SpriteRenderer _bg, _bgFill, _branch, _hand;
        readonly List<(SpriteRenderer sr, float s, float v)> _clouds = new List<(SpriteRenderer, float, float)>();
        readonly float[] _cloudX = { 40, 250, 170, 330 }, _cloudY = { 150, 118, 250, 300 };
        readonly List<(SpriteRenderer sr, float p, float s)> _motes = new List<(SpriteRenderer, float, float)>();
        readonly List<Vector2> _motePos = new List<Vector2>();

        // ---- level state
        LevelData _data;
        WorldDef _world;
        int _n, _c, _k, _par, _moves, _done, _starsShown = 3, _tutorial;
        float _lastAct;   // time of the player's last touch on the board, for the free idle hint
        const int FreeIdleHintUpToLevel = 10;
        const float IdleHintAfter = 12f;
        float _playTime, _size, _gap, _ropeLen, _spacing, _homeW, _starPop, _coinBump, _coinsShown, _checkAt, _time;
        bool _won, _started, _stuckShown;
        readonly List<Rope> _ropes = new List<Rope>();
        readonly List<Home> _homes = new List<Home>();
        readonly List<Pal> _flying = new List<Pal>();
        readonly List<Snapshot> _undo = new List<Snapshot>();
        readonly Dictionary<string, int> _boost = new Dictionary<string, int>();
        readonly Dictionary<Pal, Sprite[]> _faces = new Dictionary<Pal, Sprite[]>();
        readonly List<(float t, Action fn)> _timers = new List<(float, Action)>();
        int _ropeSeq, _palSeq;
        Rope _selSrc;
        int _selIdx;

        sealed class DragState { public Rope Src; public int Idx; public List<Pal> Chain; public float Sx, Sy, OffX, OffY; public bool Moved; }
        DragState _drag;
        Vector2 _pointer;
        sealed class HintState { public Rope Src, Dst; public int Idx; public float T; }
        HintState _hint;
        string _refillId;
        int _winReward;
        bool _adShowing;

        bool Paused => _ui.SheetOpen || _ui.WinOpen || _ui.HomeOpen || _adShowing;

        void Awake()
        {
            Res.Art = art;
            LevelBank.Load(art.levels != null ? art.levels.bytes : null);
            _ui = ui; _audio = audioManager; _fx = fx;
        }

        // ======================================================================================= setup
        void Start()
        {
            Application.targetFrameRate = 60;
            _cam = Camera.main;
            _save = SaveData.Load();
            Haptics.Enabled = _save.haptics;
            _coinsShown = _save.coins;

            _fx.ToWorld = P;
            _ui.DesignToCanvas = (x, y) => (Vector2)P(x, y) * 100f;
            _ui.OnClick = HandleClick;

            _ads = new PlaceholderAds((label, done) => { _adShowing = true; _audio.Pause(true); _ui.ShowAdCard(label, () => { _adShowing = false; _audio.Pause(false); done(); }); });
            _iap = new PlaceholderIap();
            _ads.Init();
            _iap.Init(noAds => { if (noAds) { _save.noAds = true; _save.Save(); } });

            BuildStatic();
            Relayout(true);
            NewGame(Mathf.Min(_save.level, MaxPlayable), true);
            _audio.SetSfx(_save.sfx);
            _audio.SetMusic(_save.music);
            ShowHomeScreen(false);
        }

        Vector3 P(float x, float y) => new Vector3((x - 195f) / 100f, (_hs * 0.5f - _insetTop - y) / 100f, 0f);
        Vector2 D(Vector3 w) => new Vector2(w.x * 100f + 195f, _hs * 0.5f - _insetTop - w.y * 100f);

        void Relayout(bool force)
        {
            var scr = new Vector2Int(Screen.width, Screen.height);
            var sa = Screen.safeArea;
            if (!force && scr == _lastScreen && sa == _lastSafe) return;
            _lastScreen = scr; _lastSafe = sa;
            float aspect = (float)Screen.width / Screen.height;
            _hs = W / aspect; _vw = W;
            if (_hs < 760f) { _hs = 760f; _vw = 760f * aspect; }
            _ox = (_vw - W) / 2f;
            _cam.orthographicSize = _hs / 200f;
            _insetTop = (Screen.height - sa.yMax) / Screen.height * _hs;
            float insetBottom = sa.yMin / Screen.height * _hs;
            _h = _hs - _insetTop - insetBottom;
            float extra = Mathf.Max(0, _h - 760f);
            _branchY = 158f + Mathf.Round(extra * 0.55f);
            _topY = _branchY + 6f;
            _homeY = Mathf.Round(_h - 194f);
            LayoutStatic();
        }

        SpriteRenderer Sr(string name, Transform parent, string sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? Res.Sprite(sprite) : null;
            sr.sortingOrder = order;
            return sr;
        }

        void BuildStatic()
        {
            _bg = backdrop; _bgFill = groundFill; _branch = branchSr; _hand = handSr;
            _hand.enabled = false;
            float[] cs = { 1f, 0.8f, 0.65f, 0.9f }, cv = { 6f, 4f, 7f, 5f };
            for (int i = 0; i < cloudSrs.Length && i < 4; i++) _clouds.Add((cloudSrs[i], cs[i], cv[i]));
            foreach (var m in moteSrs)
            {
                _motes.Add((m, UnityEngine.Random.value * 6.28f, 1f + UnityEngine.Random.value * 1.6f));
                _motePos.Add(new Vector2(UnityEngine.Random.value * W, 180 + UnityEngine.Random.value * 420));
            }
        }

        void LayoutStatic()
        {
            float s = _vw / W;
            float bottom = P(0, _h).y;
            _bg.transform.localScale = new Vector3(s, s, 1);
            _bg.transform.position = new Vector3(0, bottom + 8.44f * s / 2f, 0);
            float screenBottom = -_hs / 200f;
            float fillH = bottom - screenBottom + 1f;
            _bgFill.transform.position = new Vector3(0, screenBottom + fillH / 2f - 0.5f, 0);
            _bgFill.transform.localScale = new Vector3(_vw / 10f + 1f, (fillH + 1f) / 0.06f, 1);
            _branch.transform.position = P(195, _branchY);
            _branch.transform.localScale = new Vector3((374f + 2f * Mathf.Min(_ox, 30f)) / 396f, 1, 1);
        }

        void ApplyTheme(WorldDef w)
        {
            _world = w;
            _bg.sprite = Res.Sprite("bg_" + w.Theme);
            _cam.backgroundColor = w.SkyTop;
            _bgFill.color = w.Shelf;
        }

        // ======================================================================================= level
        void After(float sec, Action fn) => _timers.Add((_time + sec, fn));

        void NewGame(int n, bool snap)
        {
            _timers.Clear();
            if (_boardRoot != null) Destroy(_boardRoot.gameObject);
            _boardRoot = new GameObject("Board").transform;
            _flying.Clear(); _undo.Clear(); _ropes.Clear(); _homes.Clear(); _faces.Clear();
            _selSrc = null; _drag = null; _hint = null;
            _ui.HideTip();

            _data = LevelBank.Get(n);
            _n = n; _c = _data.Capacity; _k = _data.Species; _par = _data.Par;
            _moves = 0; _done = 0; _playTime = 0; _won = false; _stuckShown = false; _checkAt = 0; _starsShown = 3; _starPop = 0;
            _tutorial = n == 1 ? 1 : 0;
            _boost["undo"] = GameConfig.FreeUndo;
            _boost["hint"] = GameConfig.FreeHint;
            _boost["rope"] = n >= GameConfig.RopeUnlockLevel ? GameConfig.FreeRope : 0;
            ApplyTheme(Content.WorldForLevel(n));

            int r = _data.State.Length;
            _gap = _c <= 4 ? 64f : _c == 5 ? 58f : _c == 6 ? 52f : 46f;
            int maxN = r + (n >= GameConfig.RopeUnlockLevel ? 1 : 0);
            _size = Mathf.Min(62f, (W - 30f) / maxN * 0.98f, _gap * 1.08f);
            _ropeLen = 40f + (_c - 1) * _gap + 30f;
            for (int ri = 0; ri < r; ri++)
            {
                var rope = MakeRope(ri * 0.06f);
                for (int j = 0; j < _data.State[ri].Length; j++)
                {
                    int k = _data.State[ri][j];
                    rope.Items.Add(MakePal(Content.Pal(_data.PalIds[k]), k, ri * 0.06f + j * 0.05f + 0.2f));
                }
                _ropes.Add(rope);
            }
            _homeW = Mathf.Min(72f, (W - 24f) / _k - 8f);
            for (int i = 0; i < _k; i++) _homes.Add(MakeHome(Content.Pal(_data.PalIds[i]), i, W / 2f + (i - (_k - 1) / 2f) * (_homeW + 8f)));
            LayoutRopes(true);
            foreach (var rope in _ropes)
            {
                if (snap) { rope.Drop = 1; rope.DropDelay = 0; }
                for (int j = 0; j < rope.Items.Count; j++)
                {
                    var c = rope.Items[j];
                    if (snap) { c.Delay = 0; c.Scale = 1; }
                    var p = SlotPos(rope, j);
                    c.X = p.x; c.Y = snap ? p.y : _topY + 10;
                }
            }
            Debug.Log($"[DanglePals] Level {n}: {_k} species x {_c}, {r} ropes, par {_par}, tier {_data.Tier}{(_data.Hard ? " (hard)" : "")}, world {_world.Name}");
        }

        Rope MakeRope(float delay)
        {
            var rope = new Rope { Id = _ropeSeq++, X = W / 2, Tx = W / 2, DropDelay = delay };
            rope.Root = new GameObject("Rope " + rope.Id).transform;
            rope.Root.SetParent(_boardRoot, false);
            rope.Line = Sr("line", rope.Root, "rope", 20);
            rope.Line.drawMode = SpriteDrawMode.Tiled;
            rope.Line.tileMode = SpriteTileMode.Continuous;
            rope.Bead = Sr("bead", rope.Root, "bead", 21);
            rope.Knot = Sr("knot", rope.Root, "knot", 26);
            rope.Pill = Sr("highlight", rope.Root, "ui_pill", 15);
            rope.Pill.drawMode = SpriteDrawMode.Sliced;
            rope.Pill.enabled = false;
            return rope;
        }

        Pal MakePal(PalDef sp, int k, float delay)
        {
            var c = new Pal { Id = _palSeq++, Sp = sp, K = k, Delay = delay, BlinkIn = 1f + UnityEngine.Random.value * 4f };
            c.Root = new GameObject(sp.Id).transform;
            c.Root.SetParent(_boardRoot, false);
            c.GlowSr = Sr("glow", c.Root, "fx_glow", 29);
            c.GlowSr.transform.localScale = Vector3.one * 1.9375f;
            c.Img = Sr("img", c.Root, null, 30);
            c.Img.transform.localPosition = new Vector3(0, 0.067f, 0);
            var faces = new Sprite[4];
            for (int i = 0; i < 4; i++) faces[i] = Res.Pal(sp.Id, Moods[i]);
            _faces[c] = faces;
            return c;
        }

        Home MakeHome(PalDef sp, int k, float x)
        {
            var h = new Home { Sp = sp, K = k, X = x, Y = _homeY };
            h.Root = new GameObject("Home " + sp.Id).transform;
            h.Root.SetParent(_boardRoot, false);
            h.Body = new GameObject("body").transform;
            h.Body.SetParent(h.Root, false);
            // Each home gets its own sorting band and mask range, so masked door pals never batch together.
            int b = -60 + k * 6; // above the scenery (-80..-100), below ropes and pals
            var back = Sr("back", h.Body, "hut_back", b);
            back.transform.localPosition = new Vector3(0, 0.37f, 0);
            var maskGo = new GameObject("door mask");
            maskGo.transform.SetParent(h.Body, false);
            maskGo.transform.localPosition = new Vector3(0, 0.37f, 0);
            var mask = maskGo.AddComponent<SpriteMask>();
            mask.sprite = Res.Sprite("hut_mask");
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = b;
            mask.frontSortingOrder = b + 2;
            h.Glow = Sr("glow", h.Body, "fx_glow", b + 1);
            h.Glow.transform.localPosition = new Vector3(0, 0.19f, 0);
            h.Glow.transform.localScale = Vector3.one * 0.94f;
            h.Glow.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            h.PalSr = Sr("pal", h.Body, null, b + 2);
            h.PalSr.sprite = Res.Pal(sp.Id, "idle");
            h.PalSr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            h.PalSr.transform.localScale = Vector3.one * 0.4752f;
            var front = Sr("front", h.Body, "hut_front_" + (k % 3), b + 3);
            front.transform.localPosition = new Vector3(0, 0.37f, 0);
            h.Badge = Sr("heart", h.Body, "heart_badge", b + 4);
            h.Badge.transform.localPosition = new Vector3(0.3024f, 0.6563f, 0);
            for (int i = 0; i < _c; i++)
            {
                var dot = Sr("dot", _boardRoot, "ui_circle", 8);
                dot.transform.localScale = Vector3.one * 0.165f;
                var ring = Sr("ring", dot.transform, "ui_circle", 7);
                ring.transform.localScale = Vector3.one * 1.18f;
                ring.color = new Color(0.35f, 0.24f, 0.12f, 0.35f);
                h.Dots.Add(dot);
            }
            return h;
        }

        void LayoutRopes(bool snap)
        {
            int n = _ropes.Count;
            _spacing = Mathf.Min(74f, (W - 30f) / Mathf.Max(n, 1));
            for (int i = 0; i < n; i++) { _ropes[i].Tx = W / 2f + (i - (n - 1) / 2f) * _spacing; if (snap) _ropes[i].X = _ropes[i].Tx; }
        }

        Vector2 SlotPos(Rope r, int j)
        {
            float d = (40f + j * _gap) * Ease.OutBack(Mathf.Clamp01(r.Drop));
            return new Vector2(r.X + Mathf.Sin(r.Ang) * d, _topY + Mathf.Cos(r.Ang) * d);
        }

        int[][] Ints()
        {
            var st = new int[_ropes.Count][];
            for (int i = 0; i < _ropes.Count; i++) { st[i] = new int[_ropes[i].Items.Count]; for (int j = 0; j < st[i].Length; j++) st[i][j] = _ropes[i].Items[j].K; }
            return st;
        }

        int Stars() => _moves <= _par + 2 ? 3 : _moves <= Mathf.CeilToInt(_par * 1.5f) + 3 ? 2 : 1;
        int BoostCount(string id) => (_boost.TryGetValue(id, out var v) ? v : 0) + _save.Stock(id);
        void ConsumeBoost(string id)
        {
            if (_boost[id] > 0) _boost[id]--;
            else if (_save.Stock(id) > 0) { _save.AddStock(id, -1); _save.Save(); }
        }
        static bool IsDone(Rope r, int c)
        {
            if (r.Items.Count != c) return false;
            for (int i = 1; i < r.Items.Count; i++) if (r.Items[i].K != r.Items[0].K) return false;
            return true;
        }

        // ======================================================================================= flow
        void HandleClick(string id)
        {
            if (id.StartsWith("boost_")) { if (!Paused) UseBoost(id.Substring(6)); return; }
            if (id.StartsWith("buy_")) { Buy(id.Substring(4)); return; }
            switch (id)
            {
                case "play": Play(); break;
                case "world_go": _ui.HideSheet(); StartLevel(); break;
                case "pause": OpenPause(); break;
                case "settings": _ui.ShowPause(_save.sfx, _save.music, _ads.PrivacyOptionsRequired, false); break;
                case "resume": _ui.HideSheet(); break;
                case "restart": _ui.HideSheet(); Restart(); break;
                case "home": _ui.HideSheet(); NewGame(_n, true); ShowHomeScreen(false); break;
                case "shop": OpenShop(); break;
                case "shop_close": case "refill_close": _ui.HideSheet(); break;
                case "privacy": _ads.ShowPrivacyOptions(); break;
                case "toggle_sfx": _save.sfx = _ui.SfxOn; _save.Save(); _audio.SetSfx(_save.sfx); break;
                case "toggle_music": _save.music = _ui.MusicOn; _save.Save(); _audio.SetMusic(_save.music); break;
                case "stuck_undo": _ui.HideSheet(); _stuckShown = false; UseBoost("undo"); break;
                case "stuck_rope": _ui.HideSheet(); _stuckShown = false; UseBoost("rope"); break;
                case "stuck_restart": _ui.HideSheet(); _save.streak = 0; _save.Save(); NewGame(_n, false); StartLevel(); break;
                case "refill_video":
                {
                    var rid = _refillId;
                    _ui.HideSheet();
                    _ads.ShowRewarded("refill_" + rid, ok => { if (ok) { _audio.Coin(); GrantBoost(rid); } else _ui.Toast("No video available right now. Try again soon."); });
                    break;
                }
                case "refill_coins":
                {
                    var rid = _refillId; int price = GameConfig.Price(rid);
                    if (_save.coins < price) return;
                    _save.coins -= price; _save.Save(); _audio.Coin();
                    _ui.HideSheet();
                    GrantBoost(rid);
                    break;
                }
                case "restore":
                    _iap.Restore((ok, noAds) =>
                    {
                        if (!ok) { _ui.Toast("Could not reach the store. Try again later."); return; }
                        if (noAds) { _save.noAds = true; _save.Save(); }
                        _ui.Toast(noAds ? "Purchases restored. No Ads is on." : "No purchases to restore on this account.");
                    });
                    break;
                case "next": NextLevel(false); break;
                case "replay": NextLevel(true); break;
                case "double":
                    _ui.SetDoubleBusy(true);
                    _ads.ShowRewarded("double_coins", ok =>
                    {
                        if (ok) { _save.coins += _winReward; _save.Save(); _ui.SetWinCoins(_winReward * 2); _audio.Coin(); _audio.Coin(); }
                        else { _ui.SetDoubleBusy(false); _ui.Toast("No video available right now."); }
                    });
                    break;
            }
        }

        /// <summary>Home: the current scene card, its revealed tiles, and the big Level button.</summary>
        void ShowHomeScreen(bool animate)
        {
            _boardRoot.gameObject.SetActive(false);
            _branch.enabled = false;
            _hand.enabled = false;
            _ui.HideTip();
            int wi = Content.WorldIndexForLevel(_n);
            var w = Content.World(wi);
            ApplyTheme(w);
            _ui.ShowHome(wi + 1, w, _n - w.StartLevel, Content.WorldLength(wi), _n, _data.Hard, animate);
        }

        void Play()
        {
            _ui.HideHome();
            _boardRoot.gameObject.SetActive(true);
            _branch.enabled = true;
            NewGame(_n, false);
            MaybeWorldThenStart();
        }

        /// <summary>After a win: back home, reveal the new tile. Finishing a scene shows it complete, then the next one.</summary>
        void ContinueAfterWin(int finishedLevel)
        {
            if (finishedLevel + 1 > MaxPlayable)
            {
                // Level limit reached: stay on the last allowed level, no level 11.
                NewGame(MaxPlayable, true);
                ShowHomeScreen(false);
                _ui.Toast("You finished all " + MaxPlayable + " levels!");
                return;
            }
            int prevWorld = Content.WorldIndexForLevel(finishedLevel);
            NewGame(finishedLevel + 1, true);
            int nextWorld = Content.WorldIndexForLevel(_n);
            if (nextWorld != prevWorld)
            {
                var pw = Content.World(prevWorld);
                _boardRoot.gameObject.SetActive(false);
                _branch.enabled = false;
                ApplyTheme(pw);
                int len = Content.WorldLength(prevWorld);
                _ui.ShowHome(prevWorld + 1, pw, len, len, _n, false, true);
                After(2.6f, () => { if (_ui.HomeOpen) { _audio.Fanfare(); ShowHomeScreen(false); } });
                return;
            }
            ShowHomeScreen(true);
        }

        void MaybeWorldThenStart()
        {
            int wi = Content.WorldIndexForLevel(_n);
            if (wi > _save.worldSeen)
            {
                _save.worldSeen = wi; _save.Save();
                var w = Content.World(wi);
                _audio.Fanfare();
                _ui.ShowWorld(w, w.NewPals);
                return;
            }
            StartLevel();
        }

        void StartLevel()
        {
            _started = true;
            _lastAct = _time;
            if (_tutorial == 1) After(0.9f, () => { if (_tutorial == 1 && ShowHint()) _ui.ShowTip("Drag the glowing pals to the green rope", W / 2, _branchY - 20, 99f); });
            // Teach the one rule that isn't obvious: a pal in the middle takes everyone below it along.
            else if (_n == 2) After(0.9f, () => _ui.ShowTip("Grab ANY pal: the pals below it come along!", W / 2, _branchY - 20, 4.5f));
            else if (_n == 3) After(0.9f, () => _ui.ShowTip("Drop on a matching bottom pal or an empty rope", W / 2, _branchY - 20, 4.5f));
        }

        void Restart()
        {
            if (_moves > 0) { _save.streak = 0; _save.Save(); }
            NewGame(_n, false);
            StartLevel();
        }

        void NextLevel(bool replay)
        {
            _ui.HideWin();
            int finished = _n;
            if (replay) { NewGame(_n, false); StartLevel(); return; }
            void Go() => ContinueAfterWin(finished);
            if (!_save.noAds && finished + 1 >= GameConfig.InterstitialFromLevel && _save.winsSinceAd >= GameConfig.InterstitialEvery)
            {
                _save.winsSinceAd = 0; _save.Save();
                _ads.ShowInterstitial("level_end", Go);
            }
            else Go();
        }

        void OpenPause()
        {
            if (_ui.SheetOpen || _ui.WinOpen) return;
            _ui.ShowPause(_save.sfx, _save.music, _ads.PrivacyOptionsRequired, true);
        }

        void OpenShop()
        {
            if (_ui.WinOpen) return;
            _ui.ShowShop(id => _iap.Available ? _iap.Price(id) : null,
                id => { var p = Array.Find(GameConfig.Products, x => x.Id == id); return p.NoAds && p.Coins == 0 && _save.noAds; },
                _iap.Available);
        }

        void Buy(string id)
        {
            var p = Array.Find(GameConfig.Products, x => x.Id == id);
            _iap.Buy(id, res =>
            {
                if (res.Ok)
                {
                    if (p.NoAds || res.NoAds) _save.noAds = true;
                    if (p.Coins > 0) { _save.coins += p.Coins; _fx.Coins(W / 2, _h / 2, Mathf.Min(12, p.Coins / 100), () => _ui.CoinTargetDesign, CoinArrive); }
                    _save.AddStock("undo", p.Undo); _save.AddStock("hint", p.Hint); _save.AddStock("rope", p.Rope);
                    _save.Save();
                    _audio.Fanfare();
                    _ui.Toast("Thank you! " + p.Title + " added.");
                    OpenShop();
                }
                else if (!res.Cancelled) _ui.Toast("Purchase did not go through. Please try again.");
            });
        }

        void CoinArrive() { _coinBump = 1; _audio.Coin(); }

        void OpenRefill(string id)
        {
            _refillId = id;
            string name = id == "undo" ? "Undo" : id == "hint" ? "Hint" : "Spare Rope";
            int price = GameConfig.Price(id);
            _ui.ShowRefill("+1 " + name, price, _save.coins >= price);
        }

        void GrantBoost(string id) { _boost[id] = _boost[id] + 1; UseBoost(id); }

        // ======================================================================================= input
        static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        static bool OverUi(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            Hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screen }, Hits);
            return Hits.Count > 0;
        }

        void HandleInput()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
            var ptr = Pointer.current;
            if (ptr == null) return;
            Vector2 sp = ptr.position.ReadValue();
            var w = _cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -_cam.transform.position.z));
            var d = D(w);
            if (ptr.press.wasPressedThisFrame) { if (!OverUi(sp)) OnDown(d); }
            else if (ptr.press.isPressed) OnMove(d);
            if (ptr.press.wasReleasedThisFrame) OnUp();
        }

        void Back()
        {
            if (_adShowing || _ui.WinOpen) return;
            if (_ui.SheetOpen)
            {
                var k = _ui.SheetKind;
                if (k == "pause" || k == "shop" || k == "refill") _ui.HideSheet();
                return;
            }
            if (_ui.HomeOpen) { Application.Quit(); return; }
            if (_started) OpenPause();
        }

        (Rope r, int j)? HitPal(float x, float y)
        {
            foreach (var r in _ropes)
            {
                if (r.Completing || r.Retracting || r.Drop < 0.9f) continue;
                for (int j = r.Items.Count - 1; j >= 0; j--)
                {
                    var c = r.Items[j];
                    if (c.State != PalState.Rope) continue;
                    float dx = x - c.X, dy = y - c.Y;
                    if (dx * dx + dy * dy < Mathf.Pow(_size * 0.5f, 2)) return (r, j);
                }
            }
            return null;
        }

        Rope RopeAt(float x, float y)
        {
            if (y < _branchY - 30 || y > _homeY - 30) return null;
            Rope best = null; float bd = 1e9f;
            foreach (var r in _ropes)
            {
                if (r.Retracting) continue;
                float d = Mathf.Abs(x - r.X);
                if (d < _spacing * 0.55f && d < bd) { bd = d; best = r; }
            }
            return best;
        }

        bool CanDrop(Rope r, List<Pal> chain)
        {
            if (r.Completing || r.Retracting) return false;
            if (r.Items.Count + chain.Count > _c) return false;
            return r.Items.Count == 0 || r.Items[r.Items.Count - 1].Sp == chain[0].Sp;
        }

        void OnDown(Vector2 p)
        {
            if (!_started || _won || Paused) return;
            _pointer = p;
            _lastAct = _time;
            var hit = HitPal(p.x, p.y);
            if (_selSrc != null)
            {
                var src = _selSrc;
                var target = hit.HasValue ? hit.Value.r : RopeAt(p.x, p.y);
                if (target != null && target != src) { TryMove(src, _selIdx, target, src.Items.GetRange(_selIdx, src.Items.Count - _selIdx)); return; }
                if (!hit.HasValue) { Deselect(); return; }
                if (hit.Value.r == src && hit.Value.j == _selIdx) { Deselect(); return; }
                Deselect();
            }
            if (!hit.HasValue) return;
            var (rope, j) = hit.Value;
            var chain = rope.Items.GetRange(j, rope.Items.Count - j);
            _hint = null; _hand.enabled = false;
            for (int k = 0; k < chain.Count; k++) { var c = chain[k]; c.State = PalState.Held; c.ScaleT = k == 0 ? 1.12f : 1.04f; c.GlowT = 1; c.Fly = null; }
            chain[0].Mood = "wow"; chain[0].MoodT = 0.6f;
            _drag = new DragState { Src = rope, Idx = j, Chain = chain, Sx = p.x, Sy = p.y, OffX = chain[0].X - p.x, OffY = chain[0].Y - p.y };
            rope.AngV += (UnityEngine.Random.value < 0.5f ? -1 : 1) * 0.25f;
            _audio.Pick(chain[0].Sp);
            Haptics.Light();
        }

        void OnMove(Vector2 p)
        {
            if (_drag == null) return;
            _pointer = p;
            if (!_drag.Moved && Vector2.Distance(p, new Vector2(_drag.Sx, _drag.Sy)) > 8) _drag.Moved = true;
        }

        void OnUp()
        {
            if (_drag == null) return;
            var d = _drag; _drag = null;
            if (d.Moved)
            {
                var t = RopeAt(_pointer.x, _pointer.y);
                if (t != null && t != d.Src) { TryMove(d.Src, d.Idx, t, d.Chain); return; }
                foreach (var c in d.Chain) { c.State = PalState.Rope; c.ScaleT = 1; c.GlowT = 0; StartFly(c, 0.3f, 0, Ease.OutBack); }
                return;
            }
            foreach (var c in d.Chain) { c.State = PalState.Rope; c.ScaleT = 1.06f; c.GlowT = 1; }
            d.Chain[0].ScaleT = 1.12f;
            _selSrc = d.Src; _selIdx = d.Idx;
        }

        void Deselect()
        {
            if (_selSrc == null) return;
            for (int i = _selIdx; i < _selSrc.Items.Count; i++) { _selSrc.Items[i].ScaleT = 1; _selSrc.Items[i].GlowT = 0; }
            _selSrc = null;
        }

        static void StartFly(Pal c, float dur, float arc, Func<float, float> ease) => c.Fly = new PalFly { Sx = c.X, Sy = c.Y, Dur = dur, Arc = arc, Ease = ease ?? Ease.InOut };

        // ======================================================================================= rules + juice
        bool TryMove(Rope src, int idx, Rope dst, List<Pal> chain)
        {
            _selSrc = null;
            if (!CanDrop(dst, chain) || src.Completing)
            {
                bool full = dst.Items.Count + chain.Count > _c;
                foreach (var c in chain) { c.State = PalState.Rope; c.ScaleT = 1; c.GlowT = 0; c.Shake = 0.6f; StartFly(c, 0.42f, 0, Ease.OutBack); }
                chain[0].Mood = "wow"; chain[0].MoodT = 0.5f;
                dst.Flash = 0.5f;
                _audio.Bad(); Haptics.Medium();
                _ui.ShowTip(full ? "That rope is full!" : "Match the bottom pal to hang here!", dst.X, _branchY - 20, 1.8f);
                return false;
            }
            PushUndo();
            src.Items.RemoveRange(idx, src.Items.Count - idx);
            dst.Items.AddRange(chain);
            _moves++;
            for (int k = 0; k < chain.Count; k++) { var c = chain[k]; c.State = PalState.Rope; c.ScaleT = 1; c.GlowT = 0; StartFly(c, 0.26f + k * 0.03f, 22, Ease.InOut); }
            int n = dst.Items.Count; var lead = chain[0];
            src.AngV += (dst.X > src.X ? -1 : 1) * 0.18f;
            After(0.26f, () =>
            {
                _audio.Drop(n); Haptics.Light();
                dst.AngV += (dst.X > src.X ? 1 : -1) * 0.22f; lead.SqV = -3.2f;
                _fx.Burst(lead.X, lead.Y + _size * 0.35f, 6, new[] { "dust" }, new[] { Color.white }, 70, 0.5f, 5, -20, 0, Mathf.PI);
            });
            _hint = null; _hand.enabled = false;
            if (_tutorial == 1)
            {
                _tutorial = 2; _ui.HideTip();
                After(0.6f, () => { if (_tutorial == 2) _ui.ShowTip("Fill a rope with " + _c + " of a kind!", W / 2, _branchY - 20, 3f); _tutorial = 0; });
            }
            int s = Stars(); if (s < _starsShown) { _starsShown = s; _starPop = 1; }
            if (IsDone(dst, _c)) { dst.Completing = true; After(0.32f, () => CompleteRope(dst)); }
            _checkAt = _time + 0.5f;
            return true;
        }

        void PushUndo()
        {
            var snap = new Snapshot { Moves = _moves };
            foreach (var r in _ropes) snap.Ropes.Add((r.Id, new List<Pal>(r.Items)));
            _undo.Add(snap);
            if (_undo.Count > 40) _undo.RemoveAt(0);
        }

        void CompleteRope(Rope r)
        {
            _undo.Clear(); _done++;
            int tier = Mathf.Clamp(_done - 1, 0, 4);
            var home = _homes.Find(h => h.Sp == r.Items[0].Sp);
            var col = r.Items[0].Sp.Col;
            for (int j = 0; j < r.Items.Count; j++)
            {
                var c = r.Items[j]; int jj = j;
                c.Mood = "happy"; c.MoodT = 9;
                After(j * 0.07f, () =>
                {
                    c.Hop = 1; _audio.Cheer(jj);
                    _fx.Burst(c.X, c.Y, 7, new[] { "heart", "petal", "spark" }, new[] { col, Res.Hex("#FF7E9A"), Color.white }, 170, 0.9f, 6);
                });
            }
            After(0.12f, () => { _ui.ShowPraise(tier, Mathf.Clamp(r.X, 90, W - 90), _branchY - 6); _audio.Praise(tier); _audio.Shimmer(); Haptics.Medium(); });
            int count = r.Items.Count;
            After(0.6f, () =>
            {
                _audio.Whoosh();
                var list = new List<Pal>(r.Items); list.Reverse();
                for (int k = 0; k < list.Count; k++)
                {
                    var c = list[k];
                    After(k * 0.09f, () =>
                    {
                        c.State = PalState.Home;
                        r.Items.Remove(c);
                        float ex = home.X, ey = home.Y - 6;
                        c.Hf = new HomeFly { Sx = c.X, Sy = c.Y, Cx = (c.X + ex) / 2 + (ex < c.X ? -40 : 40), Cy = Mathf.Min(c.Y, ey) - 60, Ex = ex, Dur = 0.5f, Home = home };
                        _flying.Add(c);
                    });
                }
            });
            After(0.6f + count * 0.09f + 0.25f, () => r.Retracting = true);
        }

        void ArriveHome(Pal c)
        {
            var h = c.Hf.Home;
            h.Count++; h.SqV = -3.4f;
            _audio.HomePop(h.Count - 1);
            _fx.Burst(h.X, h.Y - 10, 6, new[] { "spark" }, new[] { c.Sp.Col, Res.Hex("#FFF6B0") }, 120, 0.6f, 5);
            c.Root.gameObject.SetActive(false);
            if (h.Count >= _c)
            {
                h.Filled = true; h.FillT = 0;
                After(0.12f, () =>
                {
                    _audio.HomeFilled();
                    _fx.Ring(h.X, h.Y - 12, Res.Hex("#FFE27A"), _homeW * 0.8f);
                    _fx.Burst(h.X, h.Y - 20, 14, new[] { "spark", "heart" }, new[] { Res.Hex("#FFE066"), Res.Hex("#FF8FB1"), c.Sp.Col }, 200, 1f, 6);
                    _fx.Coins(h.X, h.Y - 20, 5, () => _ui.CoinTargetDesign, CoinArrive);
                    _save.coins += GameConfig.CoinsPerHome; _save.Save();
                });
                if (_homes.TrueForAll(x => x.Filled)) After(1.1f, WinLevel);
            }
        }

        void UseBoost(string id)
        {
            _audio.Tap();
            if (_drag != null) return;
            if (id == "restart") { Restart(); return; }
            if (id == "undo")
            {
                if (_undo.Count == 0) { _ui.ShowTip("Nothing to undo yet", W / 2, _branchY - 20, 1.8f); return; }
                if (_ropes.Exists(r => r.Completing)) return;
                if (BoostCount("undo") <= 0) { OpenRefill("undo"); return; }
                Deselect();
                var snap = _undo[_undo.Count - 1]; _undo.RemoveAt(_undo.Count - 1);
                foreach (var r in _ropes)
                {
                    var entry = snap.Ropes.Find(e => e.id == r.Id);
                    var items = entry.items ?? new List<Pal>();
                    for (int i = 0; i < items.Count; i++) if (r.Items.IndexOf(items[i]) != i) StartFly(items[i], 0.3f, 18, Ease.InOut);
                    r.Items = new List<Pal>(items);
                }
                _moves = snap.Moves; ConsumeBoost("undo"); _audio.Undo(); _hint = null; _hand.enabled = false;
                if (Stars() > _starsShown) _starsShown = Stars();
                _checkAt = _time + 0.5f;
                return;
            }
            if (id == "hint")
            {
                if (_ropes.Exists(r => r.Completing)) return;
                if (BoostCount("hint") <= 0) { OpenRefill("hint"); return; }
                if (ShowHint()) ConsumeBoost("hint");
                return;
            }
            if (id == "rope")
            {
                if (_n < GameConfig.RopeUnlockLevel) { _ui.ShowTip("Spare rope unlocks at level " + GameConfig.RopeUnlockLevel, W / 2, _branchY - 20, 1.8f); return; }
                if (BoostCount("rope") <= 0) { OpenRefill("rope"); return; }
                ConsumeBoost("rope");
                var nr = MakeRope(0);
                _ropes.Add(nr);
                LayoutRopes(false);
                nr.X = nr.Tx;
                _audio.RopeAdded();
                _checkAt = _time + 0.6f;
                _stuckShown = false;
                if (_ui.SheetKind == "stuck") _ui.HideSheet();
            }
        }

        bool ShowHint()
        {
            Deselect();
            var sol = Puzzle.Solve(Ints(), _c, 30000, out _, out bool exhausted);
            if (sol == null || sol.Count == 0)
            {
                // Only claim "no way out" when it is proven; a big board can simply outrun the hint budget.
                _ui.ShowTip(exhausted ? "No way out from here. Try Undo or Restart." : "Try freeing up a rope first!", W / 2, _branchY - 20, 2.4f);
                return false;
            }
            var mv = sol[0];
            _hint = new HintState { Src = _ropes[mv.Src], Idx = mv.Idx, Dst = _ropes[mv.Dst] };
            _audio.Hint();
            return true;
        }

        void CheckStuck()
        {
            if (_won || _stuckShown || _ropes.Exists(r => r.Completing) || _flying.Count > 0) return;
            if (_homes.TrueForAll(h => h.Filled)) return;
            // Stuck means no move at all, OR a dead end: moves exist (e.g. one pal swapping back and forth)
            // but no sequence of them can ever win. The solver proves the latter by visiting every
            // reachable position; if it runs out of budget first we stay quiet rather than guess.
            var st = Ints();
            if (Puzzle.GenMoves(st, _c).Count > 0)
            {
                var sol = Puzzle.Solve(st, _c, 6000, out _, out bool exhausted);
                if (sol != null || !exhausted) return;
            }
            _stuckShown = true;
            _audio.Stuck();
            foreach (var r in _ropes) foreach (var c in r.Items) { c.Mood = "wow"; c.MoodT = 2; }
            _ui.ShowStuck(_undo.Count > 0, _n >= GameConfig.RopeUnlockLevel);
        }

        void WinLevel()
        {
            if (_won) return;
            _won = true;
            int s = Stars();
            _save.streak++;
            _winReward = 20 + s * 10;
            _save.coins += _winReward;
            _save.level = _n + 1;
            _save.winsSinceAd++;
            _save.Save();
            int t = Mathf.RoundToInt(_playTime);
            int diff = _par - _moves;
            var ids = new string[_k];
            for (int i = 0; i < _k; i++) ids[i] = _data.PalIds[i];
            _ui.ShowWin(new WinData
            {
                Title = s == 3 ? "Superb!" : s == 2 ? "Great job!" : "Sorted!",
                Stars = s, Pals = ids, Moves = _moves, Streak = _save.streak, Reward = _winReward,
                Time = (t / 60) + ":" + (t % 60).ToString("00"),
                TimeGrade = t <= _k * 14 ? "A" : t <= _k * 28 ? "B" : "C",
                MovesGrade = _moves <= _par ? "S" : _moves <= _par + 2 ? "A" : _moves <= Mathf.CeilToInt(_par * 1.5f) + 3 ? "B" : "C",
                Note = diff > 0 ? $"Clever paws! {diff} move{(diff > 1 ? "s" : "")} under par ({_par})." : diff == 0 ? $"Right on par ({_par} moves). Tidy!" : $"Par is {_par} moves. Can you get closer?",
            });
            _audio.Fanfare();
            Haptics.Heavy();
        }

        // ======================================================================================= per frame
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            Relayout(false);
            HandleInput();
            Step(dt);
            Render();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            _save?.Save();
            if (_started && !_won && _ui != null && !_ui.SheetOpen && !_ui.WinOpen && !_ui.HomeOpen && !_adShowing) OpenPause();
        }

        void Step(float dt)
        {
            _time += dt;
            for (int i = _timers.Count - 1; i >= 0; i--)
                if (_timers[i].t <= _time) { var f = _timers[i].fn; _timers.RemoveAt(i); f(); }

            for (int i = 0; i < _cloudX.Length; i++) { _cloudX[i] += _clouds[i].v * dt; if (_cloudX[i] > W + _ox + 60) _cloudX[i] = -_ox - 80; }
            for (int i = 0; i < _motes.Count; i++)
            {
                var (sr, p, s) = _motes[i];
                p += dt * 0.6f;
                var pos = _motePos[i];
                pos.y -= dt * 6f * s; pos.x += Mathf.Sin(p) * 0.2f;
                if (pos.y < 170) { pos.y = _homeY; pos.x = UnityEngine.Random.value * W; }
                _motePos[i] = pos; _motes[i] = (sr, p, s);
            }
            if (_data == null) return;
            if (_started && !Paused && !_won) _playTime += dt;
            if (_hint != null) _hint.T += dt;
            _starPop = Mathf.Max(0, _starPop - dt * 2.5f);
            _coinsShown += (_save.coins - _coinsShown) * Ease.Damp(6, dt);
            if (Mathf.Abs(_save.coins - _coinsShown) < 0.5f) _coinsShown = _save.coins;
            _coinBump = Mathf.Max(0, _coinBump - dt * 4);

            bool removed = false;
            foreach (var r in _ropes)
            {
                r.X += (r.Tx - r.X) * Ease.Damp(10, dt);
                if (r.DropDelay > 0) r.DropDelay -= dt; else if (r.Drop < 1) r.Drop = Mathf.Min(1, r.Drop + dt / 0.6f);
                float breeze = 0.01f * Mathf.Sin(_time * 0.9f + r.Id * 1.7f);
                r.AngV += (-(r.Ang - breeze) * 36f - r.AngV * 4.2f) * dt;
                r.Ang = Mathf.Clamp(r.Ang + r.AngV * dt, -0.35f, 0.35f);
                if (r.Flash > 0) r.Flash -= dt;
                if (r.Retracting) { r.Retract = Mathf.Min(1, r.Retract + dt / 0.35f); if (r.Retract >= 1) { r.Dead = true; removed = true; } }
                int lifted = _selSrc == r ? _selIdx : 99;
                for (int j = 0; j < r.Items.Count; j++)
                {
                    var c = r.Items[j];
                    if (c.State != PalState.Rope) continue;
                    var p = SlotPos(r, j);
                    float tx = p.x, ty = p.y - (j >= lifted ? 12 : 0);
                    float trot = -r.Ang * 0.9f + 0.03f * Mathf.Sin(_time * 1.3f + c.Id);
                    if (c.Fly != null)
                    {
                        c.Fly.T += dt;
                        float u = Mathf.Clamp01(c.Fly.T / c.Fly.Dur), e = c.Fly.Ease(u);
                        c.X = Mathf.LerpUnclamped(c.Fly.Sx, tx, e);
                        c.Y = Mathf.LerpUnclamped(c.Fly.Sy, ty, e) - c.Fly.Arc * Mathf.Sin(Mathf.PI * u);
                        if (u >= 1) c.Fly = null;
                    }
                    else { float k = Ease.Damp(20, dt); c.X += (tx - c.X) * k; c.Y += (ty - c.Y) * k; }
                    c.Rot += (trot - c.Rot) * Ease.Damp(10, dt);
                }
            }
            if (removed)
            {
                foreach (var r in _ropes) if (r.Dead) Destroy(r.Root.gameObject);
                _ropes.RemoveAll(r => r.Dead);
                LayoutRopes(false);
                _checkAt = _time + 0.4f;
            }

            if (_drag != null)
            {
                var ch = _drag.Chain;
                for (int k = 0; k < ch.Count; k++)
                {
                    var c = ch[k];
                    float tx, ty;
                    if (k == 0) { tx = _pointer.x + _drag.OffX * 0.4f; ty = _pointer.y + _drag.OffY * 0.4f - 8; }
                    else { tx = ch[k - 1].X; ty = ch[k - 1].Y + _gap * 0.86f; }
                    float kk = k == 0 ? Ease.Damp(30, dt) : Ease.Damp(16, dt);
                    float ox = c.X;
                    c.X += (tx - c.X) * kk; c.Y += (ty - c.Y) * kk;
                    c.Rot += (Mathf.Clamp((c.X - ox) * 0.03f, -0.45f, 0.45f) - c.Rot) * Ease.Damp(12, dt);
                }
            }

            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var c = _flying[i]; var f = c.Hf;
                f.T += dt;
                float u = Mathf.Clamp01(f.T / f.Dur), e = Ease.InOut(u), ey = f.Home.Y - 6;
                c.X = (1 - e) * (1 - e) * f.Sx + 2 * (1 - e) * e * f.Cx + e * e * f.Ex;
                c.Y = (1 - e) * (1 - e) * f.Sy + 2 * (1 - e) * e * f.Cy + e * e * ey;
                c.ScaleT = 1 - 0.3f * u;
                c.Rot += dt * 4 * (f.Ex < f.Sx ? -1 : 1) * (1 - u);
                _fx.Trail(c.X, c.Y, c.Sp.Accent, _size * 0.21f);
                if (u >= 1) { _flying.RemoveAt(i); c.State = PalState.Gone; ArriveHome(c); }
            }

            List<Pal> hc = _hint != null && _ropes.Contains(_hint.Src) && _hint.Idx < _hint.Src.Items.Count ? _hint.Src.Items.GetRange(_hint.Idx, _hint.Src.Items.Count - _hint.Idx) : null;
            foreach (var r in _ropes) foreach (var c in r.Items) AnimatePal(c, dt, hc);
            foreach (var c in _flying) AnimatePal(c, dt, hc);

            foreach (var h in _homes) { h.Y = _homeY; h.SqV += ((1 - h.Sq) * 240 - h.SqV * 12) * dt; h.Sq += h.SqV * dt; if (h.Filled) h.FillT += dt; }
            if (_checkAt > 0 && _time > _checkAt) { _checkAt = 0; CheckStuck(); }

            // Early levels: if the player stops for a while, show the next good move for free.
            if (_started && !_won && !Paused && !_stuckShown && _n <= FreeIdleHintUpToLevel && _hint == null && _drag == null && _selSrc == null
                && _time - _lastAct > IdleHintAfter && _flying.Count == 0 && !_ropes.Exists(r => r.Completing || r.Retracting))
            {
                _lastAct = _time;
                if (ShowHint()) _ui.ShowTip("Psst! Try the glowing pals", W / 2, _branchY - 20, 2.5f);
            }
        }

        void AnimatePal(Pal c, float dt, List<Pal> hinted)
        {
            if (c.Delay > 0) { c.Delay -= dt; return; }
            c.ScV += ((c.ScaleT - c.Scale) * 220f - c.ScV * 13f) * dt; c.Scale = Mathf.Max(0, c.Scale + c.ScV * dt);
            c.SqV += ((1 - c.Sy) * 260f - c.SqV * 14f) * dt; c.Sy += c.SqV * dt; c.Sx = 1 + (1 - c.Sy) * 0.8f;
            float gt = Mathf.Max(c.GlowT, hinted != null && hinted.Contains(c) ? 0.5f + 0.5f * Mathf.Sin(_time * 6) : 0);
            c.Glow += (gt - c.Glow) * Ease.Damp(12, dt);
            if (c.Hop > 0) c.Hop = Mathf.Max(0, c.Hop - dt / 0.35f);
            if (c.Shake > 0) { c.Shake -= dt * 1.4f; c.Rot += Mathf.Sin(_time * 42) * 0.05f * c.Shake; }
            if (c.MoodT > 0) { c.MoodT -= dt; if (c.MoodT <= 0) c.Mood = "idle"; }
            c.BlinkIn -= dt; if (c.BlinkIn < 0) { c.Blink = 0.13f; c.BlinkIn = 2 + UnityEngine.Random.value * 4; }
            if (c.Blink > 0) c.Blink -= dt;
        }

        // ======================================================================================= draw
        void Render()
        {
            for (int i = 0; i < _clouds.Count; i++)
            {
                var (sr, s, _) = _clouds[i];
                sr.transform.position = P(_cloudX[i] + 20.5f * s, _cloudY[i] - 2.5f * s);
                sr.transform.localScale = new Vector3(s, s, 1);
            }
            for (int i = 0; i < _motes.Count; i++)
            {
                var (sr, _, s) = _motes[i];
                sr.transform.position = P(_motePos[i].x, _motePos[i].y);
                sr.transform.localScale = Vector3.one * (s * 2.6f / 24f);
            }
            if (_data == null) return;

            // drag highlight pills
            Rope src = null; List<Pal> chain = null; Rope hover = null;
            if (_drag != null && _drag.Moved) { src = _drag.Src; chain = _drag.Chain; hover = RopeAt(_pointer.x, _pointer.y); }
            else if (_selSrc != null) { src = _selSrc; chain = _selSrc.Items.GetRange(_selIdx, _selSrc.Items.Count - _selIdx); }
            float pw = Mathf.Min(_spacing * 0.86f, _size * 1.12f);

            foreach (var r in _ropes)
            {
                float len = _ropeLen * Ease.OutBack(Mathf.Clamp01(r.Drop)) * (1 - r.Retract);
                var a = P(r.X, _topY);
                var e = P(r.X + Mathf.Sin(r.Ang) * len, _topY + Mathf.Cos(r.Ang) * len);
                bool show = len >= 3;
                r.Line.enabled = show; r.Bead.enabled = show;
                if (show)
                {
                    var dir = e - a;
                    r.Line.transform.position = (a + e) / 2f;
                    r.Line.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg);
                    r.Line.size = new Vector2(0.046f, len / 100f);
                    r.Bead.transform.position = e;
                }
                r.Knot.enabled = !(r.Drop <= 0 && r.DropDelay > 0);
                r.Knot.transform.position = P(r.X, _branchY);

                Color? fill = null;
                if (chain != null) { if (r == src) fill = new Color(1, 1, 1, 0.34f); else if (CanDrop(r, chain)) fill = new Color(0.55f, 0.91f, 0.47f, r == hover ? 0.55f : 0.26f); }
                if (_hint != null && r == _hint.Dst) fill = new Color(0.55f, 0.91f, 0.47f, 0.25f + 0.2f * Mathf.Sin(_hint.T * 6));
                if (r.Flash > 0) fill = new Color(1f, 0.47f, 0.47f, r.Flash * 0.7f);
                r.Pill.enabled = fill.HasValue;
                if (fill.HasValue)
                {
                    r.Pill.color = fill.Value;
                    r.Pill.size = new Vector2(pw / 100f, (_ropeLen + 6) / 100f);
                    r.Pill.transform.position = P(r.X, _branchY + 16 + (_ropeLen + 6) / 2f);
                }

                for (int j = 0; j < r.Items.Count; j++)
                {
                    var c = r.Items[j];
                    bool lifted = c.State == PalState.Held || (r == _selSrc && j >= _selIdx) || c.Fly != null;
                    DrawPal(c, lifted ? 60 : 30);
                }
            }
            foreach (var c in _flying) DrawPal(c, 70);

            foreach (var h in _homes)
            {
                float hh = _homeW * 1.06f;
                h.Root.position = P(h.X, h.Y + hh * 0.5f);
                h.Root.localScale = Vector3.one * (_homeW / 72f);
                h.Body.localScale = new Vector3(1 + (1 - h.Sq) * 0.6f, h.Sq, 1);
                float pk = h.Filled ? Mathf.Min(1, h.FillT * 3) : 0;
                float off = h.Filled ? (1 - pk) * 11.88f - Mathf.Sin(h.FillT * 3) * 2 : 3.8f;
                h.PalSr.transform.localPosition = new Vector3(0, (19.08f - off) / 100f, 0);
                h.PalSr.sprite = Res.Pal(h.Sp.Id, h.Filled ? (Mathf.Sin(h.FillT * 1.7f) > 0.92f ? "blink" : "happy") : "idle");
                h.PalSr.color = new Color(1, 1, 1, h.Filled ? 1 : 0.32f);
                h.Glow.enabled = h.Filled;
                h.Badge.enabled = h.Filled;
                if (h.Filled) { float s = Mathf.Min(1, h.FillT * 4) * (1 + 0.08f * Mathf.Sin(h.FillT * 5)); h.Badge.transform.localScale = new Vector3(s, s, 1); }
                int n = h.Dots.Count;
                float dx = Mathf.Min(10, (_homeW - 6) / n), y0 = h.Y + hh * 0.5f + 11;
                for (int i = 0; i < n; i++)
                {
                    h.Dots[i].transform.position = P(h.X + (i - (n - 1) / 2f) * dx, y0);
                    h.Dots[i].color = i < h.Count ? h.Sp.Accent : new Color(1, 1, 1, 0.65f);
                }
            }

            DrawHand();
            _ui.SetHud(_n, _data.Hard, Mathf.RoundToInt(_coinsShown), _moves, _par, _starsShown, _starPop,
                _world.Name + "  ·  Rescued " + _homes.FindAll(h => h.Filled).Count + " / " + _k, _coinBump);
            _ui.SetBooster("undo", BoostCount("undo"), false);
            _ui.SetBooster("hint", BoostCount("hint"), false);
            _ui.SetBooster("rope", BoostCount("rope"), _n < GameConfig.RopeUnlockLevel);
            _ui.SetBooster("restart", 0, false);
        }

        void DrawPal(Pal c, int order)
        {
            bool vis = c.Delay <= 0 && c.Scale >= 0.02f && c.State != PalState.Gone;
            c.Img.enabled = vis;
            c.GlowSr.enabled = vis && c.Glow > 0.02f;
            if (!vis) return;
            int mood = c.Mood == "idle" ? (c.Blink > 0 ? 1 : 0) : c.Mood == "happy" ? 2 : c.Mood == "wow" ? 3 : 0;
            c.Img.sprite = _faces[c][mood];
            float s = _size * c.Scale;
            float y = c.Y - (c.Hop > 0 ? Mathf.Sin(Mathf.PI * (1 - c.Hop)) * 16 : 0);
            c.Root.position = P(c.X, y);
            c.Root.rotation = Quaternion.Euler(0, 0, -c.Rot * Mathf.Rad2Deg);
            c.Root.localScale = new Vector3(s / 100f * c.Sx, s / 100f * c.Sy, 1);
            c.Img.sortingOrder = order;
            c.GlowSr.sortingOrder = order - 1;
            c.GlowSr.color = new Color(1, 1, 1, c.Glow);
        }

        void DrawHand()
        {
            var h = _hint;
            if (h == null || !_ropes.Contains(h.Src) || !_ropes.Contains(h.Dst) || h.Idx >= h.Src.Items.Count) { _hand.enabled = false; return; }
            var c0 = h.Src.Items[h.Idx];
            const float T = 1.9f;
            float u = (h.T % T) / T;
            var end = SlotPos(h.Dst, h.Dst.Items.Count);
            float x, y, a = 1; bool press = false;
            if (u < 0.18f) { float e = Ease.OutCubic(u / 0.18f); x = c0.X + 40 * (1 - e); y = c0.Y + 60 * (1 - e); a = e; }
            else if (u < 0.28f) { x = c0.X; y = c0.Y; press = true; }
            else if (u < 0.75f) { float e = Ease.InOut((u - 0.28f) / 0.47f); x = Mathf.Lerp(c0.X, end.x, e); y = Mathf.Lerp(c0.Y, end.y, e) - Mathf.Sin(Mathf.PI * e) * 26; press = true; }
            else { x = end.x; y = end.y; a = 1 - (u - 0.75f) / 0.25f; }
            _hand.enabled = true;
            _hand.color = new Color(1, 1, 1, a);
            float s = press ? 0.9f : 1f;
            _hand.transform.position = P(x + 20 * s, y + 32 * s);
            _hand.transform.localScale = new Vector3(s, s, 1);
        }
    }
}
