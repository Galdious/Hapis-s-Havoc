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
        static bool GoalReachable(GridManager grid, BoatController boat, out string trace)
        {
            var goalMarker = Object.FindFirstObjectByType<GoalMarker>();
            // Bank goals are the common case. Resolve the goal bank GEOMETRICALLY - the bank
            // further along +Z is the far one - rather than trusting an enum ordering.
            float boardZ = 0f; int counted = 0;
            for (int y = 0; y < grid.rows; y++)
                for (int x = 0; x < grid.cols; x++)
                    if (grid.GetTileAt(x, y) != null) { boardZ += grid.GetWorldPosition(x, y).z; counted++; }
            if (counted > 0) boardZ /= counted;

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

            foreach (var bankGo in boat.DockableBanks)
                if (bankGo != null && bankGo.transform.position.z > boardZ) reached = true;

            foreach (var next in boat.ValidMoves.ToList())
            {
                if (next == null || next.IsHardBlocker) continue;
                int snap = boat.ValidMoveEntrySnaps.TryGetValue(next, out int e0) ? e0 : -1;
                var ns = new State(next, snap);
                if (seen.Add(ns)) queue.Enqueue(ns);
            }

            if (reached) { trace = "docked at the far bank directly from the start"; return true; }
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
                foreach (var bankGo in boat.DockableBanks)
                {
                    if (bankGo == null) continue;
                    if (bankGo.transform.position.z > boardZ) { reached = true; break; }
                }
                if (reached) { log.Add($"docked at the far bank from {Where(grid, s.Tile)}:{s.Snap}"); break; }

                // A tile goal: the marker's tile is reachable.
                if (goalMarker != null && goalMarker.GetComponentInParent<TileInstance>() == s.Tile)
                { reached = true; log.Add($"reached the goal tile {Where(grid, s.Tile)}"); break; }

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

                bool ok = GoalReachable(grid, boat, out string trace);
                string name = lvl.Replace("Levels/", "");
                rows.Add($"  {(ok ? "REACHABLE  " : "UNREACHABLE")}  {name,-20} {trace}");
                if (!ok) (name.StartsWith("study_") ? studyFailures : shippedFailures).Add(name);

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

            Assert.IsTrue(GoalReachable(grid, boat, out string before),
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

            bool afterOk = GoalReachable(grid, boat, out string after);
            Debug.Log($"[X15] before severing: {before}\n      after severing (0,3): {after}");

            Assert.IsFalse(afterOk,
                "X15 META-FAILURE: the river was severed by a hard blocker at (0,3), yet the goal " +
                "is still reported reachable. L10 cannot detect a broken river, so it proves " +
                "nothing. Fix L10, not this control.");
        }
    }
}
