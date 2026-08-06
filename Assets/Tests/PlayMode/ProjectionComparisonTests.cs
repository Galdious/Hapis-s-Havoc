using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// STEP 4. Measures the legibility cost of perspective rather than asserting a preference.
    ///
    /// THE NUMBER THAT MATTERS is the near/far tile size ratio. Under perspective a tile in the
    /// far row is genuinely smaller on screen than an identical tile in the near row, so the
    /// player reads the two rows at different sizes and the far row is where mistakes happen.
    /// Orthographic has no foreshortening, so that ratio is 1.000 by construction - the question
    /// is only how large the cost is under perspective, and whether ortho gives anything up.
    ///
    /// Not an assertion with a threshold: this is a measurement that informs a design decision.
    /// The one thing it DOES assert is that the measurement is not vacuous.
    /// </summary>
    [TestFixture]
    public class ProjectionComparisonTests
    {
        [UnityTest]
        public IEnumerator P1_CompareProjections()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            var boat = SceneFixture.Boat;
            if (boat != null) boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(1.6f);

            var rows = new List<string>();
            var ratios = new Dictionary<BoardFraming.Projection, float>();

            foreach (BoardFraming.Projection proj in new[]
            {
                BoardFraming.Projection.PerspectiveTilted,
                BoardFraming.Projection.OrthographicTilted,
                BoardFraming.Projection.OrthographicShallow,
            })
            {
                float nearArea = 0f, farArea = 0f, fill = 0f;
                string shot;

                using (var ctx = new DeterministicContext(projection: proj))
                {
                    ctx.Quiesce();
                    var cam = ctx.Cam;

                    // Near row is row 0: rows increase with +Z and the camera sits at -Z.
                    nearArea = MeanTileViewportArea(grid, 0, cam);
                    farArea = MeanTileViewportArea(grid, grid.rows - 1, cam);
                    fill = BoardFraming.FillFraction(ctx.FramedBounds, cam);

                    shot = CaptureRig.Capture(ctx, "projection", proj.ToString());
                }

                float ratio = farArea > 0f ? nearArea / farArea : float.NaN;
                ratios[proj] = ratio;

                rows.Add($"  {proj,-22} near/far size = {ratio:F3}   " +
                         $"board fill = {fill:P2}   " +
                         $"(near {nearArea:E2}, far {farArea:E2})");

                Assert.IsTrue(CaptureRig.LooksRendered(shot), $"{proj}: capture is black");
                yield return new WaitForSecondsRealtime(0.2f);
            }

            Debug.Log("[P1] projection comparison on 01_06 (3x4 board), portrait 1080x1920:\n" +
                      string.Join("\n", rows) +
                      "\n  A ratio of 1.000 means every tile reads at the same size. Anything " +
                      "above 1 is the far row shrinking - perspective's legibility cost.");

            // Guard against a vacuous measurement: if the near and far rows measured the same
            // under PERSPECTIVE, the projection is not actually being applied.
            Assert.Greater(ratios[BoardFraming.Projection.PerspectiveTilted], 1.001f,
                "P1: perspective reported no near/far size difference at all, so the camera is " +
                "not actually perspective and this comparison measures nothing.");

            Assert.AreEqual(1f, ratios[BoardFraming.Projection.OrthographicTilted], 0.02f,
                "P1: orthographic reported foreshortening, which is impossible - the projection " +
                "is not actually orthographic and the comparison is invalid.");
        }

        /// <summary>
        /// Mean projected viewport AREA of the tiles in one grid row. Area rather than width,
        /// because perspective shrinks both axes and area is what the eye actually reads.
        /// </summary>
        static float MeanTileViewportArea(GridManager grid, int row, Camera cam)
        {
            float total = 0f;
            int n = 0;
            for (int x = 0; x < grid.cols; x++)
            {
                var tile = grid.GetTileAt(x, row);
                if (tile == null) continue;
                var rend = tile.GetComponentInChildren<MeshRenderer>();
                if (rend == null) continue;
                var r = BoardFraming.ViewportRectOf(rend.bounds, cam);
                total += r.width * r.height;
                n++;
            }
            return n > 0 ? total / n : 0f;
        }
    }
}
