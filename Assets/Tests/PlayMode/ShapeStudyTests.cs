using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// The board-shape study capture pass. Measures, captures, and asserts nothing about the
    /// result - the conclusion is a human judgement made from the images, not a threshold.
    /// Orthographic tilted, which won the projection comparison on both foreshortening (1.000
    /// vs 1.114) and fill (47.75% vs 40.89%).
    /// </summary>
    [TestFixture]
    public class ShapeStudyTests
    {
        const int TouchTargetFloorPx = 44;      // mobile accessibility floor

        static readonly string[] Levels =
        {
            "Levels/study_3x3", "Levels/study_3x6", "Levels/study_3x8", "Levels/study_6x6",
        };

        /// <summary>
        /// Median horizontal run length of path-coloured pixels. The path is a saturated green
        /// (theme pathShallow ~ RGB 90,255,90) that nothing else on the board uses, so a run of
        /// it across a scanline IS the line's on-screen width.
        /// </summary>
        static float MedianPathRunPx(PixelUtil.Image img)
        {
            var runs = new List<int>();
            for (int y = 0; y < img.Height; y += 4)
            {
                int run = 0;
                for (int x = 0; x < img.Width; x++)
                {
                    var c = img.Pixels[y * img.Width + x];
                    bool isPath = c.g > 170 && c.r < 160 && c.b < 160 && (c.g - c.r) > 60 && (c.g - c.b) > 60;
                    if (isPath) run++;
                    else { if (run > 0 && run < 200) runs.Add(run); run = 0; }
                }
            }
            if (runs.Count == 0) return 0f;
            runs.Sort();
            return runs[runs.Count / 2];
        }

        /// <summary>
        /// Z2. The same levels framed CORRECTLY at two pitches, so the choice is made from
        /// images rather than arguments. 70 is the authored VCam_Player angle; 55 is what the
        /// shape study was measured at. Each is fitted for its own angle - a fit computed at one
        /// pitch and rendered at another is simply wrong, since extU scales with cos(pitch).
        ///
        /// Captures and measures only. Nothing here changes the authored pitch.
        /// </summary>
        [UnityTest]
        public IEnumerator Z2_PitchComparison()
        {
            LogAssert.ignoreFailingMessages = true;
            var rows = new List<string>();

            foreach (var lvl in new[] { "Levels/study_3x3", "Levels/study_3x6" })
            foreach (float pitch in new[] { 70f, 55f })
            {
                yield return SceneFixture.Load(FixtureMode.Playing, lvl);
                var grid = SceneFixture.Grid;
                var boat = SceneFixture.Boat;
                if (boat != null) boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(1.4f);

                string name = lvl.Replace("Levels/", "");
                string shot; float fill; Vector2 tilePx;

                using (var ctx = new DeterministicContext(
                    projection: BoardFraming.Projection.OrthographicTilted, pitchDegrees: pitch))
                {
                    ctx.Quiesce();
                    ctx.FrameBoard();
                    fill = BoardFraming.FillFraction(ctx.FramedBounds, ctx.Cam);
                    var mid = grid.GetTileAt(grid.cols / 2, grid.rows / 2);
                    var rend = mid != null ? mid.GetComponentInChildren<MeshRenderer>() : null;
                    var r = rend != null ? BoardFraming.ViewportRectOf(rend.bounds, ctx.Cam) : new Rect();
                    tilePx = new Vector2(r.width * ctx.RtWidth, r.height * ctx.RtHeight);
                    shot = CaptureRig.Capture(ctx, "pitch", $"{name}_pitch{pitch:F0}");
                }

                float pathPx = MedianPathRunPx(PixelUtil.Load(shot));
                rows.Add($"  {name,-11} pitch={pitch,4:F0}  fill={fill,7:P2}  " +
                         $"tile={tilePx.x,6:F1}x{tilePx.y,5:F1}px  path={pathPx,4:F1}px");
                Assert.IsTrue(CaptureRig.LooksRendered(shot), $"{name}@{pitch}: capture is black");
                yield return new WaitForSecondsRealtime(0.2f);
            }

            Debug.Log("[Z2] pitch comparison, each fitted for its own angle\n" + string.Join("\n", rows));
        }

        [UnityTest]
        public IEnumerator Z1_CaptureShapeStudy()
        {
            LogAssert.ignoreFailingMessages = true;
            var rows = new List<string>();

            foreach (var lvl in Levels)
            foreach (var (w, h, label) in new[] { (1080, 1920, "portrait"), (1920, 1080, "landscape") })
            {
                yield return SceneFixture.Load(FixtureMode.Playing, lvl);
                var grid = SceneFixture.Grid;
                var boat = SceneFixture.Boat;
                if (boat != null) boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(1.4f);

                string name = lvl.Replace("Levels/", "");
                string shot;
                float fill; Vector2 tilePx; float pathPx;

                using (var ctx = new DeterministicContext(
                    projection: BoardFraming.Projection.OrthographicTilted, width: w, height: h))
                {
                    ctx.Quiesce();
                    ctx.FrameBoard();

                    fill = BoardFraming.FillFraction(ctx.FramedBounds, ctx.Cam);

                    // A middle tile, so the measurement is typical rather than an edge case.
                    var mid = grid.GetTileAt(grid.cols / 2, grid.rows / 2);
                    var rend = mid != null ? mid.GetComponentInChildren<MeshRenderer>() : null;
                    var r = rend != null ? BoardFraming.ViewportRectOf(rend.bounds, ctx.Cam) : new Rect();
                    tilePx = new Vector2(r.width * ctx.RtWidth, r.height * ctx.RtHeight);

                    // Path line width in PIXELS. Derived from the LineRenderer's world width and
                    // the orthographic scale, not sampled - sampling would measure antialiasing.
                    // Ortho: pixels per world unit = targetHeight / (2 * orthographicSize).
                    float pxPerUnit = ctx.RtHeight / (2f * ctx.Cam.orthographicSize);
                    pathPx = -1f;   // measured from the image below, not from the LineRenderer

                    shot = CaptureRig.Capture(ctx, "shape-study", $"{name}_{label}");
                }

                // PATH WIDTH, MEASURED FROM THE RENDER. LineRenderer.widthMultiplier reports 1.0
                // world unit at unit scale, which would be half a tile wide - the lines are
                // visibly a few pixels, so that property is not what governs the drawn width
                // here. Measuring the run length of path-coloured pixels across scanlines is the
                // honest alternative: it measures what was actually drawn.
                pathPx = MedianPathRunPx(PixelUtil.Load(shot));

                rows.Add($"  {name,-11} {label,-9} fill={fill,7:P2}  " +
                         $"tile={tilePx.x,6:F1}x{tilePx.y,5:F1}px  " +
                         $"path={pathPx,5:F1}px  " +
                         $"tap={(Mathf.Min(tilePx.x, tilePx.y) >= TouchTargetFloorPx ? "OK " : "SMALL")}");

                Assert.IsTrue(CaptureRig.LooksRendered(shot), $"{name}/{label}: capture is black");
                yield return new WaitForSecondsRealtime(0.2f);
            }

            Debug.Log($"[Z1] board shape study, orthographic tilted " +
                      $"(touch-target floor {TouchTargetFloorPx}px)\n" + string.Join("\n", rows));
        }
    }
}
