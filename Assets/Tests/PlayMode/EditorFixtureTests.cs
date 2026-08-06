using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// STEP A. Playing mode across all 7 levels and Endless are already measured; this closes
    /// the remaining gap - Editor mode across the 7 levels, plus a blank/new-level case.
    /// </summary>
    [TestFixture]
    public class EditorFixtureTests
    {
        [UnityTest]
        public IEnumerator A1_EditorModeLoadsAllSevenLevels()
        {
            var rows = new List<string>();
            var failures = new List<string>();

            foreach (var lvl in SceneFixture.AllLevels)
            {
                string load = "FAIL", err = "";
                int tiles = 0, lines = 0;
                bool arrows = false;

                var it = TestRunner.Safe(() => SceneFixture.Load(FixtureMode.Editor, lvl), e => err = e);
                while (true)
                {
                    object cur; bool more;
                    try { more = it.MoveNext(); cur = it.Current; }
                    catch (Exception e) { err = e.Message; break; }
                    if (!more) { load = string.IsNullOrEmpty(err) ? "OK" : "FAIL"; break; }
                    yield return cur;
                }

                if (load == "OK")
                {
                    tiles = SceneFixture.TileCount();
                    foreach (var _ in UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)) lines++;
                    // Editor mode builds 3D push arrows rather than drop zones - a good signal
                    // that GenerateControlsForGrid actually took the Editor branch.
                    foreach (var _ in UnityEngine.Object.FindObjectsByType<PointerArrowButton>(FindObjectsSortMode.None))
                    { arrows = true; break; }
                }

                rows.Add($"  {lvl.Replace("Levels/", ""),-22} load={load,-4} mode={GameManager.Instance?.currentMode,-8} " +
                         $"tiles={tiles,3} lineRenderers={lines,4} pushArrows={(arrows ? "yes" : "NO ")} {err}");

                if (load != "OK") failures.Add($"{lvl}: {err}");
                else if (tiles == 0) failures.Add($"{lvl}: zero tiles in Editor mode");
                else if (GameManager.Instance.currentMode != OperatingMode.Editor)
                    failures.Add($"{lvl}: mode is {GameManager.Instance.currentMode}, expected Editor");
            }

            Debug.Log("[A1 Editor mode x7]\n" + string.Join("\n", rows));
            Assert.IsEmpty(failures, "Editor-mode fixture failures:\n" + string.Join("\n", failures));
        }

        /// <summary>
        /// The blank case: open the scene with no level instruction at all, the way the editor
        /// is entered from the main menu. No grid exists until Create Grid is pressed, so this
        /// asserts the scene comes up sane and empty rather than asserting on tiles.
        /// </summary>
        [UnityTest]
        public IEnumerator A2_BlankEditorSceneLoads()
        {
            LogAssert.ignoreFailingMessages = true;
            LevelSelectManager.LevelToLoad = null;

            var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(SceneFixture.GameplayScene);
            float deadline = Time.realtimeSinceStartup + SceneFixture.DefaultTimeout;
            while (op != null && !op.isDone)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("blank scene load timed out");
                yield return null;
            }
            yield return DeterministicContext.WaitUntil(() => GameManager.Instance != null,
                                                        SceneFixture.DefaultTimeout, "GameManager");
            yield return new WaitForSecondsRealtime(0.5f);

            var grid = SceneFixture.Grid;
            Debug.Log($"[A2 blank] mode={GameManager.Instance.currentMode} state={GameManager.Instance.currentState} " +
                      $"gridManager={(grid != null ? "present" : "MISSING")} tiles={SceneFixture.TileCount()}");

            Assert.IsNotNull(grid, "no GridManager in a blank editor scene");
            Assert.AreEqual(OperatingMode.Editor, GameManager.Instance.currentMode,
                            "a blank scene should sit in Editor mode");
            // GetTileAt must survive an unbuilt grid - this is the Job 1.5 ITEM 1 guard.
            Assert.DoesNotThrow(() => grid.GetTileAt(0, 0),
                                "GetTileAt threw on an unbuilt grid - the null guard regressed");
        }
    }

    /// <summary>Shared helper: run a coroutine without letting a throw abort the whole pass.</summary>
    internal static class TestRunner
    {
        public static IEnumerator Safe(Func<IEnumerator> body, Action<string> onErr)
        {
            LogAssert.ignoreFailingMessages = true;
            IEnumerator it;
            try { it = body(); }
            catch (Exception e) { onErr(e.Message); yield break; }
            while (true)
            {
                object cur;
                try { if (!it.MoveNext()) yield break; cur = it.Current; }
                catch (Exception e) { onErr(e.Message); yield break; }
                yield return cur;
            }
        }
    }
}
