using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DanglePals.EditorTools
{
    /// <summary>
    /// Authors the whole canvas into the scene at edit time (HUD, home, sheets, shop, win screen, pools)
    /// and wires every reference on <see cref="GameUi"/>. Re-run "Dangle Pals > Run Full Setup" to rebuild.
    /// </summary>
    public static class UiBuilder
    {
        static GameUi _ui;
        static readonly Vector2 Top = new Vector2(0.5f, 1f), Bottom = new Vector2(0.5f, 0f), Center = new Vector2(0.5f, 0.5f);
        static Material _outlineWhite, _outlineBlue, _outlinePurple, _outlineRed, _outlineOrange;

        public static GameUi Build(Transform parent, AudioManager audio)
        {
            MakeMaterials();
            var go = new GameObject("UI", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 760);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<CanvasAspect>();
            go.AddComponent<GraphicRaycaster>();
            _ui = go.AddComponent<GameUi>();
            _ui.audioManager = audio;
            _ui.root = (RectTransform)go.transform;
            _ui.starOn = Res.Sprite("star_on"); _ui.starOff = Res.Sprite("star_off");
            _ui.starGold = Res.Sprite("star_gold"); _ui.starDark = Res.Sprite("star_dark");
            _ui.keySprite = Res.Sprite("key"); _ui.keyLockedSprite = Res.Sprite("key_locked");
            _ui.btnBlue = Res.Sprite("btn_blue"); _ui.btnGhost = Res.Sprite("btn_ghost");

            var hud = Safe("HUD");
            _ui.hud = hud.gameObject;
            BuildHud(hud);
            BuildBoosters(hud);
            var home = Safe("Home");
            _ui.home = home.gameObject;
            BuildHome(home);
            BuildBoardFx();
            BuildScrim();
            var sheets = Safe("Sheets");
            BuildPause(sheets); BuildStuck(sheets); BuildRefill(sheets); BuildShop(sheets); BuildWorld(sheets);
            BuildWin();
            var top = Safe("Top");
            BuildToast(top);
            BuildAdCard();
            EditorUtility.SetDirty(_ui);
            return _ui;
        }

        // ------------------------------------------------------------------ helpers
        static void MakeMaterials()
        {
            _outlineWhite = Mat("Outline White", Color.white, 0.28f);
            _outlineBlue = Mat("Outline Blue", Res.Hex("#2F6FC9"), 0.24f);
            _outlinePurple = Mat("Outline Purple", Res.Hex("#6A45E0"), 0.22f);
            _outlineRed = Mat("Outline Red", Res.Hex("#B5282F"), 0.2f);
            _outlineOrange = Mat("Outline Orange", Res.Hex("#C9601A"), 0.2f);
        }

        static Material Mat(string name, Color col, float width)
        {
            string path = "Assets/DanglePals/Art/Fonts/Fredoka Bold " + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Res.Font(true).material); AssetDatabase.CreateAsset(m, path); }
            m.CopyPropertiesFromMaterial(Res.Font(true).material);
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetColor(ShaderUtilities.ID_OutlineColor, col);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, width * 0.6f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static RectTransform Safe(string name)
        {
            var rt = UiKit.Node(name, _ui.root);
            UiKit.Stretch(rt);
            rt.gameObject.AddComponent<SafeArea>();
            return rt;
        }

        static RectTransform At(RectTransform rt, Vector2 anchor, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        static Button Btn(Transform parent, string style, string label, float w, float h, string id, float size = 19, string icon = null)
        {
            var b = UiKit.Button(parent, style, label, w, h, null, size, icon);
            _ui.binds.Add(new GameUi.Bind { id = id, button = b });
            return b;
        }

        static Button Link(Transform parent, string label, Color color, string id)
        {
            var b = UiKit.LinkButton(parent, label, color, null);
            _ui.binds.Add(new GameUi.Bind { id = id, button = b });
            return b;
        }

        static void Bind(GameObject go, string id)
        {
            var b = go.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            go.AddComponent<PressScale>();
            _ui.binds.Add(new GameUi.Bind { id = id, button = b });
        }

        static (RectTransform face, RectTransform shadow) Pill(Transform parent, Vector2 anchor, float x, float y, float w, float h, Color face)
        {
            var sh = UiKit.Img(parent, "ui_pill", new Color(0.24f, 0.35f, 0.27f, 0.14f), new Vector2(w, h), true);
            At(sh.rectTransform, anchor, x, y - 2.5f);
            var f = UiKit.Img(parent, "ui_pill", face, new Vector2(w, h), true);
            At(f.rectTransform, anchor, x, y);
            return (f.rectTransform, sh.rectTransform);
        }

        static TextMeshProUGUI Outlined(Transform parent, string text, float size, Color color, Material mat)
        {
            var t = UiKit.Text(parent, text, size, color, true);
            t.fontSharedMaterial = mat;
            return t;
        }

        // ------------------------------------------------------------------ HUD
        static void BuildHud(RectTransform hud)
        {
            var psh = UiKit.Img(hud, "ui_circle", new Color(0.24f, 0.35f, 0.27f, 0.16f), new Vector2(40, 40));
            At(psh.rectTransform, Top, 34 - 195, -43);
            var pause = UiKit.Img(hud, "ui_circle", Color.white, new Vector2(40, 40), false, true);
            pause.name = "pause button";
            At(pause.rectTransform, Top, 34 - 195, -40);
            UiKit.Img(pause.transform, "icon_pause", Color.white, new Vector2(24, 24));
            Bind(pause.gameObject, "pause");

            Pill(hud, Top, 0, -40, 130, 38, new Color(1, 1, 1, 0.9f));
            _ui.levelText = UiKit.Text(hud, "Level 1", 20, UiKit.Leaf);
            At(_ui.levelText.rectTransform, Top, 0, -41).sizeDelta = new Vector2(130, 38);

            var hard = UiKit.Node("hard tag", hud);
            At(hard, Top, 0, -66).sizeDelta = new Vector2(80, 18);
            UiKit.Img(hard, "icon_flame", Color.white, new Vector2(14, 17)).rectTransform.anchoredPosition = new Vector2(-20, 0);
            var ht = UiKit.Text(hard, "Hard", 14, Res.Hex("#F0434F"), true, TextAlignmentOptions.Left);
            ht.rectTransform.sizeDelta = new Vector2(50, 18); ht.rectTransform.anchoredPosition = new Vector2(14, 0);
            _ui.hardTag = hard.gameObject;

            var coin = Pill(hud, Top, 326 - 195, -40, 100, 34, new Color(1, 1, 1, 0.9f)).face;
            coin.name = "coins button";
            coin.GetComponent<Image>().raycastTarget = true;
            Bind(coin.gameObject, "shop");
            var ci = UiKit.Img(coin, "coin", Color.white, new Vector2(26, 26));
            ci.rectTransform.anchoredPosition = new Vector2(-35, 0);
            _ui.coinIcon = ci.rectTransform;
            _ui.coinsText = UiKit.Text(coin, "120", 18, Res.Hex("#5A3E12"), false, TextAlignmentOptions.Right);
            _ui.coinsText.rectTransform.sizeDelta = new Vector2(60, 30);
            _ui.coinsText.rectTransform.anchoredPosition = new Vector2(-4, 1);
            UiKit.Img(coin, "icon_plus", Color.white, new Vector2(20, 20)).rectTransform.anchoredPosition = new Vector2(42, 0);

            Pill(hud, Top, 0, -94, 212, 32, new Color(1, 1, 1, 0.8f));
            _ui.movesText = UiKit.Text(hud, "Moves 0", 15, UiKit.Leaf, false, TextAlignmentOptions.Left);
            At(_ui.movesText.rectTransform, Top, -45, -95).sizeDelta = new Vector2(90, 24);
            _ui.parText = UiKit.Text(hud, "par 8", 11, UiKit.Muted, false, TextAlignmentOptions.Left);
            At(_ui.parText.rectTransform, Top, 1, -96).sizeDelta = new Vector2(50, 20);
            for (int i = 0; i < 3; i++)
            {
                _ui.stars[i] = UiKit.Img(hud, "star_on", Color.white, new Vector2(23, 23));
                At(_ui.stars[i].rectTransform, Top, 40 + i * 24, -94);
            }
            var (rp, rs) = Pill(hud, Top, 0, -126, 160, 22, new Color(1, 1, 1, 0.6f));
            _ui.rescuedPill = rp; _ui.rescuedShadow = rs;
            _ui.rescuedText = UiKit.Text(hud, "Rescued 0 / 3", 13, UiKit.Leaf);
            At(_ui.rescuedText.rectTransform, Top, 0, -127).sizeDelta = new Vector2(340, 22);
        }

        static void BuildBoosters(RectTransform hud)
        {
            string[] ids = { "undo", "hint", "rope", "restart" };
            string[] labels = { "Undo", "Hint", "+Rope", "Restart" };
            for (int i = 0; i < 4; i++)
            {
                string id = ids[i];
                float x = (i - 1.5f) * 82;
                var holder = UiKit.Node("key " + id, hud);
                At(holder, Bottom, x, 68).sizeDelta = new Vector2(64, 66);
                var bg = UiKit.Img(holder, "key", Color.white, new Vector2(62, 64), false, true);
                bg.rectTransform.anchoredPosition = new Vector2(0, -1);
                UiKit.Img(holder, "icon_" + id, Color.white, new Vector2(44, 44)).rectTransform.anchoredPosition = new Vector2(0, 2);
                var label = UiKit.Text(hud, labels[i], 12, Res.Hex("#6A4E30"));
                At(label.rectTransform, Bottom, x, 26).sizeDelta = new Vector2(80, 18);
                var k = new GameUi.KeyRefs { id = id, bg = bg };
                if (id != "restart")
                {
                    var ring = UiKit.Img(holder, "ui_circle", Color.white, new Vector2(25, 25));
                    ring.rectTransform.anchoredPosition = new Vector2(22, -16);
                    var badge = UiKit.Img(holder, "ui_circle", Res.Hex("#3FAE55"), new Vector2(21, 21));
                    badge.rectTransform.anchoredPosition = new Vector2(22, -16);
                    var count = UiKit.Text(badge.transform, "3", 12, Color.white);
                    count.rectTransform.sizeDelta = new Vector2(21, 21);
                    count.rectTransform.anchoredPosition = new Vector2(0, 0.5f);
                    var lockTag = UiKit.Node("lock", holder);
                    var lk = UiKit.Img(lockTag, "ui_pill", Res.Hex("#8C7F6E"), new Vector2(26, 18), true);
                    UiKit.Text(lk.transform, "Lv." + GameConfig.RopeUnlockLevel, 10, Color.white).rectTransform.sizeDelta = new Vector2(26, 18);
                    lockTag.anchoredPosition = new Vector2(19, -17.5f);
                    lockTag.gameObject.SetActive(false);
                    k.ring = ring.gameObject; k.badge = badge.gameObject; k.badgeImg = badge; k.count = count; k.lockTag = lockTag.gameObject;
                }
                Bind(bg.gameObject, "boost_" + id);
                _ui.keys.Add(k);
            }
        }

        // ------------------------------------------------------------------ home
        static void BuildHome(RectTransform home)
        {
            var gsh = UiKit.Img(home, "ui_circle", new Color(0.24f, 0.35f, 0.27f, 0.16f), new Vector2(42, 42));
            At(gsh.rectTransform, Top, 34 - 195, -43);
            var gear = UiKit.Img(home, "ui_circle", Color.white, new Vector2(42, 42), false, true);
            gear.name = "settings button";
            At(gear.rectTransform, Top, 34 - 195, -40);
            UiKit.Img(gear.transform, "icon_gear", Color.white, new Vector2(34, 34));
            Bind(gear.gameObject, "settings");

            var coin = Pill(home, Top, 326 - 195, -40, 100, 34, new Color(1, 1, 1, 0.92f)).face;
            coin.name = "coins button";
            coin.GetComponent<Image>().raycastTarget = true;
            Bind(coin.gameObject, "shop");
            UiKit.Img(coin, "coin", Color.white, new Vector2(26, 26)).rectTransform.anchoredPosition = new Vector2(-35, 0);
            _ui.homeCoins = UiKit.Text(coin, "120", 18, Res.Hex("#5A3E12"), false, TextAlignmentOptions.Right);
            _ui.homeCoins.rectTransform.sizeDelta = new Vector2(60, 30);
            _ui.homeCoins.rectTransform.anchoredPosition = new Vector2(-4, 1);
            UiKit.Img(coin, "icon_plus", Color.white, new Vector2(20, 20)).rectTransform.anchoredPosition = new Vector2(42, 0);

            var title = Outlined(home, "Dangle Pals", 48, Color.white, _outlineBlue);
            At(title.rectTransform, Top, 0, -112).sizeDelta = new Vector2(360, 64);
            var tag = UiKit.Text(home, "Win a level to open the glowing tiles!", 15, UiKit.Leaf);
            At(tag.rectTransform, Top, 0, -152).sizeDelta = new Vector2(360, 22);

            // scene card: the world's picture hidden under 25 tiles, one revealed per cleared level
            var card = UiKit.Img(home, "ui_panel", Res.Hex("#FFB43B"), new Vector2(312, 312), true);
            card.name = "scene card";
            At(card.rectTransform, Top, 0, -352);
            var inner = UiKit.Img(card.transform, "ui_panel", Res.Hex("#FFF4E2"), new Vector2(292, 292), true);
            var window = UiKit.Node("picture window", inner.transform);
            window.sizeDelta = new Vector2(278, 278);
            window.gameObject.AddComponent<RectMask2D>();
            _ui.scenePicture = UiKit.Img(window, "bg_0", Color.white, new Vector2(278, 278f * 844f / 390f));
            _ui.scenePicture.rectTransform.anchoredPosition = new Vector2(0, 48);
            // the scene's pals, posing for a group photo: this is what the tiles hide
            var posePos = new[] { new Vector2(-62, 46), new Vector2(62, 46), new Vector2(-66, -72), new Vector2(66, -72) };
            for (int i = 0; i < 4; i++)
            {
                var pal = UiKit.Img(window, "ui_card", Color.white, new Vector2(118, 118));
                pal.name = "scene pal " + i;
                pal.sprite = Res.Art.Pal(i == 0 ? "fox" : i == 1 ? "bunny" : i == 2 ? "bear" : "raccoon", "happy");
                pal.preserveAspect = true;
                pal.rectTransform.anchoredPosition = posePos[i];
                _ui.scenePals.Add(pal);
            }
            float tile = (278f - 4 * 3f) / 5f;
            for (int i = 0; i < 25; i++)
            {
                var t = UiKit.Img(window, "ui_card", Res.Hex("#7FCB6E"), new Vector2(tile, tile), true);
                t.name = "tile " + i;
                int cx = i % 5, cy = i / 5;
                t.rectTransform.anchoredPosition = new Vector2(-139 + tile / 2 + cx * (tile + 3), 139 - tile / 2 - cy * (tile + 3));
                _ui.sceneTiles.Add(t);
                var icon = UiKit.Img(t.transform, "fx_heart", new Color(1, 1, 1, 0.55f), new Vector2(20, 20));
                icon.name = "icon";
                icon.preserveAspect = true;
                _ui.sceneTileIcons.Add(icon);
            }
            var banner = UiKit.Img(card.transform, "ui_pill", Res.Hex("#FF8A2A"), new Vector2(268, 42), true);
            banner.rectTransform.anchoredPosition = new Vector2(0, 156);
            _ui.sceneTitle = Outlined(banner.transform, "Scene 1  ·  Sunny Meadow", 18, Color.white, _outlineOrange);
            _ui.sceneTitle.rectTransform.sizeDelta = new Vector2(260, 40);
            // progress bar: dark track, green fill (width set at runtime), "6 / 15 levels" on top
            var counter = UiKit.Img(card.transform, "ui_pill", Res.Hex("#2E6B57"), new Vector2(200, 32), true);
            counter.name = "progress bar";
            counter.rectTransform.anchoredPosition = new Vector2(0, -160);
            var fill = UiKit.Img(counter.transform, "ui_pill", Res.Hex("#5FD068"), new Vector2(200, 26), true);
            fill.name = "fill";
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0, 0.5f);
            fill.rectTransform.pivot = new Vector2(0, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(3, 0);
            _ui.sceneProgressFill = fill.rectTransform;
            _ui.sceneCounter = Outlined(counter.transform, "0 / 15 levels", 16, Color.white, _outlineBlue);
            _ui.sceneCounter.rectTransform.sizeDelta = new Vector2(200, 32);

            var play = UiKit.Img(home, "btn_red", Color.white, new Vector2(300, 88), true, true);
            play.name = "play button";
            At(play.rectTransform, Bottom, 0, 118);
            Bind(play.gameObject, "play");
            _ui.playLabel = Outlined(play.transform, "Level 1", 36, Color.white, _outlineRed);
            _ui.playLabel.rectTransform.sizeDelta = new Vector2(290, 50);
            _ui.playLabel.rectTransform.anchoredPosition = new Vector2(0, 10);
            var hard = UiKit.Node("hard", play.transform);
            hard.anchoredPosition = new Vector2(0, -22);
            hard.sizeDelta = new Vector2(80, 20);
            UiKit.Img(hard, "icon_flame", Color.white, new Vector2(14, 17)).rectTransform.anchoredPosition = new Vector2(-20, 0);
            var hl = UiKit.Text(hard, "Hard", 16, Color.white, true, TextAlignmentOptions.Left);
            hl.rectTransform.sizeDelta = new Vector2(50, 20); hl.rectTransform.anchoredPosition = new Vector2(14, 0);
            _ui.playHard = hard.gameObject;
        }

        // ------------------------------------------------------------------ board-space fx (tip, praise pool)
        static void BuildBoardFx()
        {
            var board = UiKit.Node("Board FX", _ui.root);
            UiKit.Place(board, Center, Vector2.zero, Vector2.zero);
            _ui.board = board;

            var tip = UiKit.Node("tip", board);
            _ui.tip = tip;
            _ui.tipGroup = tip.gameObject.AddComponent<CanvasGroup>();
            _ui.tipGroup.blocksRaycasts = false;
            var sh = UiKit.Img(tip, "ui_pill", new Color(0.24f, 0.2f, 0.12f, 0.18f), new Vector2(200, 34), true);
            sh.rectTransform.anchoredPosition = new Vector2(0, -2);
            _ui.tipShadow = sh.rectTransform;
            var tail = UiKit.Img(tip, "ui_card", Color.white, new Vector2(12, 12));
            tail.rectTransform.anchoredPosition = new Vector2(0, -16);
            tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            _ui.tipBg = UiKit.Img(tip, "ui_pill", Color.white, new Vector2(200, 34), true).rectTransform;
            _ui.tipText = UiKit.Text(tip, "", 14, UiKit.Ink);
            _ui.tipText.rectTransform.sizeDelta = new Vector2(360, 34);

            for (int i = 0; i < 4; i++)
            {
                var rt = UiKit.Node("praise " + i, board);
                var t = Outlined(rt, "Nice!", 34, Color.white, _outlineWhite);
                t.rectTransform.sizeDelta = new Vector2(300, 50);
                t.enableVertexGradient = true;
                var s1 = UiKit.Img(rt, "fx_spark", Res.Hex("#FFF7C2"), new Vector2(16, 16));
                var s2 = UiKit.Img(rt, "fx_spark", Res.Hex("#FFF7C2"), new Vector2(13, 13));
                rt.localRotation = Quaternion.Euler(0, 0, 3.4f);
                _ui.praisePool.Add(new GameUi.PraiseRefs { rt = rt, text = t, s1 = s1, s2 = s2 });
            }
        }

        // ------------------------------------------------------------------ sheets
        static void BuildScrim()
        {
            var s = UiKit.Img(_ui.root, null, new Color(0.094f, 0.15f, 0.12f, 0.38f), Vector2.zero, false, true);
            s.name = "Scrim";
            UiKit.Stretch(s.rectTransform);
            _ui.scrim = s.gameObject;
        }

        static RectTransform Sheet(RectTransform parent, string kind)
        {
            var panel = UiKit.Img(parent, "ui_panel", UiKit.Paper, new Vector2(366, 200), true, true);
            panel.name = "Sheet " + kind;
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(12, 14); rt.offsetMax = new Vector2(-12, 214);
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(18, 18, 18, 16);
            v.spacing = 8;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.16f, 0.12f, 0.04f, 0.3f);
            shadow.effectDistance = new Vector2(0, -6);
            _ui.sheets.Add(new GameUi.SheetRefs { kind = kind, panel = rt });
            return rt;
        }

        static TextMeshProUGUI Title(RectTransform s, string text, float size = 26)
        {
            var t = UiKit.Text(s, text, size, UiKit.Ink, true);
            t.name = "title";
            return t;
        }
        static TextMeshProUGUI Body(RectTransform s, string text) => UiKit.Text(s, text, 15, UiKit.Body);

        static void Toggle(RectTransform s, string label, string id, out Image track, out RectTransform knob)
        {
            var bg = UiKit.Img(s, "ui_card", UiKit.Cream, new Vector2(300, 46), true, true);
            bg.name = "toggle " + id;
            UiKit.Layout(bg, 320, 46);
            var t = UiKit.Text(bg.transform, label, 17, Res.Hex("#4A3424"), false, TextAlignmentOptions.Left);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(14, 0); t.rectTransform.offsetMax = new Vector2(-70, 0);
            track = UiKit.Img(bg.transform, "ui_pill", Res.Hex("#4CB85E"), new Vector2(46, 26), true);
            track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.anchoredPosition = new Vector2(-37, 0);
            knob = UiKit.Img(track.transform, "ui_circle", Color.white, new Vector2(20, 20)).rectTransform;
            knob.anchoredPosition = new Vector2(10, 0);
            Bind(bg.gameObject, id);
        }

        static void BuildPause(RectTransform parent)
        {
            var s = Sheet(parent, "pause");
            Title(s, "Paused");
            Toggle(s, "Sound effects", "toggle_sfx", out _ui.sfxTrack, out _ui.sfxKnob);
            Toggle(s, "Music", "toggle_music", out _ui.musicTrack, out _ui.musicKnob);
            var r1 = UiKit.Row(s, 10, 48);
            Btn(r1.transform, "ghost", "Shop", 120, 44, "shop", 16);
            _ui.privacyButton = Btn(r1.transform, "ghost", "Privacy options", 170, 44, "privacy", 16).gameObject;
            var r2 = UiKit.Row(s, 10, 48);
            r2.name = "level row";
            Btn(r2.transform, "ghost", "Home", 120, 44, "home", 16, "icon_home");
            Btn(r2.transform, "ghost", "Restart level", 160, 44, "restart", 16);
            var r3 = UiKit.Row(s, 10, 56);
            Btn(r3.transform, "blue", "Continue", 200, 52, "resume", 19);
        }

        static void BuildStuck(RectTransform parent)
        {
            var s = Sheet(parent, "stuck");
            Title(s, "No way out!");
            Body(s, "These pals can't all get home from here. Undo a step, add a spare rope, or start over.");
            var row = UiKit.Row(s, 8, 56);
            _ui.stuckUndo = Btn(row.transform, "blue", "Undo", 96, 52, "stuck_undo");
            _ui.stuckRope = Btn(row.transform, "green", "+ Rope", 104, 52, "stuck_rope");
            Btn(row.transform, "ghost", "Restart", 104, 52, "stuck_restart");
        }

        static void BuildRefill(RectTransform parent)
        {
            var s = Sheet(parent, "refill");
            _ui.refillTitle = Title(s, "+1 Undo");
            Body(s, "You have used the free ones in this level. Watch a short video or spend coins.");
            var row = UiKit.Row(s, 10, 56);
            Btn(row.transform, "green", "Watch video", 160, 52, "refill_video", 18, "icon_play");
            _ui.refillCoins = Btn(row.transform, "blue", "Use 40 coins", 160, 52, "refill_coins", 18);
            _ui.refillCoinsLabel = _ui.refillCoins.GetComponentInChildren<TextMeshProUGUI>();
            Link(s, "Not now", Res.Hex("#6A4A2C"), "refill_close");
        }

        static void BuildShop(RectTransform parent)
        {
            var s = Sheet(parent, "shop");
            Title(s, "Shop");
            foreach (var p in GameConfig.Products)
            {
                var card = UiKit.Img(s, "ui_card", p.Best ? Res.Hex("#FFE9B0") : UiKit.Cream, new Vector2(320, 64), true);
                card.name = "product " + p.Id;
                UiKit.Layout(card, 330, 64);
                var info = UiKit.Node("info", card.transform);
                info.anchorMin = Vector2.zero; info.anchorMax = Vector2.one;
                info.offsetMin = new Vector2(12, 6); info.offsetMax = new Vector2(-110, -6);
                UiKit.Stretch(UiKit.Text(info, p.Title, 17, UiKit.Ink, true, TextAlignmentOptions.TopLeft).rectTransform);
                UiKit.Stretch(UiKit.Text(info, p.Desc, 12, UiKit.Body, false, TextAlignmentOptions.BottomLeft).rectTransform);
                var b = Btn(card.transform, "blue", "—", 92, 40, "buy_" + p.Id, 15);
                var brt = (RectTransform)b.transform;
                brt.anchorMin = brt.anchorMax = new Vector2(1, 0.5f);
                brt.anchoredPosition = new Vector2(-56, 1);
                _ui.shopRows.Add(new GameUi.ShopRow { productId = p.Id, button = b, buttonImg = b.GetComponent<Image>(), price = b.GetComponentInChildren<TextMeshProUGUI>() });
            }
            _ui.shopNote = Body(s, "<size=13>Purchases turn on once the store is connected.</size>").gameObject;
            var row = UiKit.Row(s, 14, 52);
            Link(row.transform, "Restore purchases", Res.Hex("#6A4A2C"), "restore");
            Btn(row.transform, "blue", "Close", 110, 48, "shop_close", 18);
        }

        static void BuildWorld(RectTransform parent)
        {
            var s = Sheet(parent, "world");
            var row = UiKit.Row(s, 2, 64);
            row.name = "faces";
            for (int i = 0; i < 4; i++)
            {
                var holder = UiKit.Node("face", row.transform);
                UiKit.Layout(holder, 64, 64);
                var bob = UiKit.Node("bob", holder);
                bob.sizeDelta = new Vector2(64, 64);
                var img = UiKit.PalImg(bob, "fox", "happy", 64);
                img.rectTransform.anchoredPosition = new Vector2(0, 26);
                _ui.worldFaces.Add(img);
                _ui.bobbers.Add(bob);
            }
            _ui.worldEyebrow = UiKit.Text(s, "New scene unlocked!", 15, UiKit.Body);
            _ui.worldName = Title(s, "Sunny Meadow", 30);
            _ui.worldBody = Body(s, "");
            var r = UiKit.Row(s, 10, 56);
            Btn(r.transform, "green", "Let's go!", 200, 52, "world_go", 20);
        }

        // ------------------------------------------------------------------ win
        static void BuildWin()
        {
            var bg = UiKit.Img(_ui.root, "win_bg", Color.white, Vector2.zero, false, true);
            bg.name = "Win";
            _ui.win = bg.rectTransform;
            UiKit.Stretch(_ui.win);
            _ui.winGroup = bg.gameObject.AddComponent<CanvasGroup>();
            var safe = UiKit.Node("safe", _ui.win);
            UiKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            _ui.winRainbow = At(UiKit.Img(safe, "win_rainbow", Color.white, new Vector2(290, 150)).rectTransform, Top, 0, -64);
            _ui.winTitle = Outlined(safe, "Superb!", 52, Color.white, _outlinePurple);
            _ui.winTitleRt = At(_ui.winTitle.rectTransform, Top, 0, -62);
            _ui.winTitleRt.sizeDelta = new Vector2(360, 70);
            for (int i = 0; i < 3; i++)
            {
                _ui.wStars[i] = UiKit.Img(safe, "star_dark", Color.white, new Vector2(46, 46));
                At(_ui.wStars[i].rectTransform, Top, (i - 1) * 50, i == 1 ? -122 : -132);
            }
            _ui.habitat = At(UiKit.Img(safe, "win_habitat", Color.white, new Vector2(300, 200)).rectTransform, Top, 0, -268);
            for (int i = 0; i < 7; i++)
            {
                var img = UiKit.PalImg(_ui.habitat, "fox", "happy", 66);
                img.name = "pal slot " + i;
                _ui.winPals.Add(img);
            }
            string[] labels = { "Time", "Moves", "Win streak" };
            for (int i = 0; i < 3; i++)
            {
                var card = UiKit.Img(safe, "ui_card", Color.white, new Vector2(104, 62), true);
                _ui.cards[i] = At(card.rectTransform, Top, (i - 1) * 112, -414);
                var band = UiKit.Img(card.transform, "ui_card", Res.Hex("#3D8BFF"), new Vector2(104, 20), true);
                band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                band.rectTransform.anchoredPosition = new Vector2(0, -10);
                UiKit.Text(band.transform, labels[i], 12, Color.white).rectTransform.sizeDelta = new Vector2(104, 20);
                var v = UiKit.Text(card.transform, "0", 22, Res.Hex("#28324F"), true);
                v.rectTransform.sizeDelta = new Vector2(104, 40);
                v.rectTransform.anchoredPosition = new Vector2(0, -9);
                if (i == 0) _ui.wTime = v; else if (i == 1) _ui.wMoves = v; else _ui.wStreak = v;
                if (i < 2)
                {
                    var g = UiKit.Img(card.transform, "ui_circle", Res.Hex("#E5458B"), new Vector2(20, 20));
                    g.rectTransform.anchoredPosition = new Vector2(38, -17);
                    var gt = UiKit.Text(g.transform, "A", 12, Color.white, true);
                    gt.rectTransform.sizeDelta = new Vector2(20, 20);
                    if (i == 0) _ui.wTimeG = gt; else _ui.wMovesG = gt;
                }
            }
            _ui.wNote = UiKit.Text(safe, "", 14, Res.Hex("#D9DEFF"));
            At(_ui.wNote.rectTransform, Top, 0, -468).sizeDelta = new Vector2(340, 40);
            var coinRow = UiKit.Node("coins", safe);
            At(coinRow, Top, 0, -500).sizeDelta = new Vector2(160, 30);
            UiKit.Img(coinRow, "coin", Color.white, new Vector2(24, 24)).rectTransform.anchoredPosition = new Vector2(-28, 0);
            _ui.wCoins = UiKit.Text(coinRow, "+0", 20, Res.Hex("#FFD866"), true, TextAlignmentOptions.Left);
            _ui.wCoins.rectTransform.sizeDelta = new Vector2(80, 30);
            _ui.wCoins.rectTransform.anchoredPosition = new Vector2(28, 0);

            _ui.winButtons = UiKit.Node("buttons", safe);
            At(_ui.winButtons, Bottom, 0, 100).sizeDelta = new Vector2(240, 170);
            _ui.btnDouble = Btn(_ui.winButtons, "gold", "Double coins", 230, 50, "double", 20, "icon_play");
            ((RectTransform)_ui.btnDouble.transform).anchoredPosition = new Vector2(0, 54);
            var next = Btn(_ui.winButtons, "green", "Continue", 230, 54, "next", 22);
            ((RectTransform)next.transform).anchoredPosition = new Vector2(0, -6);
            var replay = Link(_ui.winButtons, "Replay this level", Res.Hex("#C9D0FF"), "replay");
            ((RectTransform)replay.transform).anchoredPosition = new Vector2(0, -60);

            for (int i = 0; i < 90; i++)
            {
                var c = UiKit.Img(_ui.win, "fx_square", Color.white, new Vector2(7, 4));
                c.name = "confetti";
                _ui.confettiPool.Add(c);
            }
        }

        // ------------------------------------------------------------------ toast + ad placeholder
        static void BuildToast(RectTransform top)
        {
            var bg = UiKit.Img(top, "ui_card", Res.Hex("#2E3B33"), new Vector2(300, 40), true);
            bg.name = "Toast";
            _ui.toast = At(bg.rectTransform, Top, 0, -140);
            _ui.toastText = UiKit.Text(bg.transform, "", 15, Color.white);
            _ui.toastText.rectTransform.sizeDelta = new Vector2(300, 40);
        }

        static void BuildAdCard()
        {
            var bg = UiKit.Img(_ui.root, null, new Color(0.04f, 0.05f, 0.08f, 0.92f), Vector2.zero, false, true);
            bg.name = "Ad Placeholder";
            UiKit.Stretch(bg.rectTransform);
            _ui.adCard = bg.gameObject;
            _ui.adTitle = UiKit.Text(bg.transform, "Reward video", 24, Color.white, true);
            _ui.adTitle.rectTransform.sizeDelta = new Vector2(320, 40);
            _ui.adTitle.rectTransform.anchoredPosition = new Vector2(0, 14);
            var sub = UiKit.Text(bg.transform, "Test placeholder. Real ads appear once the ad network is connected.", 14, new Color(1, 1, 1, 0.8f));
            sub.rectTransform.sizeDelta = new Vector2(320, 50);
            sub.rectTransform.anchoredPosition = new Vector2(0, -24);
        }
    }
}
