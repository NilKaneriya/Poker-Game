using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Poker.EditorTools
{
    /// <summary>
    /// One-click Android build into <repo>/Build/android/PokerGame.apk (landscape, installable
    /// directly on a phone). Uses the SDK, NDK and JDK that ship with the Unity Android module.
    /// Menu: Poker > Build Android APK.
    /// </summary>
    public static class AndroidBuilder
    {
        [MenuItem("Poker/Build Android APK")]
        public static void Build()
        {
            string dir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Build", "android"));
            Directory.CreateDirectory(dir);
            string apk = Path.Combine(dir, "PokerGame.apk");

            EditorUserBuildSettings.buildAppBundle = false; // APK, not an app bundle
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.fantosia.pokergame");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.forceInternetPermission = true; // the game talks to the server

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log(ok
                ? $"[AndroidBuilder] Build succeeded: {apk} ({report.summary.totalSize / (1024 * 1024)} MB, server {Core.ServerConfig.Load().serverUrl})"
                : $"[AndroidBuilder] Build FAILED ({report.summary.result}, {report.summary.totalErrors} errors). See the Console.");

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
