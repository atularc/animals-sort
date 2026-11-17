using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DanglePals
{
    public sealed class WinData
    {
        public string Title, Time, TimeGrade, MovesGrade, Note;
        public int Stars, Moves, Streak, Reward;
        public string[] Pals;
    }

    /// <summary>
    /// Runtime side of the UI. The whole canvas (HUD, home, sheets, shop, win screen, pools) is authored in the
    /// scene by "Dangle Pals > Run Full Setup"; this component only shows/hides, fills in text and animates.
    /// Nothing is instantiated or loaded at runtime. Canvas units equal design pixels (390 wide).
    /// </summary>
    public sealed class GameUi : MonoBehaviour
    {
        [Serializable] public sealed class Bind { public string id; public Button button; }
        [Serializable] public sealed class KeyRefs { public string id; public Image bg; public GameObject badge, ring, lockTag; public Image badgeImg; public TextMeshProUGUI count; }
        [Serializable] public sealed class PraiseRefs { public RectTransform rt; public TextMeshProUGUI text; public Image s1, s2; }
        [Serializable] public sealed class SheetRefs { public string kind; public RectTransform panel; }
        [Serializable] public sealed class ShopRow { public string productId; public Image buttonImg; public Button button; public TextMeshProUGUI price; }

        public AudioManager audioManager;
        public List<Bind> binds = new List<Bind>();
        public RectTransform root, board;
        public GameObject scrim;

        [Header("HUD")]
        public GameObject hud;
        public TextMeshProUGUI levelText, coinsText, movesText, parText, rescuedText;
        public GameObject hardTag;
        public Image[] stars = new Image[3];
        public RectTransform rescuedPill, rescuedShadow, coinIcon;
        public List<KeyRefs> keys = new List<KeyRefs>();
        public Sprite starOn, starOff, starGold, starDark, keySprite, keyLockedSprite;

        [Header("Tip + praise")]
        public RectTransform tip, tipBg, tipShadow;
        public CanvasGroup tipGroup;
        public TextMeshProUGUI tipText;
        public List<PraiseRefs> praisePool = new List<PraiseRefs>();

        [Header("Sheets")]
        public List<SheetRefs> sheets = new List<SheetRefs>();
        public Image sfxTrack, musicTrack;
        public RectTransform sfxKnob, musicKnob;
        public GameObject privacyButton;
        public Button stuckUndo, stuckRope;
        public TextMeshProUGUI refillTitle, refillCoinsLabel;
        public Button refillCoins;
        public List<ShopRow> shopRows = new List<ShopRow>();
        public GameObject shopNote;
        public Sprite btnBlue, btnGhost;
        public TextMeshProUGUI worldEyebrow, worldName, worldBody;
        public List<Image> worldFaces = new List<Image>();
        public List<RectTransform> bobbers = new List<RectTransform>();

        [Header("Toast + ad")]
        public RectTransform toast;
        public TextMeshProUGUI toastText;
        public GameObject adCard;
        public TextMeshProUGUI adTitle;

        [Header("Win")]
        public RectTransform win, winRainbow, winTitleRt, habitat, winButtons;
        public CanvasGroup winGroup;
        public TextMeshProUGUI winTitle, wTime, wTimeG, wMoves, wMovesG, wStreak, wNote, wCoins;
        public Image[] wStars = new Image[3];
        public RectTransform[] cards = new RectTransform[3];
        public Button btnDouble;
        public List<Image> winPals = new List<Image>();
        public List<Image> confettiPool = new List<Image>();

        [Header("Home")]
        public GameObject home;
        public TextMeshProUGUI homeCoins, sceneTitle, sceneCounter, playLabel;
        public GameObject playHard;
        public Image scenePicture;
        public List<Image> scenePals = new List<Image>();
        public List<Image> sceneTileIcons = new List<Image>();
        public RectTransform sceneProgressFill;

        // cover-tile colours per world theme (two tones, checkerboard)
        static readonly string[,] TileTones =
        {
            { "#7FCB6E", "#95D985" }, { "#FFAE63", "#FFC285" }, { "#86BDEB", "#A3D0F3" },
            { "#4FC0B6", "#70D0C7" }, { "#6E64C2", "#857CD2" }, { "#FF95C4", "#FFB1D3" },
        };
        int _glowFrom, _glowTo, _glowScene;
        public List<Image> sceneTiles = new List<Image>();

        // callbacks (set by GameController)
        public Action<string> OnClick;
        public Func<float, float, Vector2> DesignToCanvas;

        public bool SheetOpen => SheetKind != null;
        public string SheetKind { get; private set; }
        public bool WinOpen => win != null && win.gameObject.activeSelf;
        public bool HomeOpen => home != null && home.activeSelf;
        public Vector2 CoinTargetDesign => new Vector2(291, 40);

        bool _sfx = true, _music = true;
        int _shownTiles = -1, _shownWorld = -1;
        Coroutine _sheetAnim;

        void Awake()
        {
            foreach (var b in binds)
            {
                var id = b.id;
                b.button.onClick.AddListener(() => Click(id));
            }
            scrim.SetActive(false);
            foreach (var s in sheets) s.panel.gameObject.SetActive(false);
            win.gameObject.SetActive(false);
            adCard.SetActive(false);
            toast.gameObject.SetActive(false);
            tip.gameObject.SetActive(false);
            foreach (var p in praisePool) p.rt.gameObject.SetActive(false);
            foreach (var c in confettiPool) c.gameObject.SetActive(false);
        }

        void Click(string id)
        {
            if (id == "toggle_sfx") { _sfx = !_sfx; PaintToggle(sfxTrack, sfxKnob, _sfx); }
            if (id == "toggle_music") { _music = !_music; PaintToggle(musicTrack, musicKnob, _music); }
            if (!id.StartsWith("boost_")) audioManager.Tap();
            OnClick?.Invoke(id);
        }

        public bool SfxOn => _sfx;
        public bool MusicOn => _music;

        // ---------------------------------------------------------------- HUD
        public void ShowHud(bool on) => hud.SetActive(on);

        public void SetHud(int level, bool hard, int coins, int moves, int par, int starCount, float starPop, string rescued, float coinBump)
        {
            levelText.text = "Level " + level;
            if (hardTag.activeSelf != hard) hardTag.SetActive(hard);
            coinsText.text = coins.ToString();
            movesText.text = "Moves " + moves;
            parText.text = "par " + par;
            for (int i = 0; i < 3; i++)
            {
                stars[i].sprite = i < starCount ? starOn : starOff;
                stars[i].rectTransform.localScale = Vector3.one * ((i == starCount && starPop > 0) ? 1 + starPop * 0.5f : 1f);
            }
            if (rescuedText.text != rescued)
            {
                rescuedText.text = rescued;
                float w = rescuedText.GetPreferredValues(rescued).x + 24;
                rescuedPill.sizeDelta = new Vector2(w, 22);
                rescuedShadow.sizeDelta = new Vector2(w, 22);
            }
            coinIcon.localScale = Vector3.one * (1 + coinBump * 0.25f);
            homeCoins.text = coins.ToString();
        }

        public void SetBooster(string id, int count, bool locked)
        {
            var k = keys.Find(x => x.id == id);
            if (k == null) return;
            k.bg.sprite = locked ? keyLockedSprite : keySprite;
            if (k.badge == null) return;
            k.badge.SetActive(!locked);
            k.ring.SetActive(!locked);
            k.lockTag.SetActive(locked);
            k.badgeImg.color = count > 0 ? Res.Hex("#3FAE55") : Res.Hex("#F2A93B");
            k.count.text = count > 0 ? count.ToString() : "+";
        }

        // ---------------------------------------------------------------- tip + praise
        float _tipT, _tipDur, _tipX, _tipY;

        public void ShowTip(string text, float x, float y, float dur)
        {
            tipText.text = text;
            float w = tipText.GetPreferredValues(text).x + 30;
            tipBg.sizeDelta = new Vector2(w, 34);
            tipShadow.sizeDelta = new Vector2(w, 34);
            _tipX = Mathf.Clamp(x, w / 2 + 8, 390 - w / 2 - 8); _tipY = y; _tipT = 0; _tipDur = dur;
            tip.gameObject.SetActive(true);
        }
        public void HideTip() { _tipDur = 0; tip.gameObject.SetActive(false); }

        static readonly (string t, string c1, string c2)[] PraiseTiers =
        {
            ("Nice!", "#8BE68A", "#22A24A"), ("Great!", "#86D4FF", "#1C7FE0"), ("Excellent!", "#D9AEFF", "#7A43E6"),
            ("Amazing!", "#FFD66E", "#FF8A1F"), ("Purrfect!", "#FFB8D4", "#FF4F93"),
        };
        readonly Dictionary<PraiseRefs, (float age, float x, float y)> _praiseLive = new Dictionary<PraiseRefs, (float, float, float)>();

        public void ShowPraise(int tier, float x, float y)
        {
            PraiseRefs p = praisePool.Find(q => !q.rt.gameObject.activeSelf);
            if (p == null) { float oldest = -1; foreach (var kv in _praiseLive) if (kv.Value.age > oldest) { oldest = kv.Value.age; p = kv.Key; } }
            if (p == null) return;
            var d = PraiseTiers[Mathf.Clamp(tier, 0, PraiseTiers.Length - 1)];
            p.text.text = d.t;
            var c1 = Res.Hex(d.c1); var c2 = Res.Hex(d.c2);
            p.text.colorGradient = new VertexGradient(c1, c1, c2, c2);
            float half = p.text.GetPreferredValues(d.t).x / 2 + 16;
            p.s1.rectTransform.anchoredPosition = new Vector2(-half, 6);
            p.s2.rectTransform.anchoredPosition = new Vector2(half, -4);
            p.rt.gameObject.SetActive(true);
            _praiseLive[p] = (0, x, y);
        }

        // ---------------------------------------------------------------- sheets
        RectTransform Panel(string kind) => sheets.Find(s => s.kind == kind)?.panel;

        void OpenSheet(string kind)
        {
            foreach (var s in sheets) s.panel.gameObject.SetActive(s.kind == kind);
            scrim.SetActive(true);
            SheetKind = kind;
            var rt = Panel(kind);
            if (_sheetAnim != null) StopCoroutine(_sheetAnim);
            _sheetAnim = StartCoroutine(SheetIn(rt));
        }

        IEnumerator SheetIn(RectTransform rt)
        {
            float t = 0;
            while (t < 0.38f)
            {
                t += Time.unscaledDeltaTime;
                float e = Ease.OutBack(Mathf.Clamp01(t / 0.38f));
                rt.anchoredPosition = new Vector2(0, 14 - 40 * (1 - e));
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, e);
                yield return null;
            }
        }

        public void HideSheet()
        {
            foreach (var s in sheets) s.panel.gameObject.SetActive(false);
            scrim.SetActive(false);
            SheetKind = null;
        }

        static void PaintToggle(Image track, RectTransform knob, bool on)
        {
            track.color = on ? Res.Hex("#4CB85E") : Res.Hex("#CDBFA8");
            knob.anchoredPosition = new Vector2(on ? 10 : -10, 0);
        }

        public void ShowPause(bool sfx, bool music, bool privacy, bool inLevel)
        {
            _sfx = sfx; _music = music;
            PaintToggle(sfxTrack, sfxKnob, sfx);
            PaintToggle(musicTrack, musicKnob, music);
            privacyButton.SetActive(privacy);
            var p = Panel("pause");
            p.Find("title").GetComponent<TextMeshProUGUI>().text = inLevel ? "Paused" : "Settings";
            p.Find("level row").gameObject.SetActive(inLevel);
            OpenSheet("pause");
        }

        public void ShowStuck(bool canUndo, bool canRope)
        {
            stuckUndo.interactable = canUndo;
            stuckRope.interactable = canRope;
            OpenSheet("stuck");
        }

        public void ShowRefill(string title, int price, bool canCoins)
        {
            refillTitle.text = title;
            refillCoinsLabel.text = "Use " + price + " coins";
            refillCoins.interactable = canCoins;
            refillCoins.GetComponent<Image>().color = canCoins ? Color.white : new Color(1, 1, 1, 0.45f);
            OpenSheet("refill");
        }

        public void ShowShop(Func<string, string> price, Func<string, bool> owned, bool storeReady)
        {
            foreach (var row in shopRows)
            {
                bool own = owned(row.productId);
                string pr = price(row.productId);
                row.price.text = own ? "Owned" : pr ?? "—";
                row.buttonImg.sprite = own ? btnGhost : btnBlue;
                row.price.color = own ? Res.Hex("#6A4A2C") : Color.white;
                row.button.interactable = !own && pr != null;
            }
            shopNote.SetActive(!storeReady);
            OpenSheet("shop");
        }

        public void ShowWorld(WorldDef w, string[] newPals)
        {
            worldEyebrow.text = w.StartLevel == 1 ? "Welcome to" : "New scene unlocked!";
            worldName.text = w.Name;
            var names = new List<string>();
            foreach (var id in newPals) names.Add(Content.Pal(id).Name);
            worldBody.text = w.Tagline + (names.Count > 0 ? "\n<size=13><color=#8A7460>New pals: " + string.Join(", ", names) + "</color></size>" : "");
            // face image -> bob -> holder (layout slot) -> row
            for (int i = 0; i < worldFaces.Count; i++)
            {
                bool on = i < newPals.Length;
                worldFaces[i].transform.parent.parent.gameObject.SetActive(on);
                if (on) worldFaces[i].sprite = Res.Pal(newPals[i], "happy");
            }
            worldFaces[0].transform.parent.parent.parent.gameObject.SetActive(newPals.Length > 0);
            OpenSheet("world");
        }

        // ---------------------------------------------------------------- home
        static int TileOrder(int worldIndex, int i)
        {
            var order = new List<int>();
            for (int k = 0; k < 25; k++) order.Add(k);
            new Mulberry32((uint)(worldIndex * 7919 + 3)).Shuffle(order);
            return order[i];
        }

        static Color TileTone(int theme, int idx) => Res.Hex(TileTones[theme, ((idx % 5) + (idx / 5)) % 2]);

        /// <summary>The 4 pals hidden in a scene's picture: the world's new pals first, then other unlocked pals.</summary>
        static List<string> SceneCast(int sceneNumber, WorldDef w)
        {
            var cast = new List<string>(w.NewPals);
            var pool = Content.UnlockedPals(w.StartLevel);
            for (int i = 0; i < pool.Count && cast.Count < 4; i++)
            {
                var id = pool[(pool.Count - 1 - i + sceneNumber * 3) % pool.Count];
                if (cast.Contains(id)) continue;
                if ((id == "bunny" && cast.Contains("panda")) || (id == "panda" && cast.Contains("bunny"))) continue;
                cast.Add(id);
            }
            foreach (var id in pool) { if (cast.Count >= 4) break; if (!cast.Contains(id)) cast.Add(id); }
            return cast;
        }

        /// <summary>Home screen: the current scene with one tile revealed per cleared level.</summary>
        public void ShowHome(int sceneNumber, WorldDef w, int done, int length, int nextLevel, bool hard, bool animate)
        {
            home.SetActive(true);
            hud.SetActive(false);
            sceneTitle.text = "Scene " + sceneNumber + "  ·  " + w.Name;
            sceneCounter.text = Mathf.Min(done, length) + " / " + length + " levels";
            scenePicture.sprite = Res.Sprite("bg_" + w.Theme);
            var cast = SceneCast(sceneNumber, w);
            for (int i = 0; i < scenePals.Count; i++) scenePals[i].sprite = Res.Art.Pal(cast[i % cast.Count], "happy");
            playLabel.text = done >= length ? "Next scene" : "Level " + nextLevel;
            playHard.SetActive(hard && done < length);
            int target = Mathf.Clamp(Mathf.FloorToInt(25f * done / Mathf.Max(1, length)), 0, 25);
            if (_shownWorld != sceneNumber) { _shownWorld = sceneNumber; _shownTiles = animate ? Mathf.Max(0, target - 1) : target; if (done == 0) _shownTiles = 0; }
            int from = Mathf.Min(_shownTiles, target);
            int theme = Mathf.Clamp(w.Theme, 0, TileTones.GetLength(0) - 1);
            for (int i = 0; i < 25; i++)
            {
                int idx = TileOrder(sceneNumber, i);
                var tile = sceneTiles[idx];
                tile.gameObject.SetActive(i >= from);
                tile.rectTransform.localScale = Vector3.one;
                tile.color = TileTone(theme, idx);
                if (idx < sceneTileIcons.Count) { sceneTileIcons[idx].sprite = Res.Sprite("fx_heart"); sceneTileIcons[idx].color = new Color(1, 1, 1, 0.55f); sceneTileIcons[idx].rectTransform.sizeDelta = new Vector2(20, 20); }
            }
            // the tiles the NEXT win will open glow gold with a star, so the link "win = open" is visible
            _glowScene = sceneNumber; _glowFrom = target;
            _glowTo = done >= length ? target : Mathf.Clamp(Mathf.FloorToInt(25f * (done + 1) / Mathf.Max(1, length)), target, 25);
            if (_glowTo == _glowFrom && done < length) _glowTo = Mathf.Min(25, _glowFrom + 1);
            for (int i = _glowFrom; i < _glowTo; i++)
            {
                int idx = TileOrder(sceneNumber, i);
                if (idx < sceneTileIcons.Count) { sceneTileIcons[idx].sprite = Res.Sprite("star_gold"); sceneTileIcons[idx].color = Color.white; sceneTileIcons[idx].rectTransform.sizeDelta = new Vector2(30, 30); }
            }
            if (sceneProgressFill != null)
            {
                float f = Mathf.Clamp01(done / (float)Mathf.Max(1, length));
                sceneProgressFill.sizeDelta = new Vector2(f <= 0 ? 0 : Mathf.Lerp(26, 194, f), 26);
            }
            if (animate && target > from) StartCoroutine(RevealTiles(sceneNumber, from, target));
            _shownTiles = target;
        }

        IEnumerator RevealTiles(int sceneNumber, int from, int to)
        {
            yield return new WaitForSecondsRealtime(0.45f);
            for (int i = from; i < to; i++)
            {
                var tile = sceneTiles[TileOrder(sceneNumber, i)];
                audioManager.HomePop(Mathf.Min(i - from, 5));
                float t = 0;
                while (t < 0.35f)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(t / 0.35f);
                    tile.rectTransform.localScale = Vector3.one * (u < 0.3f ? 1f + u * 0.4f : (1.12f) * (1f - (u - 0.3f) / 0.7f));
                    tile.color = Color.Lerp(tile.color, Res.Hex("#FFE27A"), u);
                    yield return null;
                }
                tile.gameObject.SetActive(false);
                yield return new WaitForSecondsRealtime(0.08f);
            }
            if (to >= 25) { audioManager.HomeFilled(); }
        }

        public void HideHome() { home.SetActive(false); hud.SetActive(true); }

        // ---------------------------------------------------------------- toast + ad
        float _toastT;
        public void Toast(string msg)
        {
            toastText.text = msg;
            var w = Mathf.Min(toastText.GetPreferredValues(msg).x + 32, 340);
            toast.sizeDelta = new Vector2(w, 40);
            toastText.rectTransform.sizeDelta = new Vector2(w - 16, 40);
            _toastT = 2.2f;
            toast.gameObject.SetActive(true);
        }

        public void ShowAdCard(string label, Action done) => StartCoroutine(AdCard(label, done));
        IEnumerator AdCard(string label, Action done)
        {
            adTitle.text = label;
            adCard.SetActive(true);
            yield return new WaitForSecondsRealtime(1.4f);
            adCard.SetActive(false);
            done?.Invoke();
        }

        // ---------------------------------------------------------------- win
        static readonly Vector2[] PalPos = { new Vector2(22, 74), new Vector2(78, 74), new Vector2(36, 86), new Vector2(64, 86), new Vector2(11, 58), new Vector2(89, 58), new Vector2(50, 92) };
        Coroutine _winRun;

        public void ShowWin(WinData d)
        {
            winTitle.text = d.Title;
            wTime.text = d.Time; wTimeG.text = d.TimeGrade;
            wMoves.text = d.Moves.ToString(); wMovesG.text = d.MovesGrade;
            wStreak.text = d.Streak.ToString();
            wNote.text = d.Note;
            wCoins.text = "+0";
            btnDouble.gameObject.SetActive(true);
            btnDouble.interactable = true;
            for (int i = 0; i < winPals.Count; i++)
            {
                bool on = i < d.Pals.Length;
                winPals[i].gameObject.SetActive(on);
                if (!on) continue;
                winPals[i].sprite = Res.Pal(d.Pals[i], "happy");
                var p = PalPos[i];
                var rt = winPals[i].rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(p.x / 100f, 1f - p.y / 100f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.zero;
            }
            win.gameObject.SetActive(true);
            if (_winRun != null) StopCoroutine(_winRun);
            _winRun = StartCoroutine(WinSequence(d));
        }

        public void SetWinCoins(int total) { wCoins.text = "+" + total; btnDouble.gameObject.SetActive(false); }
        public void SetDoubleBusy(bool busy) { btnDouble.interactable = !busy; }

        public void HideWin()
        {
            if (_winRun != null) StopCoroutine(_winRun);
            win.gameObject.SetActive(false);
            foreach (var c in confettiPool) c.gameObject.SetActive(false);
            _confettiT = 0;
        }

        IEnumerator Pop(RectTransform rt, float delay, float dur = 0.5f, float from = 0f)
        {
            rt.localScale = Vector3.one * from;
            if (delay > 0) yield return new WaitForSecondsRealtime(delay);
            float t = 0;
            while (t < dur) { t += Time.unscaledDeltaTime; rt.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, Ease.OutBack(Mathf.Clamp01(t / dur))); yield return null; }
        }

        IEnumerator Later(float delay, Action a) { yield return new WaitForSecondsRealtime(delay); a(); }

        IEnumerator WinSequence(WinData d)
        {
            _confettiT = 2.5f;
            foreach (var s in wStars) s.sprite = starDark;
            StartCoroutine(Pop(winRainbow, 0.15f, 0.7f, 0.3f));
            StartCoroutine(TitleDrop());
            StartCoroutine(Pop(habitat, 0.3f, 0.55f));
            for (int i = 0; i < d.Pals.Length && i < winPals.Count; i++)
            {
                int ii = i;
                StartCoroutine(Pop(winPals[i].rectTransform, 0.55f + i * 0.14f));
                StartCoroutine(Later(0.55f + i * 0.14f, () => { audioManager.Voice(Content.Pal(d.Pals[ii])); audioManager.HomePop(ii); }));
            }
            for (int i = 0; i < 3; i++) StartCoroutine(Pop(cards[i], 0.9f + i * 0.15f, 0.4f));
            StartCoroutine(Pop(winButtons, 1.5f, 0.4f));
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime) { winGroup.alpha = t / 0.3f; yield return null; }
            winGroup.alpha = 1;
            yield return new WaitForSecondsRealtime(0.4f);
            for (int i = 0; i < d.Stars; i++)
            {
                wStars[i].sprite = starGold;
                StartCoroutine(Pop(wStars[i].rectTransform, 0, 0.35f, 0.6f));
                audioManager.Star(i);
                yield return new WaitForSecondsRealtime(0.25f);
            }
            yield return new WaitForSecondsRealtime(0.8f);
            int shown = 0, step = Mathf.Max(1, Mathf.RoundToInt(d.Reward / 12f));
            while (shown < d.Reward)
            {
                shown = Mathf.Min(d.Reward, shown + step);
                if (btnDouble.gameObject.activeSelf) wCoins.text = "+" + shown;
                audioManager.Tick();
                yield return new WaitForSecondsRealtime(0.045f);
            }
        }

        IEnumerator TitleDrop()
        {
            float t = 0;
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                float e = Ease.OutBack(Mathf.Clamp01((t - 0.1f) / 0.5f));
                winTitleRt.anchoredPosition = new Vector2(0, -62 + 60 * (1 - e));
                winTitleRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, e);
                winTitleRt.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(8f, 3f, e));
                yield return null;
            }
        }

        // ---------------------------------------------------------------- confetti (pooled UI images above the win screen)
        sealed class Confetto { public float X, Y, Vx, Vy, Rot, Vr, T; }
        readonly Dictionary<Image, Confetto> _confetti = new Dictionary<Image, Confetto>();
        float _confettiT;
        static readonly string[] ConfettiCols = { "#FF6B6B", "#FFD54A", "#6BD48E", "#5CB8FF", "#B98CFF", "#FF8FC8" };

        void SpawnConfetto()
        {
            var img = confettiPool.Find(c => !c.gameObject.activeSelf);
            if (img == null) return;
            float w = root.rect.width, h = root.rect.height;
            img.color = Res.Hex(ConfettiCols[UnityEngine.Random.Range(0, ConfettiCols.Length)]);
            img.rectTransform.sizeDelta = new Vector2(UnityEngine.Random.Range(5f, 9f), UnityEngine.Random.Range(3f, 5.4f));
            _confetti[img] = new Confetto { X = UnityEngine.Random.Range(-w / 2, w / 2), Y = h / 2 + 10, Vx = UnityEngine.Random.Range(-30f, 30f), Vy = -UnityEngine.Random.Range(60f, 150f), Rot = UnityEngine.Random.value * 6.28f, Vr = UnityEngine.Random.Range(-6f, 6f) };
            img.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- per frame
        readonly List<PraiseRefs> _done = new List<PraiseRefs>();
        readonly List<Image> _deadConfetti = new List<Image>();

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (home.activeSelf)
            {
                // pulse the "next win" tiles: gold, breathing
                float s = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                for (int i = _glowFrom; i < _glowTo; i++)
                {
                    var tile = sceneTiles[TileOrder(_glowScene, i)];
                    if (!tile.gameObject.activeSelf) continue;
                    tile.color = Color.Lerp(Res.Hex("#FFC93C"), Res.Hex("#FFE68A"), s);
                    tile.rectTransform.localScale = Vector3.one * (1f + 0.05f * s);
                }
            }
            if (tip.gameObject.activeSelf)
            {
                _tipT += dt;
                float a = Mathf.Min(1f, Mathf.Min(_tipT * 6f, (_tipDur - _tipT) * 4f));
                if (a <= 0 && _tipT > 0.2f) tip.gameObject.SetActive(false);
                tipGroup.alpha = Mathf.Clamp01(a);
                if (DesignToCanvas != null) tip.anchoredPosition = DesignToCanvas(_tipX, _tipY + (1 - Mathf.Min(1, _tipT * 6)) * 8f);
            }

            _done.Clear();
            foreach (var p in praisePool)
            {
                if (!_praiseLive.TryGetValue(p, out var st)) continue;
                float u = st.age + dt;
                _praiseLive[p] = (u, st.x, st.y);
                float sc = u < 0.22f ? 0.3f + Ease.OutBack(u / 0.22f) : 1f + Mathf.Max(0, 0.08f - (u - 0.22f) * 0.2f);
                float a = u < 1f ? 1f : Mathf.Max(0, 1f - (u - 1f) / 0.35f);
                p.rt.localScale = Vector3.one * sc;
                p.text.alpha = a;
                if (DesignToCanvas != null) p.rt.anchoredPosition = DesignToCanvas(st.x, st.y - u * 24f);
                p.s1.rectTransform.localRotation = Quaternion.Euler(0, 0, -u * 170f);
                p.s2.rectTransform.localRotation = Quaternion.Euler(0, 0, u * 170f);
                p.s1.color = p.s2.color = new Color(1f, 0.97f, 0.76f, a);
                if (u > 1.35f) _done.Add(p);
            }
            foreach (var p in _done) { p.rt.gameObject.SetActive(false); _praiseLive.Remove(p); }

            for (int i = 0; i < bobbers.Count; i++)
            {
                var rt = bobbers[i];
                if (!rt.gameObject.activeInHierarchy) continue;
                float s = Mathf.Sin(Time.unscaledTime * 2f + i * 0.6f);
                rt.localPosition = new Vector3(0, 3f + s * 3f, 0);
                rt.localRotation = Quaternion.Euler(0, 0, s * 3f);
            }

            if (_toastT > 0) { _toastT -= dt; if (_toastT <= 0) toast.gameObject.SetActive(false); }

            if (win.gameObject.activeSelf)
            {
                if (_confettiT > 0) { _confettiT -= dt; SpawnConfetto(); SpawnConfetto(); }
                float h = root.rect.height;
                _deadConfetti.Clear();
                foreach (var kv in _confetti)
                {
                    var img = kv.Key; var c = kv.Value;
                    if (!img.gameObject.activeSelf) continue;
                    c.T += dt; c.Vy -= 60f * dt; c.X += c.Vx * dt; c.Y += c.Vy * dt; c.Rot += c.Vr * dt;
                    img.rectTransform.anchoredPosition = new Vector2(c.X, c.Y);
                    img.rectTransform.localRotation = Quaternion.Euler(0, 0, c.Rot * Mathf.Rad2Deg);
                    img.rectTransform.localScale = new Vector3(1, Mathf.Cos(c.T * 7 + c.Rot), 1);
                    if (c.Y < -h / 2 - 20 || c.T > 5) _deadConfetti.Add(img);
                }
                foreach (var img in _deadConfetti) img.gameObject.SetActive(false);
            }
        }
    }
}
