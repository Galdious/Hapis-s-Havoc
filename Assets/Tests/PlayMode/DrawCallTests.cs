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

            // The raw reading counts everything the editor rendered that frame, not just the game
            // camera - which is why it came out ~6x the ARCHITECTURE.md estimate while the project
            // runs above 200fps in normal play. Isolate the BOARD's contribution by re-reading
            // with every tile hidden and subtracting.
            var hidden = new List<GameObject>();
            for (int y = 0; y < grid.rows; y++)
                for (int x = 0; x < grid.cols; x++)
                {
                    var t = grid.GetTileAt(x, y);
                    if (t != null && t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden.Add(t.gameObject); }
                }
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            int emptyDrawCalls = -1, emptySetPass = -1, emptyBatches = -1;
#if UNITY_EDITOR
            emptyDrawCalls = UnityEditor.UnityStats.drawCalls;
            emptySetPass = UnityEditor.UnityStats.setPassCalls;
            emptyBatches = UnityEditor.UnityStats.batches;
#endif
            foreach (var go in hidden) if (go != null) go.SetActive(true);
            yield return new WaitForEndOfFrame();

            int boardDrawCalls = drawCalls - emptyDrawCalls;
            int boardSetPass = setPass - emptySetPass;
            int boardBatches = batches - emptyBatches;
            Debug.Log($"[L6] harness/editor baseline with all {hidden.Count} tiles hidden: " +
                      $"drawCalls={emptyDrawCalls} setPassCalls={emptySetPass} batches={emptyBatches}\n" +
                      $"     BOARD CONTRIBUTION = {boardDrawCalls} drawCalls, {boardSetPass} setPassCalls, " +
                      $"{boardBatches} batches");

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

            // DELIBERATELY NOT ASSERTING ON UnityStats.drawCalls.
            //
            // Hiding all 36 tiles changed it by exactly ZERO (1028 -> 1028, setPass 70 -> 70,
            // batches 266 -> 266). A counter that does not move when the entire board is removed
            // is not measuring the board. UnityStats in a batchmode PlayMode test reports editor
            // overhead, not the pinned game camera, so any ceiling built on it would be a number
            // that cannot regress and cannot inform.
            //
            // This also RETRACTS the earlier claim that ARCHITECTURE.md's ~170 estimate was "6x
            // low". That conclusion came from this same invalid counter. The estimate is not
            // refuted; it is simply unconfirmed headlessly and needs a Frame Debugger capture.
            Assert.AreEqual(0, boardDrawCalls,
                $"L6: hiding the whole board changed UnityStats.drawCalls by {boardDrawCalls}. If this " +
                "ever becomes non-zero the counter has started tracking the game camera and a real " +
                "draw-call ceiling becomes possible - revisit this test.");
        }
    }
}
