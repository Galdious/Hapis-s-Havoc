using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HapisHavoc.Tests
{
    public enum FixtureMode { Editor, Playing, Endless }

    /// <summary>
    /// Loads LevelEditor.unity the way the real game does - by setting the static
    /// LevelSelectManager.LevelToLoad sentinel before the scene loads - then waits, in
    /// wall-clock time, until the board is actually populated.
    ///
    /// All three modes live in this one scene; there is no per-mode scene.
    /// </summary>
    public static class SceneFixture
    {
        public const string GameplayScene = "LevelEditor";
        public const float DefaultTimeout = 90f;

        public static readonly string[] AllLevels =
        {
            "Levels/01_01_BasicMoves",
            "Levels/01_02_BonusMove",
            "Levels/01_03_BasicSkip",
            "Levels/01_04_SimplePush",
            "Levels/01_05_SimpleFall",
            "Levels/01_06_TestLevel",
            "Levels/01_07_OneWayPush",
        };

        public static IEnumerator Load(FixtureMode mode, string levelResourcePath = null,
                                       float timeout = DefaultTimeout)
        {
            switch (mode)
            {
                case FixtureMode.Endless:
                    LevelSelectManager.LevelToLoad = "ENDLESS_MODE";
                    break;
                case FixtureMode.Playing:
                case FixtureMode.Editor:
                    if (string.IsNullOrEmpty(levelResourcePath))
                        throw new ArgumentException($"{mode} mode needs a level resource path.");
                    LevelSelectManager.LevelToLoad = levelResourcePath;
                    break;
            }

            var op = SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single);
            if (op == null)
                throw new InvalidOperationException(
                    $"[SceneFixture] LoadSceneAsync returned null for '{GameplayScene}'. " +
                    "Is it enabled in Build Settings?");

            float deadline = Time.realtimeSinceStartup + timeout;
            while (!op.isDone)
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException($"[SceneFixture] Scene '{GameplayScene}' did not load within {timeout}s.");
                yield return null;
            }

            yield return DeterministicContext.WaitUntil(
                () => GameManager.Instance != null, timeout, "GameManager.Instance");

            if (mode == FixtureMode.Endless)
            {
                yield return DeterministicContext.WaitUntil(
                    () => GridPopulated(), timeout, "endless grid populated");
            }
            else
            {
                // Reconstruction is a coroutine; it flips currentState to Playing at the end
                // via GameManager.UpdateLevelState.
                yield return DeterministicContext.WaitUntil(
                    () => GameManager.Instance.currentState == GameState.Playing && GridPopulated(),
                    timeout,
                    $"GameState.Playing + populated grid for '{levelResourcePath}'");

                if (mode == FixtureMode.Editor)
                {
                    // Mirrors the real "To Editor" button.
                    GameManager.Instance.EnterEditorMode();
                    yield return DeterministicContext.WaitUntil(
                        () => GameManager.Instance.currentMode == OperatingMode.Editor,
                        timeout, "OperatingMode.Editor");
                }
            }

            // One extra settle frame so components that finish work in LateUpdate are done.
            yield return null;
        }

        public static GridManager Grid =>
            UnityEngine.Object.FindFirstObjectByType<GridManager>();

        public static BoatController Boat =>
            UnityEngine.Object.FindFirstObjectByType<BoatController>();

        public static bool GridPopulated()
        {
            var g = Grid;
            if (g == null || g.cols <= 0 || g.rows <= 0) return false;
            for (int y = 0; y < g.rows; y++)
                for (int x = 0; x < g.cols; x++)
                    if (g.GetTileAt(x, y) != null) return true;
            return false;
        }

        public static int TileCount()
        {
            var g = Grid;
            if (g == null) return 0;
            int n = 0;
            for (int y = 0; y < g.rows; y++)
                for (int x = 0; x < g.cols; x++)
                    if (g.GetTileAt(x, y) != null) n++;
            return n;
        }

        /// <summary>All 13 in-play tile templates (TileMax has quantity 0 and is excluded).</summary>
        public static TileType[] PlayableTileTypes()
        {
            var bag = UnityEngine.Object.FindFirstObjectByType<TileBagManager>();
            if (bag == null || bag.tileLibrary == null) return Array.Empty<TileType>();
            return bag.tileLibrary.tileTypes.Where(t => t.quantity > 0).ToArray();
        }
    }
}
