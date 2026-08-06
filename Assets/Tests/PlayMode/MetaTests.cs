using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// STEP D. An assertion that cannot go red is decoration.
    ///
    /// Each meta-test deliberately breaks the thing a real assertion watches, then asserts the
    /// SAME comparison the real test performs now FAILS. If a meta-test does not produce the
    /// expected failure, the assertion is broken - fix the assertion, not the control.
    ///
    /// Status of the six controls in the brief:
    ///   X1 (V3 must fail)  - not yet written; V3 is not implemented in this pass.
    ///   X2 (V5 must fail)  - now real, and lives in ThemeTests beside V5 itself.
    ///   X3 (V1 must fail)  - implemented below.
    ///   X4 (V8 must fail)  - not yet written; V8 is not implemented in this pass.
    ///   X5 (L1 must fail)  - implemented below, against a SYNTHETIC control rather than the live
    ///                        R1 bug, so it keeps proving L1 can fail after R1 is fixed.
    ///   X6 (L7 must fail)  - implemented below.
    ///   X7 (V1 must fail)  - implemented below. Replaces X3's relevance now that R2 is fixed:
    ///                        X3 breaks the OLD mechanism, X7 breaks the NEW one.
    /// </summary>
    [TestFixture]
    public class MetaTests
    {
        const float Settle = 1.6f;

        /// <summary>
        /// Minimum fraction of THE SUBJECT'S OWN projected pixels that must change. Recolouring a
        /// tile changes nearly all of them; 25% leaves generous room for the parts of a tile's
        /// bounding rect that are background, path lines or a neighbouring tile, while still
        /// being far above anything a no-op could produce. Deliberately NOT frame-relative -
        /// that made these controls break on every reframe.
        /// </summary>
        const float MinSubjectChange = 0.25f;

        /// <summary>
        /// X3 -> V1 must fail. Mutates a tile's material directly and never restores it, which
        /// is precisely the risk-R2 pattern (renderer.material.color = X, restored via
        /// sharedMaterial, orphaning the instance). V1's comparison must catch it.
        /// </summary>
        [UnityTest]
        public IEnumerator X3_V1_FailsWhenAHighlightNeverRestores()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var boat = SceneFixture.Boat;
            var grid = SceneFixture.Grid;
            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(Settle);

            // Capture, recolour, capture again - and measure the change WITHIN THE VICTIM TILE'S
            // OWN projected rect, as a fraction of that rect. Measuring the whole frame made this
            // a test of how large the tile happened to appear, so it broke on every reframe.
            var victim = grid.GetTileAt(1, 1);
            Assert.IsNotNull(victim, "no tile at (1,1)");
            var rend = victim.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(rend, "tile has no MeshRenderer");

            string before, after;
            Rect tileRect;
            using (var ctx = new DeterministicContext())
            {
                before = CaptureRig.Capture(ctx, "meta", "x3-before");
                tileRect = BoardFraming.ViewportRectOf(rend.bounds, ctx.Cam);
            }

            rend.material.color = Color.magenta;      // THE BREAKAGE, deliberately never restored
            yield return new WaitForSecondsRealtime(0.3f);

            using (var ctx = new DeterministicContext())
                after = CaptureRig.Capture(ctx, "meta", "x3-after-unrestored");

            float diff = PixelUtil.FractionDifferingInRect(
                PixelUtil.Load(before), PixelUtil.Load(after), 8, tileRect, out int considered);

            float frameDiff = PixelUtil.FractionDiffering(PixelUtil.Load(before), PixelUtil.Load(after), 8);
            Debug.Log($"[X3] unrestored highlight changed {diff:P2} of the TILE'S OWN " +
                      $"{considered} projected pixels (threshold {MinSubjectChange:P0}); " +
                      $"tile rect {tileRect}; whole-frame diff {frameDiff:P3}; " +
                      $"renderer '{rend.name}' enabled={rend.enabled} " +
                      $"active={rend.gameObject.activeInHierarchy} mat='{rend.sharedMaterial?.name}'");

            Assert.Greater(considered, 200,
                "X3: the victim tile projected to almost no pixels, so this measures nothing.");

            Assert.Greater(diff, MinSubjectChange,
                $"X3 META-FAILURE: a tile was recoloured and never restored, yet only {diff:P2} of " +
                "ITS OWN pixels differ. A tile that changes colour changes nearly all of them, so " +
                "either the highlight did not apply or the projection is wrong. Fix V1, not this control.");
        }

        /// <summary>
        /// X6 -> L7 must fail. L7 asserts the boat departs from where it actually is. This breaks
        /// exactly that observable property by displacing the boat at the moment L7 samples, and
        /// requires L7's comparison to notice.
        /// </summary>
        [UnityTest]
        public IEnumerator X6_L7_FailsWhenTheBoatDepartsFromTheWrongPlace()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var boat = SceneFixture.Boat;
            Assert.IsNotNull(boat, "no boat");

            Vector3 oldTile = boat.transform.position;

            boat.maxMovementPoints = 20;
            boat.currentMovementPoints = 20;
            boat.SelectBoat();
            yield return new WaitForSecondsRealtime(0.8f);
            boat.OnBankClicked(RiverBankManager.BankSide.Bottom);
            yield return new WaitForSecondsRealtime(2.0f);

            Vector3 atBank = boat.transform.position;
            Assert.Greater(Vector3.Distance(oldTile, atBank), 0.5f, "X6 setup: boat never reached the bank");

            // THE BREAKAGE: displace the boat back to the tile it left, which is exactly what a
            // stale originalBoatPosition does during the pre-move lerp.
            boat.StopAllCoroutines();
            boat.transform.position = oldTile;
            Vector3 earlySample = boat.transform.position;

            float distFromBank = Vector3.Distance(earlySample, atBank);
            Debug.Log($"[X6] displaced sample: distFromBank={distFromBank:F3} " +
                      $"(L7 requires < 1.0 to pass)");

            Assert.GreaterOrEqual(distFromBank, 1.0f,
                $"X6 META-FAILURE: the boat was displaced to the tile it had left, but the sampled " +
                $"distance from its true position is only {distFromBank:F3} - under L7's 1.0 threshold. " +
                "L7 would pass on a wrong departure, which means L7 is broken. Fix L7, not this control.");
        }


        /// <summary>
        /// X5 -> L1 must fail. Built against a SYNTHETIC control, deliberately NOT against the
        /// live overload-2 bug: the next job fixes R1, and the moment it does an R1-based control
        /// would stop failing and X5 would silently stop proving anything. This exercises the
        /// exact comparison L1 uses - TileOrientation.SameOrientation - against two tiles that
        /// differ only in yaw, so it stays red no matter what happens to the push overloads.
        /// </summary>
        [UnityTest]
        public IEnumerator X5_L1_ComparisonCatchesAWrongRotation()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            Assert.IsNotNull(grid, "no GridManager");
            Assert.IsNotNull(grid.tilePrefab, "no tile prefab");

            // Two tiles built the loader's way (X flip), identical except one is yawed 180.
            var reference = Object.Instantiate(grid.tilePrefab, new Vector3(-50f, 0f, 0f),
                                               Quaternion.Euler(180f, 0f, 0f));
            var wrongRotation = Object.Instantiate(grid.tilePrefab, new Vector3(-60f, 0f, 0f),
                                                   Quaternion.Euler(180f, 180f, 0f));   // THE BREAKAGE
            var sameAsReference = Object.Instantiate(grid.tilePrefab, new Vector3(-70f, 0f, 0f),
                                                     Quaternion.Euler(180f, 0f, 0f));
            yield return null;

            bool matchesIdentical = TileOrientation.SameOrientation(reference.transform,
                                                                    sameAsReference.transform);
            bool matchesWrong = TileOrientation.SameOrientation(reference.transform,
                                                                wrongRotation.transform);

            Debug.Log($"[X5] reference={TileOrientation.Describe(reference.transform)}\n" +
                      $"     wrongRotation={TileOrientation.Describe(wrongRotation.transform)}\n" +
                      $"     identical-pair reported same? {matchesIdentical}   " +
                      $"wrong-pair reported same? {matchesWrong}");

            Object.DestroyImmediate(reference);
            Object.DestroyImmediate(wrongRotation);
            Object.DestroyImmediate(sameAsReference);

            // Guard against a comparison that is trivially always-false, which would "catch"
            // everything and prove nothing.
            Assert.IsTrue(matchesIdentical,
                "X5 META-FAILURE: two identically-built tiles were reported as DIFFERENT. L1's " +
                "comparison is trivially always-false, so its failures carry no information.");

            Assert.IsFalse(matchesWrong,
                "X5 META-FAILURE: a tile yawed 180 relative to the reference was reported as the " +
                "SAME orientation. L1's rotation comparison cannot detect a wrong rotation, so L1 " +
                "would pass on a genuine parity break. Fix L1, not this control.");
        }


        /// <summary>
        /// X7 -> V1 must fail. R2 is now fixed, so X3's "mutate renderer.material directly"
        /// control no longer represents how highlighting works. This is the equivalent control
        /// for the NEW mechanism: apply a HighlightService tint and never Clear it - exactly
        /// what a Clear() that silently did nothing would leave behind. V1 must still catch it.
        /// Same reasoning as X5: the assertion needs a control that survives the fix.
        /// </summary>
        [UnityTest]
        public IEnumerator X7_V1_FailsWhenHighlightServiceClearDoesNothing()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var boat = SceneFixture.Boat;
            var grid = SceneFixture.Grid;
            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(Settle);

            var victim = grid.GetTileAt(1, 1);
            Assert.IsNotNull(victim, "no tile at (1,1)");
            var rend = victim.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(rend, "tile has no MeshRenderer");

            string before, after;
            Rect tileRect;
            using (var ctx = new DeterministicContext())
            {
                before = CaptureRig.Capture(ctx, "meta", "x7-before");
                tileRect = BoardFraming.ViewportRectOf(rend.bounds, ctx.Cam);
            }

            HighlightService.Apply(rend, Color.magenta);   // THE BREAKAGE: tint, never cleared
            yield return new WaitForSecondsRealtime(0.3f);

            using (var ctx = new DeterministicContext())
                after = CaptureRig.Capture(ctx, "meta", "x7-after-uncleared");

            float diff = PixelUtil.FractionDifferingInRect(
                PixelUtil.Load(before), PixelUtil.Load(after), 8, tileRect, out int considered);

            float frameDiff = PixelUtil.FractionDiffering(PixelUtil.Load(before), PixelUtil.Load(after), 8);
            Debug.Log($"[X7] uncleared HighlightService tint changed {diff:P2} of the TILE'S OWN " +
                      $"{considered} projected pixels (threshold {MinSubjectChange:P0}); " +
                      $"whole-frame diff {frameDiff:P3}; renderer '{rend.name}' " +
                      $"enabled={rend.enabled} active={rend.gameObject.activeInHierarchy}");

            Assert.Greater(considered, 200,
                "X7: the victim tile projected to almost no pixels, so this measures nothing.");

            HighlightService.Clear(rend);   // tidy up so later tests are unaffected

            Assert.Greater(diff, MinSubjectChange,
                $"X7 META-FAILURE: a HighlightService tint was applied and never cleared, yet only " +
                $"{diff:P2} of the TILE'S OWN pixels differ. V1 would pass on a Clear() that " +
                "silently does nothing. Fix V1, not this control.");
        }

    }
}
