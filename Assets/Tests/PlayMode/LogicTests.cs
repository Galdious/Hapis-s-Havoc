using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// Logic assertions. No pixels - these run against live scene state.
    ///
    /// L1 and L4 are EXPECTED TO FAIL against the current codebase. They encode risks R1 and R3
    /// from ARCHITECTURE.md. Having them red is how we will prove the eventual fix actually
    /// landed rather than taking a diff's word for it.
    /// </summary>
    [TestFixture]
    public class LogicTests
    {
        // ---------------------------------------------------------------- L4

        /// <summary>
        /// L4. EXPECTED TO FAIL - risk R3.
        ///
        /// GridManager.InitializeTile writes SIX connections for a reversed tile
        /// (0-2, 2-0, 1-3, 3-1, 4-5, 5-4) - three logical paths written twice. PathVisualizer
        /// creates one LineRenderer GameObject per connection but registers them in a dictionary
        /// keyed by canonical (min,max) pair, so only three land in the dictionary. CleanUpPaths
        /// destroys dictionary values only, so three GameObjects per reversed init are orphaned
        /// and can never be destroyed.
        /// </summary>
        [UnityTest]
        public IEnumerator L4_NoLineRendererLeakWhenFlippingATile()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            var tile = grid.GetTileAt(0, 0);
            Assert.IsNotNull(tile, "no tile at (0,0)");
            var template = tile.originalTemplate;
            Assert.IsNotNull(template, "tile has no originalTemplate");

            int Count() => tile.GetComponentsInChildren<LineRenderer>(true).Length;

            // Settle to the blue side first so the baseline is a known state.
            grid.InitializeTile(tile, template, false);
            yield return new WaitForSecondsRealtime(0.1f);   // Destroy() is deferred to end of frame
            int baseline = Count();

            var trail = new List<int> { baseline };
            for (int i = 0; i < 20; i++)
            {
                grid.InitializeTile(tile, template, true);    // reversed
                yield return new WaitForSecondsRealtime(0.05f);
                grid.InitializeTile(tile, template, false);   // back to blue
                yield return new WaitForSecondsRealtime(0.05f);
                trail.Add(Count());
            }

            int final = Count();
            Debug.Log($"[L4] template={template.displayName} baseline={baseline} final={final} " +
                      $"growth={final - baseline}\n  trail: {string.Join(", ", trail)}");

            Assert.AreEqual(baseline, final,
                $"L4: LineRenderer count under the tile grew from {baseline} to {final} across 20 " +
                $"flip cycles (+{final - baseline}). Risk R3: GridManager.InitializeTile writes six " +
                "connections for a reversed tile where three are expected, and PathVisualizer keys " +
                "by canonical pair, so the duplicates are orphaned beyond CleanUpPaths' reach.");
        }

        // ---------------------------------------------------------------- L1

        /// <summary>
        /// L1. EXPECTED TO FAIL - risk R1.
        ///
        /// Rotation is compared through TileOrientation, never eulerAngles.y. Euler(180, theta, 0)
        /// normalises so a flipped tile authored at 0 reads back y=180 and one authored at 180
        /// reads back y=0 (CLAUDE.md gotcha 3). A naive comparison produces false failures on
        /// flipped tiles, and since L1 is already expected to fail on the real bug, the two would
        /// be indistinguishable.
        ///
        /// The real bug: overload 2 builds Quaternion.Euler(0, rotationY, isFlipped ? 180 : 0) -
        /// a Z-axis flip - where every other path flips on X. Rz(180) inverts local +X and
        /// Rx(180) does not, so TileOrientation.IsYawFlipped separates them.
        /// </summary>
        [UnityTest]
        public IEnumerator L1_PushParityAcrossTheThreeOverloads()
        {
            LogAssert.ignoreFailingMessages = true;

            const int row = 2;              // unlocked in 01_06 (lockedRows [3,1,0])
            const bool fromLeft = true;
            var results = new List<(string name, bool yaw, bool face, string detail)>();

            // ---- overload 2: (row, fromLeft, PuzzleHandTile) ----
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");
            var grid = SceneFixture.Grid;
            var type = SceneFixture.PlayableTileTypes().First(t => t.displayName == "TileCross");

            var hand2 = new PuzzleHandTile(type) { rotationY = 0f, isFlipped = true };
            yield return grid.PushRowCoroutine(row, fromLeft, hand2);
            yield return new WaitForSecondsRealtime(0.2f);
            var t2 = grid.GetTileAt(0, row);
            Assert.IsNotNull(t2, "overload 2 inserted no tile");
            results.Add(("overload2 (PuzzleHandTile)", TileOrientation.IsYawFlipped(t2),
                         TileOrientation.IsFaceFlipped(t2), TileOrientation.Describe(t2)));

            // ---- overload 3: (row, fromLeft, PuzzleHandTile, GameObject) ----
            // The caller supplies a pre-built tile; the drag path builds it the loader's way,
            // Euler(flip ? 180 : 0, rotationY, 0) - an X flip.
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");
            grid = SceneFixture.Grid;
            type = SceneFixture.PlayableTileTypes().First(t => t.displayName == "TileCross");

            var go = UnityEngine.Object.Instantiate(
                grid.tilePrefab,
                grid.GetSpawnPosition(row, fromLeft),
                Quaternion.Euler(180f, 0f, 0f),        // X flip, rotationY = 0
                grid.gridParent);
            var hand3 = new PuzzleHandTile(type) { rotationY = 0f, isFlipped = true };
            yield return grid.PushRowCoroutine(row, fromLeft, hand3, go);
            yield return new WaitForSecondsRealtime(0.2f);
            var t3 = grid.GetTileAt(0, row);
            Assert.IsNotNull(t3, "overload 3 inserted no tile");
            results.Add(("overload3 (pre-built GameObject)", TileOrientation.IsYawFlipped(t3),
                         TileOrientation.IsFaceFlipped(t3), TileOrientation.Describe(t3)));

            // ---- overload 1: (row, fromLeft, showObstacleSide) ----
            // Draws from the bag, so the TYPE and the yaw are random. Seeded for reproducibility.
            // Its flip axis is still directly comparable: it builds Euler(x = flip ? 180 : 0, ...).
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");
            grid = SceneFixture.Grid;
            UnityEngine.Random.InitState(DeterministicContext.DefaultSeed);
            yield return grid.PushRowCoroutine(row, fromLeft, true);   // showObstacleSide
            yield return new WaitForSecondsRealtime(0.2f);
            var t1 = grid.GetTileAt(0, row);
            Assert.IsNotNull(t1, "overload 1 inserted no tile");
            results.Add(("overload1 (bag draw)", TileOrientation.IsYawFlipped(t1),
                         TileOrientation.IsFaceFlipped(t1), TileOrientation.Describe(t1)));

            var report = string.Join("\n", results.Select(r =>
                $"  {r.name,-34} yaw180={r.yaw,-5} faceFlipped={r.face,-5}  {r.detail}"));
            Debug.Log("[L1] same logical input (flipped tile, rotationY=0) through each overload:\n" + report);

            // Every overload was asked for a FLIPPED tile, so every one must show the reverse face.
            foreach (var r in results)
                Assert.IsTrue(r.face, $"L1: {r.name} did not show the reverse face for isFlipped=true. {r.detail}");

            // Overloads 2 and 3 got identical explicit input (rotationY=0, isFlipped=true), so
            // they must land in identical orientation. Overload 1's yaw is randomised by the bag
            // draw, so it is reported but not compared for yaw.
            Assert.AreEqual(results[1].yaw, results[0].yaw,
                $"L1: overload 2 and overload 3 were given identical input (rotationY=0, isFlipped=true) " +
                $"but landed in different orientations - overload2 yaw180={results[0].yaw}, " +
                $"overload3 yaw180={results[1].yaw}. Risk R1: overload 2 builds " +
                "Euler(0, rotationY, isFlipped ? 180 : 0), flipping on Z where every other path flips " +
                "on X. Rz(180) inverts local +X and Rx(180) does not, so a hand-pushed reversed tile " +
                "is mirrored relative to the same tile placed by the loader.");
        }

        // ---------------------------------------------------------------- L7

        /// <summary>
        /// L7. Guards the Job 1.6 fix: MoveToBankCoroutine never refreshed originalBoatPosition,
        /// so the next move animated from a stale origin - correct landing, wrong departure.
        ///
        /// originalBoatPosition is private, so this asserts on OBSERVABLE behaviour: sample the
        /// boat early in a move and require it to still be near where it actually started. With
        /// the bug, the first 0.1s pre-move lerp drags it toward the previous tile instead.
        /// </summary>
        [UnityTest]
        public IEnumerator L7_BoatAnimatesFromWhereItActuallyIs_AfterDockingAtABank()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var boat = SceneFixture.Boat;
            var grid = SceneFixture.Grid;
            Assert.IsNotNull(boat, "no boat");

            // Give the boat plenty of budget - docking and moving each cost a point.
            boat.maxMovementPoints = 20;
            boat.currentMovementPoints = 20;
            boat.UpdateMoveCounterUI();

            Vector3 tileBefore = boat.transform.position;

            // Dock at the bottom bank through the real public path.
            boat.SelectBoat();
            yield return new WaitForSecondsRealtime(0.8f);
            boat.OnBankClicked(RiverBankManager.BankSide.Bottom);
            yield return new WaitForSecondsRealtime(2.0f);

            Vector3 atBank = boat.transform.position;
            Debug.Log($"[L7] tileBefore={tileBefore} atBank={atBank} " +
                      $"movedAway={Vector3.Distance(tileBefore, atBank):F3}");
            Assert.Greater(Vector3.Distance(tileBefore, atBank), 0.5f,
                "L7 setup: the boat did not actually move to the bank, so the test proves nothing.");

            // Now start a move back onto a tile and sample it EARLY. If originalBoatPosition were
            // stale, the 0.1s pre-move lerp would haul the boat back toward tileBefore.
            boat.SelectBoat();
            yield return new WaitForSecondsRealtime(0.8f);

            var target = FindAnyValidDestination(boat, grid);
            if (target == null)
            {
                Assert.Inconclusive("L7: no valid destination tile was reachable from the bank in this " +
                                    "level, so the departure could not be sampled. See report.");
                yield break;
            }

            boat.OnTileClicked(target.Value.tile, MakePointerData(target.Value.tile));

            // Sample during the pre-move window (preMoveDuration is 0.1s).
            yield return new WaitForSecondsRealtime(0.06f);
            Vector3 earlySample = boat.transform.position;

            float driftFromBank = Vector3.Distance(earlySample, atBank);
            float driftToOldTile = Vector3.Distance(earlySample, tileBefore);
            Debug.Log($"[L7] earlySample={earlySample} distFromBank={driftFromBank:F3} " +
                      $"distFromOldTile={driftToOldTile:F3}");

            yield return new WaitForSecondsRealtime(2.0f);

            Assert.Less(driftFromBank, 1.0f,
                $"L7: {0.06f}s into the move the boat was {driftFromBank:F3} away from where it actually " +
                $"was (the bank) and {driftToOldTile:F3} from the tile it had left earlier. The animation " +
                "is departing from a stale cached origin rather than the transform - the Job 1.6 " +
                "MoveToBankCoroutine regression.");
        }

        static (TileInstance tile, int snap)? FindAnyValidDestination(BoatController boat, GridManager grid)
        {
            // The boat is at a bank, so entry is from the edge row. Walk that row for a
            // non-reversed, non-blocker tile - FindBankEntryMoves uses the same rule.
            for (int y = 0; y < grid.rows; y++)
            {
                for (int x = 0; x < grid.cols; x++)
                {
                    var t = grid.GetTileAt(x, y);
                    if (t == null || t.IsReversed || t.IsHardBlocker) continue;
                    return (t, 2);
                }
            }
            return null;
        }

        /// <summary>
        /// BoatController reads eventData.pressEventCamera, which is read-only and derived from
        /// pointerPressRaycast.module.eventCamera - so a raycaster has to be supplied rather than
        /// the camera assigned directly.
        /// </summary>
        static UnityEngine.EventSystems.PointerEventData MakePointerData(TileInstance tile)
        {
            var es = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var data = new UnityEngine.EventSystems.PointerEventData(es);

            var cam = Camera.main;
            if (cam == null) return data;

            var caster = cam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            if (caster == null) caster = cam.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();

            data.position = cam.WorldToScreenPoint(tile.transform.position);
            var hit = new UnityEngine.EventSystems.RaycastResult
            {
                gameObject = tile.gameObject,
                module = caster,
                screenPosition = data.position,
                worldPosition = tile.transform.position,
                distance = Vector3.Distance(cam.transform.position, tile.transform.position)
            };
            data.pointerCurrentRaycast = hit;
            data.pointerPressRaycast = hit;   // pressEventCamera reads through this
            return data;
        }
    }
}
