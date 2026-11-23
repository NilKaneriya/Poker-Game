using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Poker.EditorTools
{
    /// <summary>
    /// One-click WebGL build into <repo>/Build/webgl. Compression is switched off so the build
    /// loads on any static host (Vercel, python http.server, ...) without special
    /// Content-Encoding headers; the host compresses on the fly.
    /// Menu: Poker > Build WebGL. Command line (Unity closed):
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Poker.EditorTools.WebGLBuilder.Build
    /// </summary>
    public static class WebGLBuilder
    {
        [MenuItem("Poker/Build WebGL")]
        public static void Build()
        {
            string outputPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Build", "webgl"));
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;

            RemoveStaleCompressedFiles(Path.Combine(outputPath, "Build"));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;

            Debug.Log(ok
                ? $"[WebGLBuilder] Build succeeded: {outputPath} ({report.summary.totalSize / (1024 * 1024)} MB, server {Core.ServerConfig.Load().serverUrl})"
                : $"[WebGLBuilder] Build FAILED ({report.summary.result}, {report.summary.totalErrors} errors). See the Console.");

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        // Leftovers from an earlier compressed build would confuse hosts that serve everything in the folder.
        private static void RemoveStaleCompressedFiles(string buildDir)
        {
            if (!Directory.Exists(buildDir)) return;
            foreach (string file in Directory.GetFiles(buildDir).Where(f => f.EndsWith(".br") || f.EndsWith(".gz")))
                File.Delete(file);
        }
    }
}
