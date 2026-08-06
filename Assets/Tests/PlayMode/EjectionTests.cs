using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// L9 and X9. The ejected-boat re-landing path decides WHICH SNAP POINT a boat lands on by
    /// comparing the ejected tile's rotation with the landing tile's. That is a gameplay
    /// outcome, and it was comparing raw eulerAngles.y - the readback trap of CLAUDE.md gotcha 3.
    /// </summary>
    [TestFixture]
    public class EjectionTests
    {
        /// <summary>
        /// L9. Build the exact case the raw comparison gets wrong: two tiles with the SAME
        /// authored yaw (0), one flipped and one not. Their eulerAngles.y read back 180 and 0,
        /// so a raw comparison sees a 180 degree difference that does not exist and mirrors the
        /// boat's snap point through GetOppositeSnapPoint. The boat lands on the wrong edge.
        ///
        /// The ejecting tile is the flipped one, not the landing tile - a flipped tile IS
        /// reversed, and the landing search skips over reversed tiles entirely, so a flipped
        /// destination would never be selected as the landing tile in the first place.
        /// </summary>
        [UnityTest]
        public IEnumerator L9_EjectedBoatLandsCorrectlyAcrossFlippedTiles()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            var boat = SceneFixture.Boat;
            Assert.IsNotNull(grid, "no GridManager");
            Assert.IsNotNull(boat, "no boat");

            const int row = 2;
            const bool fromLeft = true;
            int exitCol = grid.cols - 1;          // fromLeft ejects the far-right tile
            const int rideSnap = 2;               // bottom-left; its mirror is 1

            var ejecting = grid.GetTileAt(exitCol, row);
            var landing  = grid.GetTileAt(exitCol, row - 1);
            Assert.IsNotNull(ejecting, $"no tile at ({exitCol},{row})");
            Assert.IsNotNull(landing,  $"no tile at ({exitCol},{row - 1})");

            // Ejecting tile: authored rotationY = 0 AND flipped -> eulerAngles.y reads back 180.
            ejecting.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            grid.InitializeTile(ejecting, ejecting.originalTemplate, true);
            grid.UpdateTileGameplayVisuals(ejecting);

            // Landing tile: authored rotationY = 0, NOT flipped -> reads back 0, and stays
            // un-reversed so the landing search actually stops on it.
            landing.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            grid.InitializeTile(landing, landing.originalTemplate, false);
            grid.UpdateTileGameplayVisuals(landing);
            yield return new WaitForSecondsRealtime(0.3f);

            // starsCollected decides the search direction; 0 sends the boat to row - 1, which
            // is the tile this test prepared. It is read-only, so assert rather than set it.
            Assert.AreEqual(0, boat.starsCollected,
                "L9 setup: boat has collected stars, so the ejection search would run toward " +
                "row + 1 instead of the tile this test prepared at row - 1.");
            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(1.0f);
            boat.PlaceOnTile(ejecting, rideSnap);
            yield return new WaitForSecondsRealtime(0.4f);

            Debug.Log($"[L9] ejecting tile: {TileOrientation.Describe(ejecting)}\n" +
                      $"     landing  tile: {TileOrientation.Describe(landing)}\n" +
                      $"     raw eulerAngles.y: ejecting={ejecting.transform.eulerAngles.y:F1} " +
                      $"landing={landing.transform.eulerAngles.y:F1} -> a raw comparison sees " +
                      $"{Mathf.Abs(ejecting.transform.eulerAngles.y - landing.transform.eulerAngles.y):F1} degrees\n" +
                      $"     true yaw: ejecting={TileOrientation.IsYawFlipped(ejecting)} " +
                      $"landing={TileOrientation.IsYawFlipped(landing)} -> IDENTICAL, so the snap " +
                      $"point must NOT be mirrored\n" +
                      $"     boat placed on snap {rideSnap}; correct landing snap is {rideSnap}, " +
                      $"the mirrored (wrong) answer is 1");

            var type = SceneFixture.PlayableTileTypes()[0];
            yield return grid.PushRowCoroutine(row, fromLeft,
                new PuzzleHandTile(type) { rotationY = 0f, isFlipped = false });
            yield return new WaitForSecondsRealtime(1.2f);

            var landedTile = boat.GetCurrentTile();
            int landedSnap = boat.GetCurrentSnapPoint();
            var lc = landedTile != null ? grid.GetTileCoordinates(landedTile) : (x: -1, y: -1);

            Debug.Log($"[L9] boat landed on grid({lc.x},{lc.y}) snap {landedSnap} " +
                      $"(expected the landing tile, snap {rideSnap})");

            Assert.AreSame(landing, landedTile,
                $"L9: the ejected boat did not land on the expected tile. Landed on " +
                $"grid({lc.x},{lc.y}), expected grid({exitCol},{row - 1}).");

            Assert.AreEqual(rideSnap, landedSnap,
                $"L9: the ejected boat landed on snap {landedSnap} but should have landed on " +
                $"{rideSnap}. Both tiles are authored at rotationY = 0, so their yaw is IDENTICAL " +
                $"and the snap point must be carried across unchanged. Their eulerAngles.y read " +
                $"back 180 and 0 because one is flipped (CLAUDE.md gotcha 3), so a raw comparison " +
                $"believes they differ by 180 degrees and mirrors the snap point through " +
                $"GetOppositeSnapPoint. Compare orientation with TileOrientation, not eulerAngles.");
        }

        /// <summary>
        /// X9 -> L9 must fail. A synthetic control, deliberately not the live bug: once the
        /// landing path is fixed, a control built on it would stop failing and X9 would silently
        /// stop proving anything (the X5 lesson). This compares the two rules directly on the
        /// input L9 uses, and requires them to disagree - if they agreed, L9 could not tell a
        /// raw-eulerAngles landing path from a correct one.
        /// </summary>
        [UnityTest]
        public IEnumerator X9_L9_CatchesALandingPathThatComparesRawEulerAngles()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            Assert.IsNotNull(grid.tilePrefab, "no tile prefab");

            // Same authored yaw (0); one flipped, one not.
            var flipped = Object.Instantiate(grid.tilePrefab, new Vector3(-50f, 0f, 0f),
                                             Quaternion.Euler(180f, 0f, 0f));
            var plain   = Object.Instantiate(grid.tilePrefab, new Vector3(-60f, 0f, 0f),
                                             Quaternion.Euler(0f, 0f, 0f));
            // A genuine yaw difference, to prove the correct rule is not trivially always-equal.
            var yawed   = Object.Instantiate(grid.tilePrefab, new Vector3(-70f, 0f, 0f),
                                             Quaternion.Euler(0f, 180f, 0f));
            yield return null;

            // THE BREAKAGE: the rule the landing path used to apply.
            bool rawSaysDiffers = Mathf.Abs(flipped.transform.eulerAngles.y
                                          - plain.transform.eulerAngles.y) > 1f;
            // The rule it applies now.
            bool trueDiffers = TileOrientation.IsYawFlipped(flipped.transform)
                            != TileOrientation.IsYawFlipped(plain.transform);
            bool trueDetectsRealYaw = TileOrientation.IsYawFlipped(yawed.transform)
                                   != TileOrientation.IsYawFlipped(plain.transform);

            Debug.Log($"[X9] same authored yaw, one flipped:\n" +
                      $"     raw eulerAngles.y rule says they differ? {rawSaysDiffers} " +
                      $"({flipped.transform.eulerAngles.y:F1} vs {plain.transform.eulerAngles.y:F1})\n" +
                      $"     TileOrientation says they differ?        {trueDiffers}\n" +
                      $"     TileOrientation detects a REAL 180 yaw?  {trueDetectsRealYaw}");

            Object.DestroyImmediate(flipped);
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(yawed);

            Assert.IsTrue(trueDetectsRealYaw,
                "X9 META-FAILURE: TileOrientation did not detect a genuine 180 degree yaw " +
                "difference. The replacement rule is trivially always-equal, so L9 would pass " +
                "on anything. Fix the rule, not this control.");

            Assert.IsFalse(trueDiffers,
                "X9 META-FAILURE: TileOrientation reported two tiles authored at the same yaw as " +
                "differing. The replacement rule is wrong.");

            Assert.IsTrue(rawSaysDiffers,
                "X9 META-FAILURE: the raw eulerAngles.y comparison did NOT misreport these two " +
                "tiles as differing, so L9's scenario no longer distinguishes a raw-eulerAngles " +
                "landing path from a correct one and L9 proves nothing. Rebuild the control.");
        }
    }
}
