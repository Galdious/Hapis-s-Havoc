using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// W1 — ONE IMAGE PER LEVEL, WITH NOTHING SWITCHED OFF.
    ///
    /// Every other capture in this suite is taken through <see cref="DeterministicContext"/>, which
    /// suppresses fifteen things so that images are byte-reproducible. That is the right trade for
    /// a golden and the wrong one for knowing what the game looks like: three real bugs have hidden
    /// behind individually reasonable suppressions - the hand palette, BoardFraming being
    /// harness-only, and CinemachineBrain. See docs/audit/HARNESS_DIVERGENCE.md.
    ///
    /// So this captures the LIVE camera with the suppression list at its genuine minimum:
    ///
    ///   Brain live         - the camera is the real one, driven by the active vCam
    ///   Driver live        - framing is applied by the game, not computed by the test
    ///   UCC live           - pan and its rubberband are running
    ///   Hand palettes ON   - visible, exactly as the player sees them
    ///   Tile scale natural - whatever ScaleIn has reached, not forced to one
    ///   Shader time ON     - _Time advances, so time-driven effects are moving
    ///   Coroutines live    - boat, paths, grid, all of it
    ///
    /// The single unavoidable divergence is the RenderTexture standing in for the screen, because
    /// reading pixels needs a target we own. That is divergence 12 and it cannot be removed.
    ///
    /// THESE ARE NOT GOLDENS AND NOTHING IS ASSERTED ABOUT THEM. They are not deterministic - that
    /// is the point - so comparing them would produce noise, and promoting one to a baseline would
    /// re-introduce the very suppressions it exists to avoid. The only assertions here are that the
    /// capture step actually ran and wrote files; without those the test could silently do nothing
    /// and still look green, which would be its own version of the problem it documents.
    ///
    /// The output is for a human to LOOK AT.
    /// </summary>
    [TestFixture]
    public class WartsAndAllTests
    {
        /// <summary>
        /// Long side of the capture. The SHAPE is taken from the live screen, not fixed at
        /// 1080x1920 - see CaptureSizeForLiveScreen.
        /// </summary>
        const int LongSide = 1440;

        /// <summary>
        /// A capture whose aspect matches the aspect THE LIVE CAMERA WAS FRAMED FOR.
        ///
        /// This is not a cosmetic choice, and getting it wrong produced a false alarm worth
        /// recording. BoardFramingDriver picks its layout from `Screen.width/height`, and
        /// batchmode's dummy window is 640x480 - LANDSCAPE. Capturing that camera into a portrait
        /// 1080x1920 RenderTexture rendered every board oversized and cropped at the left edge,
        /// which read exactly like a live framing bug. It was not: the camera had correctly framed
        /// a landscape rect, and the capture then reinterpreted it as portrait.
        ///
        /// So the RT takes the live screen's shape. The image is then a faithful picture of what
        /// the live camera framed - which is the only thing W1 can honestly claim.
        ///
        /// WHAT THIS STILL CANNOT SHOW: the portrait framing the player actually gets. Batchmode
        /// has no phone-shaped screen, so the Portrait branch of BoardLayout is never exercised
        /// here. The deterministic captures cover portrait by pinning the layout as an INPUT; W1
        /// covers "nothing suppressed". Neither covers both, and that gap is recorded in
        /// docs/audit/HARNESS_DIVERGENCE.md rather than papered over.
        /// </summary>
        static void CaptureSizeForLiveScreen(out int width, out int height)
        {
            int sw = Mathf.Max(1, Screen.width);
            int sh = Mathf.Max(1, Screen.height);
            if (sw >= sh) { width = LongSide; height = Mathf.Max(1, Mathf.RoundToInt(LongSide * (float)sh / sw)); }
            else          { height = LongSide; width  = Mathf.Max(1, Mathf.RoundToInt(LongSide * (float)sw / sh)); }
        }

        [UnityTest]
        public IEnumerator W1_WartsAndAllCapturePerLevel()
        {
            LogAssert.ignoreFailingMessages = true;

            var lines = new List<string>();
            var written = new List<string>();

            foreach (var lvl in SceneFixture.AllLevels)
            {
                yield return SceneFixture.Load(FixtureMode.Playing, lvl);

                // Deselect only. SceneFixture ends with SelectBoat, which is a test action rather
                // than the state a player arrives in - but nothing else is touched, and in
                // particular no coroutine is stopped and no component disabled.
                var boat = SceneFixture.Boat;
                if (boat != null) boat.DeselectBoat();

                // WALL CLOCK, and generously. The driver waits for the board bounds to settle for
                // 0.25 s of GAME time, and game time runs about 15x slower than real in batchmode
                // (divergence #7), so a wall-clock wait has to be several seconds to cover it.
                // A frame count here would mean nothing at all.
                yield return new WaitForSecondsRealtime(6f);

                var cam = Camera.main;
                if (cam == null)
                {
                    lines.Add($"  {lvl,-26} NO Camera.main - nothing captured");
                    continue;
                }

                string name = lvl.Replace("Levels/", "");
                CaptureSizeForLiveScreen(out int w, out int h);
                string shot = CaptureRig.CaptureLive(cam, w, h, "warts", name);
                written.Add(shot);

                // Report what the live camera actually was, so the image is interpretable later.
                // Read from basis vectors, never eulerAngles - CLAUDE.md gotcha 3.
                float pitch = BoardFraming.PitchOf(cam.transform);
                bool rendered = CaptureRig.LooksRendered(shot);

                // THE ASPECT THE LIVE CAMERA WAS FRAMED FOR. Decisive for interpreting these
                // images: BoardFramingDriver picks its layout from the SCREEN, and batchmode's
                // dummy window is not a phone. If that orientation is not Portrait, the live
                // camera was framed for a different rect than this portrait RT, and any cropping
                // in the image is an artifact of the capture rather than a defect in the game.
                var liveMode = GameManager.Instance != null
                    ? GameManager.Instance.currentMode : OperatingMode.Playing;
                var liveOrientation = BoardFramingDriver.OrientationFor(
                    liveMode, Screen.width, Screen.height);

                lines.Add($"  {name,-26} " +
                          $"ortho={cam.orthographic} size={cam.orthographicSize:F2} " +
                          $"pos={cam.transform.position:F2} pitch={pitch:F1} " +
                          $"rendered={rendered}\n" +
                          $"    {"",-24} screen={Screen.width}x{Screen.height} " +
                          $"liveOrientation={liveOrientation} captureRT={w}x{h} (aspect matched)");

                // Loud, but NOT an assertion. A black frame here is a real finding about the live
                // path and should be visible in the log - yet failing on it would make this an
                // image check, which is exactly what W1 must not become.
                if (!rendered)
                    Debug.LogWarning($"[W1] {name} looks black. That is a finding about the LIVE " +
                                     "render path, not a harness problem - investigate it, but W1 " +
                                     "deliberately does not fail on image content.");
            }

            Debug.Log($"[W1] warts-and-all captures - LIVE camera, nothing suppressed except the " +
                      $"render target\n  written to {CaptureRig.PathFor("warts", "<level>")}\n" +
                      $"  screen={Screen.width}x{Screen.height} portrait={HarnessScreen.IsPortrait}" +
                      (HarnessScreen.IsPortrait
                          ? " - the live driver is framing the PLAYER'S orientation."
                          : " - LANDSCAPE. The live driver is NOT framing the player's " +
                            "orientation; see HARNESS_DIVERGENCE.md #9.") + "\n" +
                      string.Join("\n", lines));

            // The only assertions: the capture step ran, and it produced one file per level.
            // Never about what is IN the images.
            Assert.AreEqual(SceneFixture.AllLevels.Length, written.Count,
                "W1: did not produce one capture per level, so the run proves nothing about any " +
                "level it skipped.");
            foreach (var p in written)
                Assert.IsTrue(System.IO.File.Exists(p), $"W1: capture file missing: {p}");
        }
    }
}
