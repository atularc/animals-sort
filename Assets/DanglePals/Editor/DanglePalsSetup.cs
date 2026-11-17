using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;

namespace DanglePals.EditorTools
{
    /// <summary>One-click project setup: fonts, scene, player settings for small mobile builds.</summary>
    public static class DanglePalsSetup
    {
        const string ScenePath = "Assets/DanglePals/Scenes/Game.unity";

        [MenuItem("Dangle Pals/Run Full Setup")]
        public static void RunAll()
        {
            ReimportAssets();
            CreateFonts();
            BuildArtLibrary();
            ConfigurePlayer();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[DanglePals] Setup complete.");
        }

        [MenuItem("Dangle Pals/Reimport Art And Audio")]
        public static void ReimportAssets()
        {
            AssetDatabase.ImportAsset("Assets/DanglePals/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("Dangle Pals/Create Fonts")]
        public static void CreateFonts()
        {
            if (TMP_Settings.instance == null)
                TMP_PackageResourceImporter.ImportResources(true, false, false);
            MakeFont("Assets/DanglePals/Art/Fonts/Fredoka-SemiBold.ttf", "Assets/DanglePals/Art/Fonts/Fredoka SemiBold SDF.asset");
            MakeFont("Assets/DanglePals/Art/Fonts/Fredoka-Bold.ttf", "Assets/DanglePals/Art/Fonts/Fredoka Bold SDF.asset");
        }

        static void MakeFont(string ttf, string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) { Debug.LogError("[DanglePals] Missing font " + ttf); return; }
            var fa = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTextures[0].name = fa.name + " Atlas";
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            // Pre-bake the common characters so the first frame never stalls.
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]_abcdefghijklmnopqrstuvwxyz·×—–’…+");
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dangle Pals/Configure Player Settings")]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Dangle Pals";
            PlayerSettings.productName = "Dangle Pals";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.danglepals.game");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.danglepals.game");

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Small builds: IL2CPP, ARM64 only, high managed stripping, strip engine code.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.Android.optimizedFramePacing = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            // Size: optimise IL2CPP for size, Master config, plain Activity entry point, R8 minify.
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.iOS, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Master);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.iOS, Il2CppCompilerConfiguration.Master);
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.minifyRelease = true;
            PlayerSettings.Android.minifyDebug = false;
            SlimProject();

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.56f, 0.83f, 0.96f);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DanglePals/Art/Icon/app_icon.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        public const string ArtPath = "Assets/DanglePals/Data/GameArt.asset";

        [MenuItem("Dangle Pals/Build Art Library")]
        public static GameArt BuildArtLibrary()
        {
            Directory.CreateDirectory("Assets/DanglePals/Data");
            var art = AssetDatabase.LoadAssetAtPath<GameArt>(ArtPath);
            if (art == null) { art = ScriptableObject.CreateInstance<GameArt>(); AssetDatabase.CreateAsset(art, ArtPath); }
            art.sprites.Clear(); art.pals.Clear(); art.clips.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/DanglePals/Art/Sprites" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                art.sprites.Add(new GameArt.SpriteEntry { key = Path.GetFileNameWithoutExtension(path), sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) });
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/DanglePals/Art/Pals" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                art.pals.Add(new GameArt.SpriteEntry { key = Path.GetFileNameWithoutExtension(path), sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) });
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/DanglePals/Art/Audio" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                art.clips.Add(new GameArt.ClipEntry { key = Path.GetFileNameWithoutExtension(path), clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) });
            }
            art.fontSemiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/DanglePals/Art/Fonts/Fredoka SemiBold SDF.asset");
            art.fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/DanglePals/Art/Fonts/Fredoka Bold SDF.asset");
            art.levels = AssetDatabase.LoadAssetAtPath<TextAsset>(DanglePalsLevels.BankPath);
            art.ResetIndex();
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DanglePals] Art library: {art.sprites.Count} sprites, {art.pals.Count} pal faces, {art.clips.Count} clips");
            return art;
        }

        static SpriteRenderer MakeSr(string name, Transform parent, string sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? Res.Sprite(sprite) : null;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Removes engine extras the game never uses, so they stay out of the build.</summary>
        [MenuItem("Dangle Pals/Slim Project (build size)")]
        public static void SlimProject()
        {
            // TextMesh Pro: make Fredoka the default font, drop the sample font and emoji sheet.
            var fredoka = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/DanglePals/Art/Fonts/Fredoka SemiBold SDF.asset");
            var settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            var tmp = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
            if (tmp != null && fredoka != null)
            {
                var so = new SerializedObject(tmp);
                so.FindProperty("m_defaultFontAsset").objectReferenceValue = fredoka;
                var sprite = so.FindProperty("m_defaultSpriteAsset"); if (sprite != null) sprite.objectReferenceValue = null;
                var fallbacks = so.FindProperty("m_fallbackFontAssets"); if (fallbacks != null) fallbacks.ClearArray();
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            foreach (var path in new[] { "Assets/TextMesh Pro/Resources/Fonts & Materials", "Assets/TextMesh Pro/Fonts", "Assets/TextMesh Pro/Sprites", "Assets/TextMesh Pro/Resources/Sprite Assets", "Assets/TextMesh Pro/Resources/Style Sheets" })
                if (AssetDatabase.IsValidFolder(path)) AssetDatabase.DeleteAsset(path);

            // URP: the 2D renderer needs no post-processing data (uber shader, film grain, SMAA textures).
            foreach (var guid in AssetDatabase.FindAssets("t:Renderer2DData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                var so = new SerializedObject(data);
                var pp = so.FindProperty("m_PostProcessData");
                if (pp != null) { pp.objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dangle Pals/Create Game Scene")]
        public static void CreateScene()
        {
            var art = AssetDatabase.LoadAssetAtPath<GameArt>(ArtPath) ?? BuildArtLibrary();
            Res.Art = art;
            Directory.CreateDirectory("Assets/DanglePals/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.22f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.56f, 0.83f, 0.96f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false;

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // scenery
            var scenery = new GameObject("Scenery").transform;
            var fill = MakeSr("ground fill", scenery, "fx_square", -101);
            var backdrop = MakeSr("backdrop", scenery, "bg_0", -100);
            var clouds = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++) clouds[i] = MakeSr("cloud " + i, scenery, "cloud", -90);
            var motes = new SpriteRenderer[14];
            for (int i = 0; i < 14; i++) { motes[i] = MakeSr("mote " + i, scenery, "fx_soft", -80); motes[i].color = new Color(1f, 1f, 0.94f, 0.7f); }
            var branch = MakeSr("branch", scenery, "branch", 25);
            var hand = MakeSr("hint hand", scenery, "hand", 85);

            // audio: pooled sources authored here
            var audioGo = new GameObject("Audio");
            var audio = audioGo.AddComponent<AudioManager>();
            for (int i = 0; i < 14; i++) { var s = audioGo.AddComponent<AudioSource>(); s.playOnAwake = false; audio.pool.Add(s); }
            audio.music = audioGo.AddComponent<AudioSource>();
            audio.music.playOnAwake = false; audio.music.loop = true; audio.music.volume = 0.55f;
            audio.music.clip = art.Clip("music_meadow");

            // particles: sprites + a pre-made pool
            var fxGo = new GameObject("Fx");
            var fx = fxGo.AddComponent<Fx>();
            fx.heart = Res.Sprite("fx_heart"); fx.petal = Res.Sprite("fx_petal"); fx.spark = Res.Sprite("fx_spark");
            fx.soft = Res.Sprite("fx_soft"); fx.ring = Res.Sprite("fx_ring"); fx.coin = Res.Sprite("coin");
            for (int i = 0; i < 160; i++) { var p = MakeSr("p", fxGo.transform, "fx_spark", 90); p.gameObject.SetActive(false); fx.pool.Add(p); }

            // UI (entire canvas authored here) + event system
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var ui = UiBuilder.Build(null, audio);

            var game = new GameObject("Game");
            var gc = game.AddComponent<GameController>();
            gc.art = art; gc.ui = ui; gc.audioManager = audio; gc.fx = fx;
            gc.backdrop = backdrop; gc.groundFill = fill; gc.branchSr = branch; gc.handSr = hand;
            gc.cloudSrs = clouds; gc.moteSrs = motes;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
