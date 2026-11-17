using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DanglePals.EditorTools
{
    /// <summary>Builds: an APK for testing on a phone, an AAB for Google Play, and the Xcode project for iOS.</summary>
    public static class DanglePalsBuild
    {
        static readonly string[] Scenes = { "Assets/DanglePals/Scenes/Game.unity" };

        [MenuItem("Dangle Pals/Build/Android APK (test)")]
        public static string BuildApk() => BuildAndroid(false);

        [MenuItem("Dangle Pals/Build/Android App Bundle (Google Play)")]
        public static string BuildAab() => BuildAndroid(true);

        static string BuildAndroid(bool bundle)
        {
            EditorUserBuildSettings.buildAppBundle = bundle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            Directory.CreateDirectory("Builds");
            string path = bundle ? "Builds/DanglePals.aab" : "Builds/DanglePals.apk";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes, locationPathName = path, target = BuildTarget.Android, options = BuildOptions.None,
            });
            return Summary(report, path);
        }

        [MenuItem("Dangle Pals/Build/iOS Xcode Project")]
        public static string BuildIos()
        {
            Directory.CreateDirectory("Builds/iOS");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes, locationPathName = "Builds/iOS", target = BuildTarget.iOS, options = BuildOptions.None,
            });
            return Summary(report, "Builds/iOS");
        }

        static string Summary(BuildReport report, string path)
        {
            var s = report.summary;
            string msg = $"{s.result} {path} size={(s.totalSize / 1024f / 1024f):0.00} MB time={s.totalTime.TotalSeconds:0}s errors={s.totalErrors}";
            if (File.Exists(path)) msg += $" file={(new FileInfo(path).Length / 1024f / 1024f):0.00} MB";
            Debug.Log("[DanglePals] Build " + msg);
            return msg;
        }
    }
}
