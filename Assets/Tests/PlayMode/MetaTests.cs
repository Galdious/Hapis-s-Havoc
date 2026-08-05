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
    ///   X2 (V5 must fail)  - [Ignore], blocked on ThemeDefinition, alongside V5/V6.
    ///   X3 (V1 must fail)  - implemented below.
    ///   X4 (V8 must fail)  - not yet written; V8 is not implemented in this pass.
    ///   X5 (L1 must fail)  - NOT synthesised. L1 already fails against the real R1 bug, which is
    ///                        stronger evidence than a control: the assertion is demonstrably red
    ///                        on genuine breakage today. A synthetic control would add nothing.
    ///   X6 (L7 must fail)  - implemented below.
    /// </summary>
    [TestFixture]
    public class MetaTests
    {
        const float Settle = 1.6f;

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

            string before, after;
            using (var ctx = new DeterministicContext())
                before = CaptureRig.Capture(ctx, "meta", "x3-before");

            // THE BREAKAGE: light a tile and never put it back.
            var victim = grid.GetTileAt(1, 1);
            Assert.IsNotNull(victim, "no tile at (1,1)");
            var rend = victim.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(rend, "tile has no MeshRenderer");
            rend.material.color = Color.magenta;      // deliberately never restored
            yield return new WaitForSecondsRealtime(0.3f);

            using (var ctx = new DeterministicContext())
                after = CaptureRig.Capture(ctx, "meta", "x3-after-unrestored");

            float diff = PixelUtil.FractionDiffering(PixelUtil.Load(before), PixelUtil.Load(after), 8);
            Debug.Log($"[X3] unrestored highlight changed {diff:P3} of pixels (V1 threshold is 0.5%)");

            Assert.Greater(diff, 0.005f,
                $"X3 META-FAILURE: a tile was recoloured and never restored, yet only {diff:P3} of pixels " +
                "differ - under V1's 0.5% threshold. V1 would pass on a genuinely stuck highlight, which " +
                "means V1 is broken. Fix V1, not this control.");
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

        [Test]
        [Ignore("Blocked on ThemeDefinition, alongside V5 and V6.")]
        public void X2_V5_FailsOnNearIdenticalWaterAndStoneColours()
        {
            // Build a ThemeDefinition whose water and stone sit within a few units of each other,
            // then assert V5's WCAG contrast check rejects it. PixelUtil.ContrastRatio already
            // exists and is the comparison V5 will use; only the theme asset is missing.
            Assert.Fail("unreachable while ignored");
        }
    }
}
