using UnityEditor;
using UnityEngine;

namespace DanglePals.EditorTools
{
    /// <summary>Import rules for every Dangle Pals texture and sound. Tuned for small mobile builds.</summary>
    public sealed class DanglePalsImporter : AssetPostprocessor
    {
        const string Root = "Assets/DanglePals/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            if (assetPath.Contains("/Art/Icon/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.Tight;
            s.spriteAlignment = (int)SpriteAlignment.Center;

            // 1 world unit = 100 design pixels. Pals are 192 px for one unit, backdrops are 2x, the rest 3x.
            if (assetPath.Contains("/Art/Pals/")) s.spritePixelsPerUnit = 192;
            else if (file.StartsWith("bg_")) s.spritePixelsPerUnit = 200;
            else s.spritePixelsPerUnit = 300;

            Vector4 border = Vector4.zero;
            switch (file)
            {
                case "ui_pill": border = new Vector4(57, 57, 57, 57); break;
                case "ui_panel": border = new Vector4(78, 78, 78, 78); break;
                case "ui_card": border = new Vector4(42, 42, 42, 42); break;
                case "btn_blue": case "btn_green": case "btn_gold": case "btn_ghost": case "btn_red": border = new Vector4(54, 69, 54, 54); break;
            }
            if (border != Vector4.zero || file == "rope" || file.StartsWith("bg_")) s.spriteMeshType = SpriteMeshType.FullRect;
            if (file == "rope") { s.wrapMode = TextureWrapMode.Repeat; }
            ti.SetTextureSettings(s);
            ti.spriteBorder = border;

            // ASTC on phones: bigger blocks where detail is soft (backdrops), finer for faces and UI edges.
            var fmt = file.StartsWith("bg_") ? TextureImporterFormat.ASTC_8x8
                    : assetPath.Contains("/Art/Pals/") || file.StartsWith("win_habitat") ? TextureImporterFormat.ASTC_6x6
                    : TextureImporterFormat.ASTC_4x4;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform, overridden = true, maxTextureSize = 2048, format = fmt,
                    compressionQuality = 50,
                });
            }
            ti.textureCompression = TextureImporterCompression.Compressed;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ai = (AudioImporter)assetImporter;
            bool music = System.IO.Path.GetFileName(assetPath).StartsWith("music_");
            ai.forceToMono = !music;
            ai.loadInBackground = music;
            var st = ai.defaultSampleSettings;
            st.compressionFormat = AudioCompressionFormat.Vorbis;
            st.quality = music ? 0.45f : 0.5f;
            st.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            st.preloadAudioData = !music;
            st.sampleRateSetting = music ? AudioSampleRateSetting.PreserveSampleRate : AudioSampleRateSetting.OverrideSampleRate;
            st.sampleRateOverride = 32000;
            ai.defaultSampleSettings = st;
        }
    }
}
