using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// TEMPORARY diagnostic (not an assertion). Measures how much of R1 is actually VISIBLE.
    ///
    /// R1's buggy quaternion equals the correct one post-multiplied by Ry(180) - an extra local
    /// yaw, not a mirror. So this pushes a flipped tile the buggy way, captures, then applies
    /// exactly that missing yaw to the pushed tile and captures again. The diff is R1's entire
    /// visual signature on this board.
    /// </summary>
    [TestFixture]
    public class R1VisualDiagnostic
    {
        [UnityTest]
        public IEnumerator R1_HowVisibleIsIt()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            var boat = SceneFixture.Boat;

            var type = SceneFixture.PlayableTileTypes().First(t => t.displayName == "TileCross");
            for (int i = 0; i < 3; i++)
                yield return grid.PushRowCoroutine(2, true,
                    new PuzzleHandTile(type) { rotationY = 0f, isFlipped = true });

            if (boat != null) boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(1.6f);

            var tiles = new[] { grid.GetTileAt(0, 2), grid.GetTileAt(1, 2), grid.GetTileAt(2, 2) };
            for (int i = 0; i < 3; i++)
                Debug.Log($"[R1D] col {i}: {(tiles[i] == null ? "NULL" : TileOrientation.Describe(tiles[i].transform))}");
            var tile = tiles[0];
            Assert.IsNotNull(tile);

            string before, after;
            using (var ctx = new DeterministicContext())
                before = CaptureRig.Capture(ctx, "meta", "r1-asPushed");

            var pre = tile.snapPoints.Select(s => s != null ? s.position : Vector3.zero).ToArray();
            Debug.Log($"[R1D] as pushed : {TileOrientation.Describe(tile.transform)}");

            // Apply exactly the correction the R1 fix will produce, to every pushed tile.
            foreach (var t in tiles) if (t != null) t.transform.Rotate(0f, 180f, 0f, Space.Self);
            yield return new WaitForSecondsRealtime(0.6f);

            var post = tile.snapPoints.Select(s => s != null ? s.position : Vector3.zero).ToArray();
            Debug.Log($"[R1D] corrected : {TileOrientation.Describe(tile.transform)}");

            using (var ctx = new DeterministicContext())
                after = CaptureRig.Capture(ctx, "meta", "r1-corrected");

            for (int i = 0; i < 6; i++)
                Debug.Log($"[R1D] snap {i}: {pre[i]:F3} -> {post[i]:F3}  moved={Vector3.Distance(pre[i], post[i]):F3}");

            float frac = PixelUtil.FractionDiffering(PixelUtil.Load(before), PixelUtil.Load(after), 4);
            PixelUtil.MeanMaxDelta(PixelUtil.Load(before), PixelUtil.Load(after), out float mean, out int max);
            Debug.Log($"[R1D] R1 VISUAL SIGNATURE: differing={frac:P4} mean={mean:F3} max={max} " +
                      $"(V9 tolerance is {GoldenImageTests.AllowedDifferingFraction:P2})");
        }
    }
}
