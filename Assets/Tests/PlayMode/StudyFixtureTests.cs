using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// Fixtures for the board-shape study. S1 is the regression that matters for ITEM 2 - the
    /// filter must hide study levels from the player's list WITHOUT making them unloadable, or
    /// the study cannot run. S2 validates the four authored levels actually build a board.
    /// </summary>
    [TestFixture]
    public class StudyFixtureTests
    {
        static readonly string[] Study = { "study_3x3", "study_3x6", "study_3x8", "study_6x6" };
        static readonly (string level, int cols, int rows)[] Shapes =
        {
            ("Levels/study_3x3", 3, 3), ("Levels/study_3x6", 3, 6),
            ("Levels/study_3x8", 3, 8), ("Levels/study_6x6", 6, 6),
        };

        [Test]
        public void S1_FilterHidesStudyLevelsButLeavesThemLoadable()
        {
            var warnings = new List<string>();
            Application.LogCallback onLog = (cond, stack, t) =>
            {
                if (t == LogType.Warning || t == LogType.Error) warnings.Add($"{t}: {cond}");
            };

            Application.logMessageReceived += onLog;
            var levels = LevelFinder.GetAllLevels();
            Application.logMessageReceived -= onLog;

            var names = levels.Select(l => l.FilePath).ToList();
            Debug.Log($"[S1] player-facing list ({names.Count}):\n  " + string.Join("\n  ",
                levels.Select(l => $"W{l.WorldNumber} L{l.LevelNumber}  {l.FilePath}  \"{l.Description}\"")));

            Assert.AreEqual(7, levels.Count,
                $"S1: expected the 7 shipped levels, got {levels.Count}: {string.Join(", ", names)}");

            Assert.IsEmpty(names.Where(n => n.Contains(LevelFinder.TestLevelPrefix)).ToList(),
                "S1: a study level leaked into the player-facing list.");

            // Order: world then level, ascending, and no zero-numbered entries (which is what a
            // malformed name would produce and where it would sort to).
            for (int i = 1; i < levels.Count; i++)
                Assert.IsTrue(levels[i].WorldNumber > levels[i - 1].WorldNumber ||
                             (levels[i].WorldNumber == levels[i - 1].WorldNumber &&
                              levels[i].LevelNumber > levels[i - 1].LevelNumber),
                    $"S1: ordering broke at {levels[i - 1].FilePath} -> {levels[i].FilePath}");

            Assert.IsEmpty(levels.Where(l => l.WorldNumber == 0 || l.LevelNumber == 0).ToList(),
                "S1: a level parsed to World 0 / Level 0, which is the malformed-name signature.");

            Assert.IsEmpty(warnings, "S1: GetAllLevels logged warnings:\n  " + string.Join("\n  ", warnings));

            // The filter hides them from the LIST; it must not make them unloadable.
            foreach (var s in Study)
                Assert.IsNotNull(Resources.Load<TextAsset>($"Levels/{s}"),
                    $"S1: {s} is not loadable by direct resource path - the study cannot run.");

            Debug.Log($"[S1] all 4 study levels still load by direct path; " +
                      $"{warnings.Count} warnings during GetAllLevels()");
        }

        [UnityTest]
        public IEnumerator S2_StudyLevelsBuildAValidBoard()
        {
            LogAssert.ignoreFailingMessages = true;
            var rows = new List<string>();
            var failures = new List<string>();

            foreach (var (level, cols, expectRows) in Shapes)
            {
                yield return SceneFixture.Load(FixtureMode.Playing, level);
                var grid = SceneFixture.Grid;
                var boat = SceneFixture.Boat;
                if (boat != null) boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(1.2f);

                int spawned = 0;
                for (int y = 0; y < grid.rows; y++)
                    for (int x = 0; x < grid.cols; x++)
                        if (grid.GetTileAt(x, y) != null) spawned++;

                var goal = Object.FindFirstObjectByType<GoalMarker>();

                string shot;
                using (var ctx = new DeterministicContext())
                {
                    ctx.Quiesce();
                    ctx.FrameBoard();
                    shot = CaptureRig.Capture(ctx, "study-validate", level.Replace("Levels/", ""));
                }
                bool rendered = CaptureRig.LooksRendered(shot);

                rows.Add($"  {level.Replace("Levels/", ""),-12} grid={grid.cols}x{grid.rows} " +
                         $"tiles={spawned}/{cols * expectRows} boat={(boat != null)} " +
                         $"goal={(goal != null)} rendered={rendered}");

                if (grid.cols != cols || grid.rows != expectRows)
                    failures.Add($"{level}: grid is {grid.cols}x{grid.rows}, expected {cols}x{expectRows}");
                if (spawned != cols * expectRows)
                    failures.Add($"{level}: {spawned} tiles spawned, expected {cols * expectRows}");
                if (boat == null) failures.Add($"{level}: no boat");
                if (goal == null) failures.Add($"{level}: no goal marker");
                if (!rendered) failures.Add($"{level}: capture is black");

                yield return new WaitForSecondsRealtime(0.2f);
            }

            Debug.Log("[S2] study level validation\n" + string.Join("\n", rows));
            Assert.IsEmpty(failures, "S2 failures:\n  " + string.Join("\n  ", failures));
        }
    }
}
