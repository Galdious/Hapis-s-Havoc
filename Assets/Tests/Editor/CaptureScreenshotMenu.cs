using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HapisHavoc.Tests.EditorTools
{
    /// <summary>
    /// The human bridge. Press this while playing and the Game view lands in
    /// TestOutput/Captures/manual/ with the same conditions sidecar every harness capture
    /// carries - so "here is what I saw" is reproducible rather than a description.
    /// </summary>
    public static class CaptureScreenshotMenu
    {
        const string MenuPath = "Hapi's Havoc/Capture Screenshot %#h";   // Cmd/Ctrl+Shift+H

        [MenuItem(MenuPath)]
        public static void CaptureGameView()
        {
            var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                   "TestOutput", "Captures", "manual");
            Directory.CreateDirectory(dir);

            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string name = EditorApplication.isPlaying ? $"play_{stamp}" : $"edit_{stamp}";
            string png = Path.Combine(dir, name + ".png");

            // ScreenCapture writes asynchronously on the next frame, so the sidecar is written
            // immediately and the PNG is reported once it lands.
            ScreenCapture.CaptureScreenshot(png);

            File.WriteAllText(Path.ChangeExtension(png, ".conditions.txt"),
                Conditions() + "\n");

            Debug.Log($"[Capture Screenshot] writing {png}\n  {Conditions()}\n" +
                      "  (ScreenCapture is asynchronous - the file appears within a frame or two.)");
            EditorUtility.RevealInFinder(dir);
        }

        [MenuItem(MenuPath, true)]
        public static bool CaptureGameViewValidate() => true;

        /// <summary>
        /// Mirrors DeterministicContext.Conditions so a manual capture is directly comparable
        /// with a harness capture.
        /// </summary>
        static string Conditions()
        {
            var rp = QualitySettings.renderPipeline
                     ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            string quality = "unknown";
            var names = QualitySettings.names;
            int idx = QualitySettings.GetQualityLevel();
            if (idx >= 0 && idx < names.Length) quality = names[idx];

            return $"editor={Application.unityVersion} " +
                   $"quality={quality} " +
                   $"buildTarget={EditorUserBuildSettings.activeBuildTarget} " +
                   $"screen={Screen.width}x{Screen.height} " +
                   $"colorSpace={QualitySettings.activeColorSpace} " +
                   $"renderPipeline={(rp != null ? rp.name : "default")} " +
                   $"playing={EditorApplication.isPlaying} " +
                   $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                   $"mode={(Application.isPlaying && GameManager.Instance != null ? GameManager.Instance.currentMode.ToString() : "n/a")}";
        }
    }
}
