using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// Drives the game the way a PLAYER does, so tests can reach states that only arise from play.
    ///
    /// WHY THIS EXISTS. Several things in this project can only be observed after the game has been
    /// PLAYED for a bit, and the harness had no way to play it. `E3` is the worked example: the
    /// Endless camera is reported to lose the boat when a turn ends, but `EndlessGameLoop` parks on
    /// `WaitUntil(currentAP &lt;= 0 || !isPlayerTurn || currentStamina &lt;= 0)`, and AP only drains when
    /// the player moves. With no input the loop never left the player's turn: E3 sampled a
    /// stationary board for 25 seconds and reported green.
    ///
    /// THROUGH THE REAL MOVE PATH, NOT A SHORTCUT. Every move here goes through
    /// `BoatController.OnTileClicked` - the exact method `ClickableTile.OnPointerClick` calls when a
    /// finger lands on a tile - and the destinations come from the boat's own `ValidMoves`, which
    /// `FindValidMoves` populated. Nothing here sets state directly.
    ///
    /// That distinction is the whole value. A driver that assigned `currentAP = 0` or teleported the
    /// boat would end a turn without ever exercising the code that ends a turn, and would prove
    /// nothing about the camera, the push animation, or the AP accounting. It would be a second
    /// implementation of "make a move" - exactly the duplication that has cost this project five
    /// corrections in the affordance reservation and one in the push.
    ///
    /// WHAT THIS UNLOCKS beyond E3:
    ///   - divergence #3 (`EndlessModeManager` disabled in captures) becomes attackable, because a
    ///     driven turn is reproducible in a way that waiting on a timer is not;
    ///   - the push animation, which no capture has ever shown, becomes reachable;
    ///   - any future assertion about mid-game state stops needing a bespoke setup.
    ///
    /// EVERY WAIT IS ON A CONDITION WITH A WALL-CLOCK TIMEOUT. Never a frame count, never a fixed
    /// sleep standing in for "the move finished" - batchmode game time runs ~15x slower than real
    /// (divergence #7), so a duration that looks generous can be a fraction of one animation.
    /// </summary>
    public static class GameDriver
    {
        /// <summary>Outcome of one attempted move, for logs that have to explain a stall.</summary>
        public enum MoveResult { Moved, NoValidMoves, BoatBusy, TimedOut, NoBoat }

        /// <summary>
        /// Selects the boat through its own public entry point, so valid moves are computed by the
        /// game rather than by the test.
        /// </summary>
        public static IEnumerator SelectBoat(BoatController boat, float timeout = 10f)
        {
            if (boat == null) yield break;
            boat.SelectBoat();

            // SelectBoat highlights valid moves on a delay coroutine; wait for the list, not a
            // guessed duration.
            float deadline = Time.realtimeSinceStartup + timeout;
            while (boat.ValidMoves.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        /// <summary>
        /// Makes ONE move through the real click path. <paramref name="preferForward"/> picks the
        /// destination with the highest grid row, which is how a player heads downriver in Endless -
        /// a boat that oscillates between two tiles drains AP without ever streaming new rows.
        /// </summary>
        public static IEnumerator MakeOneMove(BoatController boat, GridManager grid,
                                              System.Action<MoveResult> report,
                                              bool preferForward = true, float timeout = 15f)
        {
            if (boat == null) { report?.Invoke(MoveResult.NoBoat); yield break; }

            if (boat.IsMoving)
            {
                float busyDeadline = Time.realtimeSinceStartup + timeout;
                while (boat.IsMoving && Time.realtimeSinceStartup < busyDeadline) yield return null;
                if (boat.IsMoving) { report?.Invoke(MoveResult.BoatBusy); yield break; }
            }

            // Re-select and give the game a moment before believing there is nowhere to go.
            // ValidMoves is repopulated by a coroutine after a move lands, so an immediate read can
            // catch it empty and report a dead end that is not one.
            // Six attempts, not three. Measured flaky at three: identical code drove 4 moves and
            // ended the turn on one run and reported a dead end after 1 move on another, because
            // ValidMoves is repopulated by HighlightValidMovesWithDelay and a short retry window
            // can expire while that coroutine is still pending. A dead end must mean "the river
            // offers nothing", never "the game had not finished answering yet".
            for (int attempt = 0; attempt < 6 && boat.ValidMoves.Count == 0; attempt++)
            {
                yield return SelectBoat(boat);
                if (boat.ValidMoves.Count > 0) break;
                yield return new WaitForSecondsRealtime(1.0f);
            }

            if (boat.ValidMoves.Count == 0) { report?.Invoke(MoveResult.NoValidMoves); yield break; }

            TileInstance target = ChooseDestination(boat, grid, preferForward);
            if (target == null) { report?.Invoke(MoveResult.NoValidMoves); yield break; }

            var before = boat.GetCurrentTile();

            // THE REAL PATH. This is what ClickableTile.OnPointerClick calls.
            //
            // THE EVENT DATA HAS TO BE FAITHFUL, not merely non-null. A first version passed a bare
            // PointerEventData and crashed inside DetermineSnapPointFromClick, which does
            // `eventData.pressEventCamera.ScreenPointToRay(eventData.position)` to decide WHICH of
            // two candidate snap points the player aimed at. That is real behaviour a tap depends
            // on, so the fix is to supply what a tap supplies rather than to route around it.
            //
            // pressEventCamera is derived from pointerPressRaycast.module, so the raycast result is
            // populated with the scene's own PhysicsRaycaster and a screen position computed from
            // the destination tile - i.e. a click aimed at the tile being moved to.
            boat.OnTileClicked(target, ClickOn(target));

            // Wait for the move to actually land: the boat stops animating AND its tile changed.
            // Either alone can be true transiently.
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!boat.IsMoving && boat.GetCurrentTile() != before)
                {
                    report?.Invoke(MoveResult.Moved);
                    yield break;
                }
                yield return null;
            }

            report?.Invoke(MoveResult.TimedOut);
        }

        /// <summary>
        /// A PointerEventData equivalent to a tap on <paramref name="target"/>: correct screen
        /// position, and a press raycast carrying the scene's raycaster so `pressEventCamera`
        /// resolves. Without the raycast module, `pressEventCamera` is null and the real click path
        /// throws.
        /// </summary>
        static PointerEventData ClickOn(TileInstance target)
        {
            var data = new PointerEventData(EventSystem.current);
            var cam = Camera.main;
            if (cam == null || target == null) return data;

            Vector3 screen = cam.WorldToScreenPoint(target.transform.position);
            data.position = new Vector2(screen.x, screen.y);

            var raycaster = cam.GetComponent<BaseRaycaster>();
            if (raycaster == null) return data;

            var hit = new RaycastResult
            {
                gameObject = target.gameObject,
                module = raycaster,
                distance = screen.z,
                worldPosition = target.transform.position,
                worldNormal = Vector3.up,
                screenPosition = data.position,
            };
            data.pointerPressRaycast = hit;
            data.pointerCurrentRaycast = hit;
            return data;
        }

        /// <summary>
        /// Highest-row valid destination, or the first one if the grid cannot say. Rows increase
        /// downriver, which is the direction that streams new content in Endless.
        /// </summary>
        static TileInstance ChooseDestination(BoatController boat, GridManager grid, bool preferForward)
        {
            TileInstance best = null;
            int bestRow = int.MinValue;

            foreach (var t in boat.ValidMoves)
            {
                if (t == null) continue;
                if (!preferForward || grid == null) return t;

                var coords = grid.GetTileCoordinates(t);
                if (coords.y > bestRow) { bestRow = coords.y; best = t; }
            }
            return best;
        }

        /// <summary>
        /// Plays moves until the Endless turn ends - AP exhausted - or the wall clock runs out.
        /// Returns how many moves actually landed, so a caller can say "the game did not respond"
        /// with a number instead of a guess.
        /// </summary>
        /// <param name="preferForward">
        /// Heading downriver is what a player does, but it also STRANDS the boat: measured, a
        /// forward-most walk dead-ended after one move on a generated river, leaving zero valid
        /// moves and a turn that never ended. When the goal is simply to reach the end of a turn,
        /// AP is what matters and direction is not - so this defaults to false and takes whatever
        /// move is available. Pass true when the point is to stream new rows.
        ///
        /// The honest limit: a real player who runs out of moves PUSHES A ROW from the hand, and
        /// this driver cannot yet do that. Until it can, a stranded boat ends the drive rather than
        /// the turn.
        /// </param>
        public static IEnumerator PlayUntilTurnEnds(BoatController boat, GridManager grid,
                                                    System.Func<bool> turnIsOver,
                                                    List<string> log,
                                                    System.Action<int> movesMade,
                                                    float timeout = 90f,
                                                    bool preferForward = false)
        {
            int moves = 0;
            float deadline = Time.realtimeSinceStartup + timeout;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (turnIsOver != null && turnIsOver()) break;

                MoveResult result = MoveResult.NoBoat;
                yield return MakeOneMove(boat, grid, r => result = r, preferForward);

                var tile = boat.GetCurrentTile();
                string where = tile == null ? "<null tile - on a bank?>"
                    : (grid != null ? $"{grid.GetTileCoordinates(tile)}" : tile.name);
                log?.Add($"    move {moves + 1}: {result}  boat at {where} " +
                         $"selected={boat.isSelected} validMoves={boat.ValidMoves.Count} " +
                         $"moving={boat.IsMoving}");

                if (result == MoveResult.Moved) { moves++; continue; }

                // Nothing more can be driven. Stop rather than spin: a caller that wanted a turn
                // ending needs to know it did not get one, and why.
                break;
            }

            movesMade?.Invoke(moves);
        }
    }
}
