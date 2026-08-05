using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// L6. The measurement that justifies the procedural path-mesh work later.
    /// ARCHITECTURE.md 5.3 ESTIMATES ~127 path LineRenderers and ~170 total draw calls on a
    /// 6x6 board, derived from object counts plus the batching settings. This replaces the
    /// estimate with a reading.
    /// </summary>
    [TestFixture]
    public class DrawCallTests
    {
        /// <summary>Threshold is set from today's real number in the assertion below, not guessed.</summary>
        [UnityTest]
        public IEnumerator L6_DrawCallCeilingOnASixBySixBoard()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            Assert.IsNotNull(grid, "no GridManager");

            // Playing mode builds the bag from the player's HAND (ApplyHandToBag), so it holds
            // only as many tiles as the level ships - 9 for 01_06. CreateGridFromEditor draws
            // one per cell and silently skips when the bag runs dry, which produced a 9-tile
            // "6x6" on the first attempt. Refill from the full library first.
            Assert.IsNotNull(grid.bagManager, "no TileBagManager");
            grid.bagManager.BuildBag();
            Debug.Log($"[L6] bag refilled from library: {grid.bagManager.TilesRemaining} tiles available");

            UnityEngine.Random.InitState(DeterministicContext.DefaultSeed);
            var anims = grid.CreateGridFromEditor(6, 6);
            if (anims != null)
                foreach (var a in anims) if (a != null) yield return a;
            yield return new WaitForSecondsRealtime(1.0f);

            int tiles = SceneFixture.TileCount();
            int lineRenderers = 0, meshRenderers = 0;
            foreach (var _ in Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)) lineRenderers++;
            foreach (var _ in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) meshRenderers++;

            int drawCalls = -1, setPass = -1, batches = -1;
            using (var ctx = new DeterministicContext())
            {
                var p = CaptureRig.Capture(ctx, "drawcalls", "6x6-board");
                Assert.IsTrue(CaptureRig.LooksRendered(p), "6x6 capture is black - numbers would be meaningless");

                // UnityStats reflects the last frame the editor rendered. A manual Camera.Render()
                // into a RenderTexture does not necessarily update it, so let a real frame elapse
                // before reading rather than trusting the off-screen render.
                yield return new WaitForEndOfFrame();
#if UNITY_EDITOR
                drawCalls = UnityEditor.UnityStats.drawCalls;
                setPass = UnityEditor.UnityStats.setPassCalls;
                batches = UnityEditor.UnityStats.batches;
#endif
            }

            Debug.Log($"[L6] 6x6 board MEASURED: tiles={tiles} lineRenderers={lineRenderers} " +
                      $"meshRenderers={meshRenderers} drawCalls={drawCalls} setPassCalls={setPass} batches={batches}\n" +
                      $"     ARCHITECTURE.md 5.3 estimated ~127 line renderers and ~170 draw calls.");

            Assert.AreEqual(36, tiles, "expected a full 6x6 = 36 tiles");

            // The renderer counts are the load-bearing measurement for the procedural path-mesh
            // argument, and they are reliable headlessly. Assert on those unconditionally.
            const int LineRendererCeiling = 160;   // measured 140 on 2026-08-05
            Assert.LessOrEqual(lineRenderers, LineRendererCeiling,
                $"L6: {lineRenderers} LineRenderers on a 6x6 board, over the {LineRendererCeiling} ceiling. " +
                "Each is its own draw call - dynamic batching is off in both RP assets and LineRenderer " +
                "is not SRP-Batcher compatible.");

            if (drawCalls > 0)
            {
                const int DrawCallCeiling = 1150;      // measured 1040 on 2026-08-05
                Assert.LessOrEqual(drawCalls, DrawCallCeiling,
                    $"L6: 6x6 board drew {drawCalls} calls, over the {DrawCallCeiling} ceiling. " +
                    "Baseline was 1040 with 140 LineRenderers. If this rose from new content, " +
                    "re-baseline deliberately; if from path rendering, the procedural mesh work is due.");
            }
            else
            {
                // Stated plainly rather than silently passing on an unavailable stat.
                Debug.LogWarning("[L6] UnityStats.drawCalls read 0 in batchmode - the profiler counters " +
                                 "are not populated without a Game View frame. The LineRenderer and " +
                                 "MeshRenderer counts above ARE real and are asserted; the draw-call " +
                                 "total needs a Frame Debugger capture in the Editor to confirm.");
                Assert.Inconclusive($"L6: renderer counts measured (lineRenderers={lineRenderers}, " +
                                    $"meshRenderers={meshRenderers}) but UnityStats.drawCalls is " +
                                    "unavailable in batchmode. See warning above.");
            }
        }
    }
}
