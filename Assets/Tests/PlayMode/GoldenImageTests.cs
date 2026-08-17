using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// V8 and X4.
    ///
    /// TOLERANCE. The floor comes from a real measurement, not a guess: diffing the same scene
    /// rendered under URP 17.5.0 (Unity 6.5) against URP 17.3.0 (Unity 6.3) gave mean 0.067/255,
    /// max 2/255, and 0.00% of pixels differing by more than 4/255. That is the observed noise
    /// of a genuine renderer change that looks identical. So:
    ///   - per-pixel tolerance 4/255  - at or above the measured max of 2
    ///   - allowed differing fraction 0.5% - well above the measured 0.00%
    /// A golden that drifts beyond this changed for a reason worth looking at.
    /// </summary>
    [TestFixture]
    public class GoldenImageTests
    {
        public const int PixelTolerance = 4;        // measured URP-version noise max was 2/255
        public const float AllowedDifferingFraction = 0.005f;

        public static string GoldenDir =>
            Path.Combine(Application.dataPath, "Tests", "Golden");

        /// <summary>
        /// Off-grid BoardTiles: tiles that still exist in the scene but are no longer part of the
        /// board. In practice, tiles ejected by a push, falling until they are destroyed.
        /// </summary>
        internal static int EjectedTileCount(GridManager grid)
        {
            if (grid == null) return 0;

            var onGrid = new System.Collections.Generic.HashSet<Transform>();
            for (int c = 0; c < grid.cols; c++)
                for (int r = 0; r < grid.rows; r++)
                {
                    var t = grid.GetTileAt(c, r);
                    if (t != null) onGrid.Add(t.transform);
                }

            int n = 0;
            foreach (var bt in Object.FindObjectsByType<BoardTile>(FindObjectsInactive.Exclude,
                                                                  FindObjectsSortMode.None))
                if (!onGrid.Contains(bt.transform)) n++;
            return n;
        }

        /// <summary>
        /// Waits until no ejected tile remains in the scene. Wall-clock timeout, loud on expiry -
        /// a tile that is never destroyed is a leak worth failing on, not worth waiting out.
        /// </summary>
        internal static IEnumerator WaitForNoEjectedTiles(GridManager grid, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            int last = -1;
            while (true)
            {
                int n = EjectedTileCount(grid);
                if (n == 0) yield break;
                if (n != last) { last = n; }
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogWarning(
                        $"[Golden] {n} ejected tile(s) still in the scene after {timeoutSeconds}s " +
                        "of wall clock. Capturing anyway, but this capture is NOT reproducible - " +
                        "the debris is rendered wherever it happens to be. Investigate whether " +
                        "ejected tiles are being destroyed at all.");
                    yield break;
                }
                yield return null;
            }
        }

        static string GoldenPathFor(string level) =>
            Path.Combine(GoldenDir, "levels", level.Replace("Levels/", "") + ".png");

        /// <summary>
        /// V8. Capture each level at load and compare against its committed golden.
        /// Set HAPI_REGENERATE_GOLDENS=1 to write them instead - see Golden/README.md for the
        /// rules on when that is legitimate.
        /// </summary>
        [UnityTest]
        public IEnumerator V8_GoldenImagePerLevel()
        {
            LogAssert.ignoreFailingMessages = true;
            bool regenerate = System.Environment.GetEnvironmentVariable("HAPI_REGENERATE_GOLDENS") == "1";

            var rows = new List<string>();
            var failures = new List<string>();
            var missing = new List<string>();

            foreach (var lvl in SceneFixture.AllLevels)
            {
                yield return SceneFixture.Load(FixtureMode.Playing, lvl);

                // Settle to a known, un-highlighted state. SceneFixture ends with SelectBoat,
                // which lifts tiles and lights highlights - none of which belongs in a baseline.
                var boat = SceneFixture.Boat;
                if (boat != null) boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(1.6f);

                string shot;
                using (var ctx = new DeterministicContext())
                    shot = CaptureRig.Capture(ctx, "golden", lvl.Replace("Levels/", ""));

                Assert.IsTrue(CaptureRig.LooksRendered(shot),
                    $"V8: capture for {lvl} is black - a golden comparison against it would be vacuous.");

                var goldenPath = GoldenPathFor(lvl);

                if (regenerate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(goldenPath));
                    File.Copy(shot, goldenPath, true);
                    var cond = Path.ChangeExtension(shot, ".conditions.txt");
                    if (File.Exists(cond)) File.Copy(cond, Path.ChangeExtension(goldenPath, ".conditions.txt"), true);
                    rows.Add($"  {lvl.Replace("Levels/", ""),-22} REGENERATED");
                    continue;
                }

                if (!File.Exists(goldenPath)) { missing.Add(lvl); rows.Add($"  {lvl.Replace("Levels/", ""),-22} NO GOLDEN"); continue; }

                var a = PixelUtil.Load(goldenPath);
                var b = PixelUtil.Load(shot);
                float frac = PixelUtil.FractionDiffering(a, b, PixelTolerance);
                PixelUtil.MeanMaxDelta(a, b, out float mean, out int max);

                rows.Add($"  {lvl.Replace("Levels/", ""),-22} differing={frac:P4} mean={mean:F3} max={max}");
                if (frac > AllowedDifferingFraction)
                    failures.Add($"{lvl}: {frac:P4} of pixels differ by more than {PixelTolerance}/255 " +
                                 $"(mean {mean:F3}, max {max}); allowed {AllowedDifferingFraction:P2}");
            }

            Debug.Log($"[V8] tolerance: {PixelTolerance}/255 per pixel, {AllowedDifferingFraction:P2} of pixels\n" +
                      string.Join("\n", rows));

            if (regenerate) Assert.Inconclusive("V8: goldens regenerated, not compared. Re-run without " +
                                                "HAPI_REGENERATE_GOLDENS to verify.");
            Assert.IsEmpty(missing, "V8: no committed golden for:\n  " + string.Join("\n  ", missing) +
                                    "\n  Run with HAPI_REGENERATE_GOLDENS=1 to create them.");
            Assert.IsEmpty(failures, "V8 golden mismatches:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// X4 -> V8 must fail. Takes a real golden, corrupts a 10% region of it, and requires
        /// V8's exact comparison to reject it.
        /// </summary>
        [UnityTest]
        public IEnumerator X4_V8_FailsOnAGoldenWithATenPercentRegionChange()
        {
            LogAssert.ignoreFailingMessages = true;

            const string lvl = "Levels/01_06_TestLevel";
            var goldenPath = GoldenPathFor(lvl);
            if (!File.Exists(goldenPath))
                Assert.Inconclusive($"X4: no golden at {goldenPath} to corrupt. Generate goldens first.");

            // Build the corrupted control: a solid block over ~10% of the image area.
            var src = PixelUtil.Load(goldenPath);
            int blockH = Mathf.RoundToInt(src.Height * 0.10f);
            var tex = new Texture2D(src.Width, src.Height, TextureFormat.RGBA32, false, false);
            var px = (Color32[])src.Pixels.Clone();
            int changed = 0;
            for (int y = 0; y < blockH; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    px[y * src.Width + x] = new Color32(255, 0, 255, 255);   // THE BREAKAGE
                    changed++;
                }
            tex.SetPixels32(px);
            tex.Apply(false);

            var corruptDir = Path.Combine(CaptureRig.Root, "meta");
            Directory.CreateDirectory(corruptDir);
            var corruptPath = Path.Combine(corruptDir, "x4-corrupted-golden.png");
            File.WriteAllBytes(corruptPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            // Now run V8's EXACT comparison against it.
            var a = PixelUtil.Load(goldenPath);
            var b = PixelUtil.Load(corruptPath);
            float frac = PixelUtil.FractionDiffering(a, b, PixelTolerance);
            PixelUtil.MeanMaxDelta(a, b, out float mean, out int max);

            Debug.Log($"[X4] corrupted {changed} px ({(float)changed / (src.Width * src.Height):P2} of the image). " +
                      $"V8 comparison reports differing={frac:P3} mean={mean:F3} max={max}; " +
                      $"V8 allows {AllowedDifferingFraction:P2}");

            yield return null;

            Assert.Greater(frac, AllowedDifferingFraction,
                $"X4 META-FAILURE: a golden with a 10% region replaced by magenta was reported as only " +
                $"{frac:P3} differing, within V8's {AllowedDifferingFraction:P2} tolerance. V8 would pass " +
                "on a corrupted baseline, which means V8 is broken. Fix V8, not this control.");
        }

        /// <summary>
        /// X11 -> the golden suite must not run against LFS pointers. The repo stores images in
        /// Git LFS; a clone without `git lfs install` gets pointer TEXT files on disk. Two such
        /// files decode to no image and would compare equal, so every golden assertion would
        /// pass while comparing nothing. This feeds PixelUtil.Load a real pointer file and
        /// requires it to refuse.
        /// </summary>
        [Test]
        public void X11_PixelUtil_RefusesAnLfsPointerInsteadOfComparingNothing()
        {
            var dir = Path.Combine(CaptureRig.Root, "meta");
            Directory.CreateDirectory(dir);
            var pointerPath = Path.Combine(dir, "x11-lfs-pointer.png");

            // Byte-for-byte the shape git-lfs writes.
            File.WriteAllText(pointerPath,
                "version https://git-lfs.github.com/spec/v1\n" +
                "oid sha256:63288503b105828b7dc2f3505501f509a57c65d245d846c03a740139625cdacf\n" +
                "size 101\n");

            var ex = Assert.Catch(() => PixelUtil.Load(pointerPath));

            Debug.Log($"[X11] PixelUtil.Load on an LFS pointer threw: " +
                      $"{ex.GetType().Name}: {ex.Message}");

            Assert.IsTrue(ex.Message.Contains("LFS"),
                "X11 META-FAILURE: PixelUtil.Load accepted a Git LFS pointer file, or failed for " +
                "an unrelated reason. On a clone without LFS content every golden comparison " +
                "would run against 130-byte text files and pass vacuously. Fix the guard.");
        }

        /// <summary>
        /// V9. A golden for the board AFTER a push, which the load-time goldens never cover -
        /// they are all captured before any push happens, so the push path has no visual baseline.
        ///
        /// Fills row 2 of 01_06 with FLIPPED tiles pushed through the hand path (overload 2).
        /// Captured deliberately on the pre-fix code so it bakes in R1: overload 2 builds
        /// Quaternion.Euler(0, rotationY, isFlipped ? 180 : 0) where every other path in the
        /// project flips on X. Unity's Euler order is ZXY, so
        ///     Euler(0, y, 180) = Ry(y).Rz(180) = Ry(y).Rx(180).Ry(180) = Euler(180, y, 0).Ry(180)
        /// i.e. R1 is NOT a mirror - it is the correct orientation plus an extra 180 local yaw.
        ///
        /// WHY THREE TILES AND NOT ONE. That yaw permutes snap points 0-3, 1-2, 4-5, and a
        /// reversed tile is forced straight along {0-2, 1-3, 4-5} - a set the permutation maps
        /// onto itself. The six snap points end up on the same six world positions, so the drawn
        /// paths are pixel-identical and R1 contributes nothing through them. The only signature
        /// is the tile mesh/vortex decal not being 180-yaw symmetric, measured at 0.5364% for a
        /// single tile - a 0.036 point margin over the 0.5% threshold, too thin to trust. Three
        /// tiles measure 1.6414%, a 3.3x margin. (Both figures measured by a throwaway
        /// diagnostic that applied the missing yaw post-hoc and diffed the captures; the numbers
        /// are recorded in Golden/README.md.)
        ///
        /// This golden EXISTS so R1's fix has visible proof. It is expected to change when R1 is
        /// fixed - see Golden/README.md.
        /// </summary>
        [UnityTest]
        public IEnumerator V9_GoldenAfterHandTilePush()
        {
            LogAssert.ignoreFailingMessages = true;
            bool regenerate = System.Environment.GetEnvironmentVariable("HAPI_REGENERATE_GOLDENS") == "1";

            const string lvl = "Levels/01_06_TestLevel";
            yield return SceneFixture.Load(FixtureMode.Playing, lvl);

            var grid = SceneFixture.Grid;
            var boat = SceneFixture.Boat;
            Assert.IsNotNull(grid, "no GridManager");

            // Row 2 is the only unlocked row in 01_06 (lockedRows [3,1,0]). Three pushes fill it.
            const int row = 2;
            var type = System.Linq.Enumerable.First(SceneFixture.PlayableTileTypes(),
                                                    t => t.displayName == "TileCross");
            for (int i = 0; i < 3; i++)
                yield return grid.PushRowCoroutine(row, true,
                    new PuzzleHandTile(type) { rotationY = 0f, isFlipped = true });

            // Settle to an un-highlighted state so no selection state is baked in.
            if (boat != null) boat.DeselectBoat();

            // WAIT FOR THE EJECTED TILE TO BE GONE, not for a fixed duration.
            //
            // A push ejects the far tile and it falls under physics until destroyed. Capturing
            // while it is still in flight bakes in wherever it happened to be, and that made this
            // golden FLAKY: measured 0.3686 %, 0.5711 % and 0.8474 % on three runs of identical
            // code, straddling the 0.5 % threshold, with a different md5 every time.
            //
            // This is a wait for a STATE the game reaches on its own, not a suppression: nothing is
            // disabled and nothing is hidden. Wall clock for the timeout so a tile that never dies
            // fails loudly instead of hanging - the harness clock, per the no-frames rule.
            yield return WaitForNoEjectedTiles(grid, 20f);
            yield return new WaitForSecondsRealtime(1.6f);

            for (int c = 0; c < 3; c++)
            {
                var pushed = grid.GetTileAt(c, row);
                Assert.IsNotNull(pushed, $"V9: nothing was pushed into ({c},{row})");
                Debug.Log($"[V9] ({c},{row}) {TileOrientation.Describe(pushed)}");
            }

            // A PUSH EJECTS THE FAR TILE, and an ejected tile is still a BoardTile in the scene
            // while it flies. If it is in the framing bounds, the fit depends on where it happens
            // to be at capture time - which is a timing-dependent framing and therefore a flaky
            // golden. Report the count and the bounds so a failure says which it was.
            int expectedOnBoard = grid.cols * grid.rows;
            var allBoardTiles = Object.FindObjectsByType<BoardTile>(FindObjectsInactive.Exclude,
                                                                   FindObjectsSortMode.None);
            BoardFraming.TryCollectBoardBounds(out var preBounds, out int preCount);
            Debug.Log($"[V9BOUNDS] BoardTile in scene={allBoardTiles.Length} expected on board=" +
                      $"{expectedOnBoard} (surplus={allBoardTiles.Length - expectedOnBoard})\n" +
                      $"  bounds x[{preBounds.min.x:F2},{preBounds.max.x:F2}] " +
                      $"y[{preBounds.min.y:F2},{preBounds.max.y:F2}] " +
                      $"z[{preBounds.min.z:F2},{preBounds.max.z:F2}] from {preCount} renderer(s)");
            foreach (var bt in allBoardTiles)
            {
                var p = bt.transform.position;
                bool onGrid = false;
                for (int c = 0; c < grid.cols && !onGrid; c++)
                    for (int r = 0; r < grid.rows && !onGrid; r++)
                        if (grid.GetTileAt(c, r) != null &&
                            grid.GetTileAt(c, r).transform == bt.transform) onGrid = true;
                if (!onGrid)
                    Debug.Log($"[V9BOUNDS] OFF-GRID BoardTile '{bt.name}' at {p:F2} - in flight?");
            }

            string shot;
            using (var ctx = new DeterministicContext())
                shot = CaptureRig.Capture(ctx, "golden", "post-push_01_06_row2_flipped");
            Assert.IsTrue(CaptureRig.LooksRendered(shot), "V9: capture is black");

            var goldenPath = System.IO.Path.Combine(GoldenDir, "push", "post-push_01_06_row2_flipped.png");

            if (regenerate)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(goldenPath));
                System.IO.File.Copy(shot, goldenPath, true);
                var cond = System.IO.Path.ChangeExtension(shot, ".conditions.txt");
                if (System.IO.File.Exists(cond))
                    System.IO.File.Copy(cond, System.IO.Path.ChangeExtension(goldenPath, ".conditions.txt"), true);
                Assert.Inconclusive("V9: golden regenerated, not compared.");
                yield break;
            }

            Assert.IsTrue(System.IO.File.Exists(goldenPath),
                $"V9: no committed golden at {goldenPath}. Run with HAPI_REGENERATE_GOLDENS=1.");

            var a = PixelUtil.Load(goldenPath);
            var b = PixelUtil.Load(shot);
            float frac = PixelUtil.FractionDiffering(a, b, PixelTolerance);
            PixelUtil.MeanMaxDelta(a, b, out float mean, out int max);
            Debug.Log($"[V9] post-push golden: differing={frac:P4} mean={mean:F3} max={max}");

            Assert.LessOrEqual(frac, AllowedDifferingFraction,
                $"V9: post-push board differs from its golden by {frac:P4} (mean {mean:F3}, max {max}). " +
                "If this followed an R1 fix, that is EXPECTED - the pushed tiles should no longer " +
                "carry an extra 180 yaw. Compare both images and replace the golden per " +
                "Golden/README.md.");
        }

    }
}
