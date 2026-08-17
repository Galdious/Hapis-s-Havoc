using UnityEngine;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// Makes the harness screen PORTRAIT, so the live path frames the shape the player gets.
    ///
    /// WHY THIS IS NEEDED AT ALL. This is a portrait mobile game, but batchmode's default screen is
    /// **640x480 - landscape**. `BoardFramingDriver` picks its layout from `Screen.width/height`,
    /// so with the default window the running game frames a LANDSCAPE rect and the Portrait branch
    /// of `BoardLayout` was never exercised by the live path even once. That is divergence #9.
    ///
    /// WHY IT IS DONE HERE RATHER THAN AT THE CAPTURE. The first attempt at the warts-and-all
    /// capture compensated for it - rendering the landscape-framed camera into a portrait
    /// RenderTexture - and produced images that looked exactly like a serious framing bug. Fixing
    /// the aspect at capture time is how the divergence stayed invisible in the first place. The
    /// screen is made portrait so the driver's own, unmodified logic selects Portrait.
    ///
    /// WHAT ACTUALLY WORKS, measured rather than assumed:
    ///
    ///   -screen-width 1080 -screen-height 1920   IGNORED in Editor batchmode. Those arguments are
    ///                                            honoured by a standalone player, not by the
    ///                                            Editor's dummy view. Screen stayed 640x480.
    ///   Screen.SetResolution(1080, 1920, false)  NO-OP in the Editor, with or without a frame or
    ///                                            half a second of settling. Screen stayed 640x480.
    ///   PlayModeWindow.SetCustomRenderingResolution
    ///                                            WORKS. Screen becomes 1080x1920 on the next
    ///                                            frame. This is the one.
    ///
    /// The flags are still passed in `run-tests.sh` because they are correct for a player build;
    /// this class is what makes the Editor honour it.
    ///
    /// REACHED BY REFLECTION, deliberately. `UnityEditor.PlayModeWindow` lives in the editor
    /// assembly, which a PlayMode test assembly must not reference - adding that reference would
    /// make the whole test assembly editor-only. Reflection keeps the dependency at runtime and
    /// confined to this file, and it degrades to a loud warning rather than a compile error if the
    /// API moves.
    /// </summary>
    public static class HarnessScreen
    {
        public const int PortraitWidth = 1080;
        public const int PortraitHeight = 1920;

        static bool _applied;

        /// <summary>True once the screen is actually reporting portrait.</summary>
        public static bool IsPortrait => Screen.height > Screen.width;

        /// <summary>
        /// Idempotent. Safe to call from every fixture load; does its work once.
        /// Returns false if the screen could not be made portrait, having said why.
        /// </summary>
        public static bool EnsurePortrait()
        {
            if (IsPortrait) { _applied = true; return true; }
            if (_applied) return IsPortrait;      // tried already and it did not take
            _applied = true;

            var type = System.Type.GetType("UnityEditor.PlayModeWindow, UnityEditor");
            var method = type?.GetMethod("SetCustomRenderingResolution",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            if (method == null)
            {
                Debug.LogWarning(
                    "[HarnessScreen] UnityEditor.PlayModeWindow.SetCustomRenderingResolution is " +
                    "unavailable, so the harness screen stays landscape and the live path will " +
                    "frame a landscape rect. This is divergence #9 in " +
                    "docs/audit/HARNESS_DIVERGENCE.md - do NOT compensate for it at capture time.");
                return false;
            }

            try
            {
                method.Invoke(null, new object[] { (uint)PortraitWidth, (uint)PortraitHeight,
                                                   "HapiPortrait" });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HarnessScreen] SetCustomRenderingResolution threw: " +
                                 $"{e.InnerException?.Message ?? e.Message}. Screen stays " +
                                 $"{Screen.width}x{Screen.height}.");
                return false;
            }

            // The change lands on the next frame, so callers that need it must yield once. The
            // fixture does, before anything reads Screen.
            return true;
        }

        /// <summary>Diagnostic string for logs and capture sidecars.</summary>
        public static string Describe() =>
            $"screen={Screen.width}x{Screen.height} portrait={IsPortrait}";
    }
}
