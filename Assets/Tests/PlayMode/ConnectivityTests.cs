using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// L10 and X15. CONNECTIVITY, not solvability - the move budget is ignored on purpose.
    ///
    /// The traversal rules are NOT reimplemented here. Reversed tiles forcing straight, seeing
    /// through runs of them, blockers and bank docking all live in BoatController.FindValidMoves,
    /// and this drives that through SelectBoat and reads the result. A second copy of those rules
    /// would drift from the first, which is the failure this project keeps paying for.
    /// </summary>
    [TestFixture]
    public class ConnectivityTests
    {
        /// <summary>A search state: the boat is on this tile, entered at this snap point. Snap
        /// matters - a reversed tile is forced straight, so where you came in decides where you
        /// can leave.</summary>
        readonly struct State : System.IEquatable<State>
        {
            public readonly TileInstance Tile; public readonly int Snap;
            public State(TileInstance t, int s) { Tile = t; Snap = s; }
            public bool Equals(State o) => ReferenceEquals(Tile, o.Tile) && Snap == o.Snap;
            public override int GetHashCode() => (Tile != null ? Tile.GetHashCode() : 0) * 31 + Snap;
        }

        /// <summary>
        /// Breadth-first over reachable (tile, snap) states, asking the boat itself for the
        /// successors of each. Returns whether the goal was reached, plus a trace.
        /// </summary>
        static bool GoalReachable(GridManager grid, BoatController boat, LevelData data,
                                  out string trace)
        {
            // GOAL FROM THE AUTHORED DATA, not from geometry. endPosition says exactly what the
            // goal is; geometry is used ONLY to tell which bank GameObject is the Top one, which
            // is classifying the enum rather than guessing the goal.
            bool bankGoal = data.endPosition != null && data.endPosition.isBankGoal;
            var goalSide = bankGoal ? (RiverBankManager.BankSide)data.endPosition.bankSide
                                    : RiverBankManager.BankSide.Top;
            TileInstance goalTile = null;
            int goalSnap = -1;
            if (!bankGoal && data.endPosition != null)
            {
                goalTile = grid.GetTileAt(data.endPosition.tileX, data.endPosition.tileY);
                goalSnap = data.endPosition.snapPointIndex;      // -1 means any snap on that tile
            }

            float boardZ = 0f; int counted = 0;
            for (int y = 0; y < grid.rows; y++)
                for (int x = 0; x < grid.cols; x++)
                    if (grid.GetTileAt(x, y) != null) { boardZ += grid.GetWorldPosition(x, y).z; counted++; }
            if (counted > 0) boardZ /= counted;

            bool IsGoalBank(GameObject bankGo)
            {
                if (bankGo == null) return false;
                var side = bankGo.transform.position.z > boardZ
                    ? RiverBankManager.BankSide.Top : RiverBankManager.BankSide.Bottom;
                return side == goalSide;
            }

            bool AtGoalTile(State st) =>
                goalTile != null && ReferenceEquals(st.Tile, goalTile) &&
                (goalSnap < 0 || st.Snap == goalSnap);

            var seen = new HashSet<State>();
            var queue = new Queue<State>();
            bool reached = false;
            var log = new List<string>();

            // SEED FROM THE BOAT WHERE IT ALREADY IS, without moving it. Several shipped levels
            // (01_01, 01_02, 01_04) start the boat AT A BANK with no current tile at all, so
            // requiring a start tile would report them unreachable when they are simply
            // embarking. SelectBoat handles both cases - FindValidMoves branches on isAtBank.
            boat.StopAllCoroutines();
            boat.SelectBoat();

            if (bankGoal) foreach (var bankGo in boat.DockableBanks) if (IsGoalBank(bankGo)) reached = true;

            foreach (var next in boat.ValidMoves.ToList())
            {
                if (next == null || next.IsHardBlocker) continue;
                int snap = boat.ValidMoveEntrySnaps.TryGetValue(next, out int e0) ? e0 : -1;
                var ns = new State(next, snap);
                if (seen.Add(ns)) queue.Enqueue(ns);
            }

            if (reached) { trace = $"docked at the {goalSide} bank directly from the start"; return true; }
            if (queue.Count == 0) { trace = "no moves at all from the start"; return false; }

            int expanded = 0;
            while (queue.Count > 0 && expanded < 400)
            {
                var s = queue.Dequeue();
                expanded++;

                boat.StopAllCoroutines();
                boat.PlaceOnTile(s.Tile, s.Snap);
                boat.SelectBoat();                 // populates ValidMoves synchronously

                // Docking at a bank beyond the board's far edge is reaching a bank goal.
                if (bankGoal)
                    foreach (var bankGo in boat.DockableBanks)
                        if (IsGoalBank(bankGo)) { reached = true; break; }
                if (reached) { log.Add($"docked at the {goalSide} bank from {Where(grid, s.Tile)}:{s.Snap}"); break; }

                if (AtGoalTile(s))
                { reached = true; log.Add($"reached the goal tile {Where(grid, s.Tile)} snap {s.Snap}"); break; }

                foreach (var next in boat.ValidMoves.ToList())
                {
                    if (next == null || next.IsHardBlocker) continue;
                    int snap = boat.ValidMoveEntrySnaps.TryGetValue(next, out int e) ? e : -1;
                    var ns = new State(next, snap);
                    if (seen.Add(ns)) queue.Enqueue(ns);
                }
            }

            boat.StopAllCoroutines();
            trace = $"expanded {expanded} state(s), {seen.Count} distinct; " +
                    (log.Count > 0 ? string.Join("; ", log) : "goal never reached");
            return reached;
        }

        static string Where(GridManager g, TileInstance t)
        {
            if (t == null) return "<null>";
            var c = g.GetTileCoordinates(t);
            return $"({c.x},{c.y})";
        }

        static readonly string[] AllLevels =
        {
            "Levels/01_01_BasicMoves", "Levels/01_02_BonusMove", "Levels/01_03_BasicSkip",
            "Levels/01_04_SimplePush", "Levels/01_05_SimpleFall", "Levels/01_06_TestLevel",
            "Levels/01_07_OneWayPush",
            "Levels/study_3x3", "Levels/study_3x6", "Levels/study_3x8", "Levels/study_6x6",
        };

        [UnityTest]
        public IEnumerator L10_GoalIsReachableFromStart()
        {
            LogAssert.ignoreFailingMessages = true;
            var rows = new List<string>();
            var shippedFailures = new List<string>();
            var studyFailures = new List<string>();

            foreach (var lvl in AllLevels)
            {
                yield return SceneFixture.Load(FixtureMode.Playing, lvl);
                var grid = SceneFixture.Grid;
                var boat = SceneFixture.Boat;
                boat.maxMovementPoints = 999; boat.currentMovementPoints = 999;  // connectivity, not budget
                boat.DeselectBoat();
                yield return new WaitForSecondsRealtime(1.0f);

                var data = JsonUtility.FromJson<LevelData>(
                    Resources.Load<TextAsset>(lvl).text);

                // A fully locked level cannot be pushed, so the goal MUST already be reachable -
                // that is assertable. If ANY row is pushable the level may legitimately require a
                // push to open the route, and L10 ignores pushes by design, so it only reports.
                bool anyPushable = data.lockedRows != null &&
                                   data.lockedRows.Any(l => l != (int)RowLockState.BothLocked);

                // Study fixtures ALWAYS assert regardless of lock state. They exist only to be
                // read at different board sizes, so a severed river would silently corrupt the
                // study's conclusion rather than merely being an unsolved puzzle.
                if (lvl.Contains("study_")) anyPushable = false;

                bool ok = GoalReachable(grid, boat, data, out string trace);
                string name = lvl.Replace("Levels/", "");
                rows.Add($"  {(ok ? "REACHABLE  " : "UNREACHABLE")}  {(anyPushable ? "report" : "ASSERT")}  " +
                         $"{name,-20} {trace}");
                if (!ok && !anyPushable)
                    (name.StartsWith("study_") ? studyFailures : shippedFailures).Add(name);

                yield return new WaitForSecondsRealtime(0.2f);
            }

            Debug.Log("[L10] goal reachability (move budget ignored)\n" + string.Join("\n", rows));

            Assert.IsEmpty(shippedFailures,
                "L10: a SHIPPED level's goal is unreachable from its start. This is a live content " +
                "bug, not a fixture problem: " + string.Join(", ", shippedFailures));
            Assert.IsEmpty(studyFailures,
                "L10: a study level's river is severed, so the shape study would misread it as " +
                "evidence about board size: " + string.Join(", ", studyFailures));
        }

        /// <summary>
        /// X15 -> L10 must fail. Severs a study river by turning a mid-river tile into a hard
        /// blocker at runtime, and requires the SAME search to report the goal unreachable.
        /// </summary>
        [UnityTest]
        public IEnumerator X15_L10_FailsOnASeveredRiver()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/study_3x6");

            var grid = SceneFixture.Grid;
            var boat = SceneFixture.Boat;
            boat.maxMovementPoints = 999; boat.currentMovementPoints = 999;
            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(1.0f);

            var d3x6 = JsonUtility.FromJson<LevelData>(Resources.Load<TextAsset>("Levels/study_3x6").text);
            Assert.IsTrue(GoalReachable(grid, boat, d3x6, out string before),
                "X15 setup: study_3x6 was already unreachable, so severing it proves nothing.");

            // Reload, so the second search starts from the level's START rather than wherever
            // the first search left the boat - which would silently begin at the goal.
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/study_3x6");
            grid = SceneFixture.Grid;
            boat = SceneFixture.Boat;
            boat.maxMovementPoints = 999; boat.currentMovementPoints = 999;
            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(1.0f);

            // THE BREAKAGE: the river runs up column 0; block it midway.
            var victim = grid.GetTileAt(0, 3);
            Assert.IsNotNull(victim, "X15: no tile at (0,3) to sever");
            victim.IsHardBlocker = true;
            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(0.5f);

            bool afterOk = GoalReachable(grid, boat, d3x6, out string after);
            Debug.Log($"[X15] before severing: {before}\n      after severing (0,3): {after}");

            Assert.IsFalse(afterOk,
                "X15 META-FAILURE: the river was severed by a hard blocker at (0,3), yet the goal " +
                "is still reported reachable. L10 cannot detect a broken river, so it proves " +
                "nothing. Fix L10, not this control.");
        }
    }
}
