using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// V1 / V2. The highest-value pair in the suite.
    ///
    /// V1 is an automated tripwire for risk R2 - the "highlight stuck on" family that has
    /// recurred through this project's entire history (banks staying cyan; BoatController
    /// carries a v03 changelog header about exactly this). Every highlight path still does
    /// renderer.material.color = X and restores with renderer.sharedMaterial = original,
    /// which orphans the instantiated material.
    ///
    /// V2 guards the opposite failure: a highlight that silently does nothing, which would
    /// make V1 pass vacuously.
    ///
    /// Driven entirely through the real public path - SelectBoat / DeselectBoat - rather than
    /// poking private highlight methods, so it exercises what the game actually runs.
    /// </summary>
    [TestFixture]
    public class VisualHighlightTests
    {
        const string Suite = "highlight";

        /// <summary>Generous wall-clock settle. Selection stacks a 0.3s boat lift, a 0.2s
        /// tileLiftDelay, a 0.4s tile lift and a 0.2s path fade; never frame counts.</summary>
        const float Settle = 1.6f;

        [UnityTest]
        public IEnumerator V1_V2_HighlightAppearsAndClearsCompletely()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var boat = SceneFixture.Boat;
            Assert.IsNotNull(boat, "no boat");

            // SceneFixture finishes with FinalizeStateReconstruction, which SELECTS the boat.
            // Start from a known un-highlighted state or the baseline is already lit.
            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(Settle);

            string before, lit, after;
            int banksLitWhileSelected;

            using (var ctx = new DeterministicContext())
                before = CaptureRig.Capture(ctx, Suite, "01-before");

            boat.SelectBoat();
            yield return new WaitForSecondsRealtime(Settle);

            // A bank is highlighted by adding a BankClickHandler to it. Counting those tells us
            // whether the BANK path was actually exercised, rather than silently testing tiles only.
            banksLitWhileSelected = Object.FindObjectsByType<BankClickHandler>(FindObjectsSortMode.None).Length;

            using (var ctx = new DeterministicContext())
                lit = CaptureRig.Capture(ctx, Suite, "02-highlighted");

            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(Settle);

            using (var ctx = new DeterministicContext())
                after = CaptureRig.Capture(ctx, Suite, "03-cleared");

            var iBefore = PixelUtil.Load(before);
            var iLit = PixelUtil.Load(lit);
            var iAfter = PixelUtil.Load(after);

            float litDiff = PixelUtil.FractionDiffering(iBefore, iLit, 8);
            float clearedDiff = PixelUtil.FractionDiffering(iBefore, iAfter, 8);
            PixelUtil.MeanMaxDelta(iBefore, iAfter, out float mean, out int max);

            Debug.Log($"[V1/V2] highlighted-vs-before={litDiff:P3}  cleared-vs-before={clearedDiff:P3} " +
                      $"(mean={mean:F3} max={max})  banksHighlighted={banksLitWhileSelected}");

            // V2 first: if the highlight did nothing, V1 is meaningless.
            Assert.Greater(litDiff, 0.05f,
                $"V2: selecting the boat changed only {litDiff:P3} of pixels. A highlight that does " +
                "nothing would make V1 pass vacuously.");

            // V1: the real assertion.
            Assert.LessOrEqual(clearedDiff, 0.005f,
                $"V1: after clearing, {clearedDiff:P3} of pixels still differ from the un-highlighted " +
                $"baseline (mean delta {mean:F3}, max {max}). This is risk R2 - a highlight that did " +
                "not fully restore. Compare 01-before.png against 03-cleared.png.");
        }

        /// <summary>
        /// V1 for BANKS specifically. Banks are where this bug historically manifested, and the
        /// board-wide test above can be dominated by tile pixels. This isolates the bank strip.
        /// </summary>
        [UnityTest]
        public IEnumerator V1b_BankHighlightClearsCompletely()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var boat = SceneFixture.Boat;
            Assert.IsNotNull(boat, "no boat");

            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(Settle);

            // Top bank occupies roughly the upper third of the framed board; bottom the lower.
            // Capture the full frame and compare only those bands, so we are not diluted by tiles.
            string before, lit, after;
            using (var ctx = new DeterministicContext())
                before = CaptureRig.Capture(ctx, Suite, "bank-01-before");

            boat.SelectBoat();
            yield return new WaitForSecondsRealtime(Settle);
            int banksLit = Object.FindObjectsByType<BankClickHandler>(FindObjectsSortMode.None).Length;
            using (var ctx = new DeterministicContext())
                lit = CaptureRig.Capture(ctx, Suite, "bank-02-highlighted");

            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(Settle);
            using (var ctx = new DeterministicContext())
                after = CaptureRig.Capture(ctx, Suite, "bank-03-cleared");

            var iBefore = PixelUtil.Load(before);
            var iAfter = PixelUtil.Load(after);
            var iLit = PixelUtil.Load(lit);

            // Bank rects are DERIVED from the actual bank renderers projected through the pinned
            // camera, not hardcoded. A first pass used fixed pixel bands and the "top bank" band
            // silently sampled orange background - RGBA(141,111,27) - so the top-bank assertion
            // was passing against a region the bank was never in.
            var rbm = Object.FindFirstObjectByType<RiverBankManager>();
            Assert.IsNotNull(rbm, "no RiverBankManager");
            var topGo = rbm.GetBankGameObject(RiverBankManager.BankSide.Top);
            var botGo = rbm.GetBankGameObject(RiverBankManager.BankSide.Bottom);
            Assert.IsNotNull(topGo, "no top bank");
            Assert.IsNotNull(botGo, "no bottom bank");

            RectInt topBand, bottomBand;
            using (var ctx = new DeterministicContext())
            {
                topBand = ScreenRectOf(ctx.Cam, topGo);
                bottomBand = ScreenRectOf(ctx.Cam, botGo);
            }
            Debug.Log($"[V1b] derived bank rects: top={topBand} bottom={bottomBand}");
            Assert.Greater(topBand.width * topBand.height, 1000, "top bank projected to a degenerate rect");
            Assert.Greater(bottomBand.width * bottomBand.height, 1000, "bottom bank projected to a degenerate rect");

            Color32 topBefore = PixelUtil.AverageInRect(iBefore, topBand);
            Color32 topLit = PixelUtil.AverageInRect(iLit, topBand);
            Color32 topAfter = PixelUtil.AverageInRect(iAfter, topBand);
            Color32 botBefore = PixelUtil.AverageInRect(iBefore, bottomBand);
            Color32 botLit = PixelUtil.AverageInRect(iLit, bottomBand);
            Color32 botAfter = PixelUtil.AverageInRect(iAfter, bottomBand);

            int topDelta = PixelUtil.ChannelDelta(topBefore, topAfter);
            int botDelta = PixelUtil.ChannelDelta(botBefore, botAfter);

            Debug.Log($"[V1b banks] banksHighlighted={banksLit}\n" +
                      $"  top    before={topBefore} lit={topLit} after={topAfter} restoreDelta={topDelta}\n" +
                      $"  bottom before={botBefore} lit={botLit} after={botAfter} restoreDelta={botDelta}");

            int topLitDelta = PixelUtil.ChannelDelta(topBefore, topLit);
            int botLitDelta = PixelUtil.ChannelDelta(botBefore, botLit);
            Debug.Log($"[V1b] lit deltas: top={topLitDelta} bottom={botLitDelta}");

            // Without this, V1b would pass vacuously on a build where bank highlighting stopped
            // working entirely: an unlit bank trivially "restores". At least one bank must
            // visibly change while selected for the restore assertions below to mean anything.
            Assert.Greater(Mathf.Max(topLitDelta, botLitDelta), 20,
                $"V1b: no bank visibly highlighted (top delta {topLitDelta}, bottom delta {botLitDelta}, " +
                $"BankClickHandlers={banksLit}). The restore assertions would be vacuous.");

            Assert.LessOrEqual(topDelta, 4,
                $"V1b: top bank did not restore - average colour moved by {topDelta}/255. " +
                "This is the literal 'banks staying cyan' bug.");
            Assert.LessOrEqual(botDelta, 4,
                $"V1b: bottom bank did not restore - average colour moved by {botDelta}/255.");
        }

        /// <summary>
        /// Screen-space rect of a renderer's world bounds, in the RenderTexture's pixel space
        /// (origin bottom-left, matching PixelUtil's indexing). Shrunk 15% toward the centre so
        /// we sample the bank's face rather than its silhouette edge against the background.
        /// </summary>
        static RectInt ScreenRectOf(Camera cam, GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new RectInt(0, 0, 0, 0);

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var sp = cam.WorldToScreenPoint(corner);
                if (sp.z <= 0f) continue;
                minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
            }
            if (minX > maxX || minY > maxY) return new RectInt(0, 0, 0, 0);

            float insetX = (maxX - minX) * 0.15f;
            float insetY = (maxY - minY) * 0.15f;
            int x0 = Mathf.Clamp(Mathf.RoundToInt(minX + insetX), 0, DeterministicContext.Width - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(minY + insetY), 0, DeterministicContext.Height - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(maxX - insetX), 0, DeterministicContext.Width);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(maxY - insetY), 0, DeterministicContext.Height);
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }
    }
}
