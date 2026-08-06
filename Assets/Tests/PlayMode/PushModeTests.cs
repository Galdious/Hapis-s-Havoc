using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// M1 and X8, added alongside the PushRowCoroutine consolidation.
    ///
    /// L1 proves the three overloads agree, but runs all three in Playing mode. M1 is the
    /// per-mode counterpart: the same push in Editor, Playing and Endless, which is what
    /// CLAUDE.md's "every change must work in all three operating modes" actually asks for.
    /// </summary>
    [TestFixture]
    public class PushModeTests
    {
        /// <summary>
        /// M1. Push a FLIPPED tile authored at rotationY = 0 in each of the three operating
        /// modes and require it to land showing the reverse face with NO extra yaw. That is
        /// exactly the R1 signature, checked through TileOrientation's basis vectors rather
        /// than eulerAngles (CLAUDE.md gotcha 3).
        /// </summary>
        [UnityTest]
        public IEnumerator M1_FlippedPushLandsAuthoredInAllThreeModes()
        {
            LogAssert.ignoreFailingMessages = true;

            var rows = new List<string>();
            var failures = new List<string>();

            foreach (var mode in new[] { FixtureMode.Editor, FixtureMode.Playing, FixtureMode.Endless })
            {
                yield return SceneFixture.Load(mode, mode == FixtureMode.Endless
                    ? null : "Levels/01_06_TestLevel");

                var grid = SceneFixture.Grid;
                if (grid == null) { failures.Add($"{mode}: no GridManager"); continue; }

                // Find a row that actually holds tiles, rather than assuming the puzzle layout.
                int row = -1;
                for (int y = 0; y < grid.rows && row < 0; y++)
                    if (grid.GetTileAt(0, y) != null && grid.GetTileAt(grid.cols - 1, y) != null)
                        row = y;

                if (row < 0) { failures.Add($"{mode}: no fully populated row to push"); continue; }

                var type = SceneFixture.PlayableTileTypes().FirstOrDefault(t => t.displayName == "TileCross")
                           ?? SceneFixture.PlayableTileTypes().FirstOrDefault();
                if (type == null) { failures.Add($"{mode}: no tile types available"); continue; }

                yield return grid.PushRowCoroutine(row, true,
                    new PuzzleHandTile(type) { rotationY = 0f, isFlipped = true });
                yield return new WaitForSecondsRealtime(0.3f);

                var pushed = grid.GetTileAt(0, row);
                if (pushed == null) { failures.Add($"{mode}: push inserted no tile into row {row}"); continue; }

                bool yaw  = TileOrientation.IsYawFlipped(pushed);
                bool face = TileOrientation.IsFaceFlipped(pushed);
                rows.Add($"  {mode,-8} row {row}  yaw180={yaw,-5} faceFlipped={face,-5}  {TileOrientation.Describe(pushed)}");

                if (!face) failures.Add($"{mode}: isFlipped=true did not produce the reverse face");
                if (yaw)   failures.Add($"{mode}: tile authored rotationY=0 came back yawed 180 - this is R1");
            }

            Debug.Log("[M1] flipped push, rotationY=0, per operating mode:\n" + string.Join("\n", rows));

            Assert.IsEmpty(failures, "M1 per-mode failures:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// X8 -> L2 must fail. L2 went green by a change to the reverse lookup
        /// (FindTileAndSnapPointAtWorldPos now compares in the horizontal plane), so L2 could
        /// now be passing because the lookup stopped discriminating at all rather than because
        /// the boat is where it says it is. This displaces the boat by more than the threshold
        /// IN THE PLANE and requires the resync to still notice.
        /// </summary>
        [UnityTest]
        public IEnumerator X8_L2_StillCatchesAGenuinelyDisplacedBoat()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            var boat = SceneFixture.Boat;
            const int row = 2;

            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(1.2f);
            boat.PlaceOnTile(grid.GetTileAt(1, row), 2);
            yield return new WaitForSecondsRealtime(0.4f);

            // THE BREAKAGE: shove the boat a whole tile sideways without telling it. Its state
            // still claims the old tile, so the resync must report a desync.
            boat.transform.position += new Vector3(2f, 0f, 0f);
            yield return new WaitForSecondsRealtime(0.2f);

            bool desyncLogged = false;
            Application.LogCallback onLog = (cond, stack, t) =>
            {
                if (t == LogType.Warning && cond != null && cond.Contains("Boat Desync Detected"))
                    desyncLogged = true;
            };
            Application.logMessageReceived += onLog;
            boat.SelectBoat();
            yield return new WaitForSecondsRealtime(0.8f);
            Application.logMessageReceived -= onLog;

            Debug.Log($"[X8] boat displaced 2 units in X; desync detected = {desyncLogged}");

            Assert.IsTrue(desyncLogged,
                "X8 META-FAILURE: the boat was moved a full tile sideways with no state update, " +
                "yet ResynchronizeStateWithTransform did not report a desync. The reverse lookup " +
                "has stopped discriminating, so L2 passes vacuously. Fix the lookup, not this control.");
        }
    }
}
