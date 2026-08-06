using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// L2, L3, L5 - state integrity. No pixels; these run against live scene state and the
    /// JSON schema.
    /// </summary>
    [TestFixture]
    public class StateIntegrityTests
    {
        // ---------------------------------------------------------------- L2

        /// <summary>
        /// L2. After a row push the boat is re-parented and slid, but currentTile /
        /// currentSnapPoint are not updated by the push itself (CLAUDE.md gotcha 2). The band-aid
        /// is ResynchronizeStateWithTransform, which SelectBoat calls and which logs
        /// "Boat Desync Detected!" when it has to correct something.
        ///
        /// Asserts both halves: the reverse lookup agrees with the boat's own state, AND the
        /// resync path emitted no desync warning.
        /// </summary>
        [UnityTest]
        public IEnumerator L2_BoatStateSyncAfterRowPush()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            var boat = SceneFixture.Boat;
            Assert.IsNotNull(boat, "no boat");

            // Put the boat on a known tile in the row we are about to push, so the push actually
            // moves it - pushing an empty row would prove nothing.
            const int row = 2;                       // unlocked in 01_06 (lockedRows [3,1,0])
            var rideTile = grid.GetTileAt(1, row);
            Assert.IsNotNull(rideTile, "no tile to ride at (1,2)");

            // SceneFixture ends with SelectBoat, which LIFTS the boat and starts it bobbing.
            // FindTileAndSnapPointAtWorldPos compares 3D distance against a 0.5 threshold, so a
            // hovering boat (resting 0.25 + hover 0.5 + 0.1) never matches on Y alone. Settle it
            // to resting height first - which is also the state SelectBoat's resync sees, since
            // ResynchronizeStateWithTransform runs at the TOP of SelectBoat before the lift.
            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(1.2f);
            boat.PlaceOnTile(rideTile, 2);
            yield return new WaitForSecondsRealtime(0.4f);

            var type = SceneFixture.PlayableTileTypes().First(t => t.displayName == "TileCross");
            var hand = new PuzzleHandTile(type) { rotationY = 0f, isFlipped = false };

            yield return grid.PushRowCoroutine(row, true, hand);
            yield return new WaitForSecondsRealtime(0.6f);

            // The push re-selects any boat that was selected when it started, which lifts it
            // again. Settle back to resting height before the reverse lookup.
            boat.DeselectBoat();
            yield return new WaitForSecondsRealtime(1.2f);

            // Reverse lookup: where does the world say the boat is?
            var (foundTile, foundSnap) = grid.FindTileAndSnapPointAtWorldPos(boat.transform.position);
            var stateTile = boat.GetCurrentTile();
            int stateSnap = boat.GetCurrentSnapPoint();

            // Report GRID COORDINATES, not GameObject names: names are stamped "Tile (x,y)" at
            // creation and never updated when a push shifts tiles, so the names are stale labels.
            string Where(TileInstance t)
            {
                if (t == null) return "<none>";
                var c = grid.GetTileCoordinates(t);
                return $"grid({c.x},{c.y})";
            }
            Debug.Log($"[L2] after push: boatState={Where(stateTile)}:{stateSnap} " +
                      $"reverseLookup={Where(foundTile)}:{foundSnap} pos={boat.transform.position} " +
                      $"sameTileInstance={ReferenceEquals(stateTile, foundTile)}");

            // Now watch for the desync warning that SelectBoat's resync would emit.
            bool desyncLogged = false;
            Application.LogCallback onLog = (cond, stack, type_) =>
            {
                if (type_ == LogType.Warning && cond != null && cond.Contains("Boat Desync Detected"))
                    desyncLogged = true;
            };
            Application.logMessageReceived += onLog;
            boat.SelectBoat();
            yield return new WaitForSecondsRealtime(0.8f);
            Application.logMessageReceived -= onLog;

            Debug.Log($"[L2] desync warning emitted by SelectBoat's resync: {desyncLogged}");

            Assert.IsNotNull(foundTile,
                "L2: the reverse transform lookup found no tile under the boat at all - it is not " +
                "sitting on a valid snap point after the push.");

            // The desync WARNING is the load-bearing assertion. It is the product's own judgement
            // that the state was wrong, not the test's inference.
            //
            // The tile-identity comparison is reported but NOT asserted, because adjacent tiles
            // share snap-point positions by design - that is how FindConnectedTile matches
            // connections - so at a shared edge the reverse lookup can legitimately pick the
            // neighbour. Asserting on it would produce failures that are not desyncs.
            Assert.IsFalse(desyncLogged,
                $"L2: ResynchronizeStateWithTransform logged 'Boat Desync Detected!' after a row push. " +
                $"Boat state was {Where(stateTile)}:{stateSnap}, the transform reverse-looked-up to " +
                $"{Where(foundTile)}:{foundSnap}. This is CLAUDE.md gotcha 2 - the push re-parents and " +
                "slides the boat without updating currentTile/currentSnapPoint, and the band-aid resync " +
                "has to correct it on the next SelectBoat. Known defect, listed in Golden/README.md.");
        }

        // ---------------------------------------------------------------- L3

        /// <summary>
        /// L3. Load all 7 levels, re-serialise, reload, assert deep equality. Guards the JSON
        /// schema against every future change.
        ///
        /// Orientation is compared through TileOrientation.MatchesAuthored, never raw
        /// eulerAngles - a flipped tile authored at 0 reads back y=180 (CLAUDE.md gotcha 3).
        /// rotationY floats in the shipped levels look like 0.000005008956122765085, so the raw
        /// field is compared with tolerance rather than equality.
        /// </summary>
        [UnityTest]
        public IEnumerator L3_LevelJsonRoundTripsForAllSevenLevels()
        {
            LogAssert.ignoreFailingMessages = true;

            const float RotTolerance = 0.01f;   // shipped values carry ~5e-06 of float noise
            var rows = new List<string>();
            var failures = new List<string>();

            foreach (var lvl in SceneFixture.AllLevels)
            {
                var asset = Resources.Load<TextAsset>(lvl);
                if (asset == null) { failures.Add($"{lvl}: resource not found"); continue; }

                var original = JsonUtility.FromJson<LevelData>(asset.text);
                var reserialised = JsonUtility.ToJson(original, true);
                var reloaded = JsonUtility.FromJson<LevelData>(reserialised);

                var diffs = CompareLevels(original, reloaded, RotTolerance);
                rows.Add($"  {lvl.Replace("Levels/", ""),-22} tiles={original.tiles.Count,2} " +
                         $"collectibles={original.collectibles.Count} hand={original.playerHand.Count,2} " +
                         $"diffs={diffs.Count}");
                if (diffs.Count > 0) failures.Add($"{lvl}:\n      " + string.Join("\n      ", diffs));

                yield return null;
            }

            Debug.Log("[L3] JSON round trip\n" + string.Join("\n", rows));
            Assert.IsEmpty(failures, "L3 round-trip failures:\n" + string.Join("\n", failures));
        }

        /// <summary>
        /// L3 part two: the LIVE scene must match what the JSON authored, with orientation read
        /// through TileOrientation rather than euler angles.
        /// </summary>
        [UnityTest]
        public IEnumerator L3b_LoadedSceneMatchesAuthoredOrientation()
        {
            LogAssert.ignoreFailingMessages = true;
            var failures = new List<string>();
            int checkedTiles = 0;

            foreach (var lvl in SceneFixture.AllLevels)
            {
                yield return SceneFixture.Load(FixtureMode.Playing, lvl);
                var grid = SceneFixture.Grid;
                var data = JsonUtility.FromJson<LevelData>(Resources.Load<TextAsset>(lvl).text);

                foreach (var td in data.tiles)
                {
                    var t = grid.GetTileAt(td.gridX, td.gridY);
                    if (t == null) { failures.Add($"{lvl} ({td.gridX},{td.gridY}) MISSING"); continue; }
                    checkedTiles++;

                    if (!TileOrientation.MatchesAuthored(t, td.rotationY, td.isFlipped))
                        failures.Add($"{lvl} ({td.gridX},{td.gridY}) authored rotY={td.rotationY} " +
                                     $"flipped={td.isFlipped} but {TileOrientation.Describe(t)}");
                    if (t.IsHardBlocker != td.isHardBlocker)
                        failures.Add($"{lvl} ({td.gridX},{td.gridY}) blocker want={td.isHardBlocker} got={t.IsHardBlocker}");
                    if (t.originalTemplate == null || t.originalTemplate.displayName != td.tileTypeName)
                        failures.Add($"{lvl} ({td.gridX},{td.gridY}) type want={td.tileTypeName} " +
                                     $"got={t.originalTemplate?.displayName}");
                }
            }

            Debug.Log($"[L3b] checked {checkedTiles} tiles across {SceneFixture.AllLevels.Length} levels, " +
                      $"mismatches={failures.Count}");
            Assert.IsEmpty(failures, "L3b live-scene mismatches:\n  " + string.Join("\n  ", failures));
        }

        static List<string> CompareLevels(LevelData a, LevelData b, float rotTol)
        {
            var d = new List<string>();
            if (a.gridWidth != b.gridWidth) d.Add($"gridWidth {a.gridWidth} vs {b.gridWidth}");
            if (a.gridHeight != b.gridHeight) d.Add($"gridHeight {a.gridHeight} vs {b.gridHeight}");
            if (a.maxMoves != b.maxMoves) d.Add($"maxMoves {a.maxMoves} vs {b.maxMoves}");

            if ((a.lockedRows?.Length ?? -1) != (b.lockedRows?.Length ?? -1))
                d.Add($"lockedRows length {a.lockedRows?.Length} vs {b.lockedRows?.Length}");
            else if (a.lockedRows != null)
                for (int i = 0; i < a.lockedRows.Length; i++)
                    if (a.lockedRows[i] != b.lockedRows[i]) d.Add($"lockedRows[{i}] {a.lockedRows[i]} vs {b.lockedRows[i]}");

            if (a.tiles.Count != b.tiles.Count) d.Add($"tile count {a.tiles.Count} vs {b.tiles.Count}");
            else for (int i = 0; i < a.tiles.Count; i++)
            {
                var x = a.tiles[i]; var y = b.tiles[i];
                if (x.tileTypeName != y.tileTypeName) d.Add($"tiles[{i}].type {x.tileTypeName} vs {y.tileTypeName}");
                if (x.gridX != y.gridX || x.gridY != y.gridY) d.Add($"tiles[{i}] coords ({x.gridX},{x.gridY}) vs ({y.gridX},{y.gridY})");
                if (Mathf.Abs(x.rotationY - y.rotationY) > rotTol)
                    d.Add($"tiles[{i}].rotationY {x.rotationY} vs {y.rotationY} (tol {rotTol})");
                if (x.isFlipped != y.isFlipped) d.Add($"tiles[{i}].isFlipped {x.isFlipped} vs {y.isFlipped}");
                if (x.isHardBlocker != y.isHardBlocker) d.Add($"tiles[{i}].isHardBlocker {x.isHardBlocker} vs {y.isHardBlocker}");
            }

            if (a.collectibles.Count != b.collectibles.Count) d.Add($"collectible count {a.collectibles.Count} vs {b.collectibles.Count}");
            else for (int i = 0; i < a.collectibles.Count; i++)
            {
                var x = a.collectibles[i]; var y = b.collectibles[i];
                if (x.gridX != y.gridX || x.gridY != y.gridY || x.type != y.type || x.value != y.value)
                    d.Add($"collectibles[{i}] ({x.gridX},{x.gridY},{x.type},{x.value}) vs ({y.gridX},{y.gridY},{y.type},{y.value})");
            }

            if (a.playerHand.Count != b.playerHand.Count) d.Add($"hand count {a.playerHand.Count} vs {b.playerHand.Count}");
            else for (int i = 0; i < a.playerHand.Count; i++)
            {
                var x = a.playerHand[i]; var y = b.playerHand[i];
                if (x.tileTypeName != y.tileTypeName) d.Add($"hand[{i}].type {x.tileTypeName} vs {y.tileTypeName}");
                if (Mathf.Abs(x.rotationY - y.rotationY) > rotTol) d.Add($"hand[{i}].rotationY {x.rotationY} vs {y.rotationY}");
                if (x.isFlipped != y.isFlipped) d.Add($"hand[{i}].isFlipped {x.isFlipped} vs {y.isFlipped}");
            }

            d.AddRange(CompareGoal("startPosition", a.startPosition, b.startPosition));
            d.AddRange(CompareGoal("endPosition", a.endPosition, b.endPosition));
            return d;
        }

        static IEnumerable<string> CompareGoal(string label, GoalData a, GoalData b)
        {
            if (a == null && b == null) yield break;
            if (a == null || b == null) { yield return $"{label} null mismatch"; yield break; }
            if (a.isBankGoal != b.isBankGoal) yield return $"{label}.isBankGoal {a.isBankGoal} vs {b.isBankGoal}";
            if (a.tileX != b.tileX || a.tileY != b.tileY) yield return $"{label} tile ({a.tileX},{a.tileY}) vs ({b.tileX},{b.tileY})";
            if (a.snapPointIndex != b.snapPointIndex) yield return $"{label}.snapPointIndex {a.snapPointIndex} vs {b.snapPointIndex}";
            if (a.isBankGoal && a.bankSide != b.bankSide) yield return $"{label}.bankSide {a.bankSide} vs {b.bankSide}";
        }

        // ---------------------------------------------------------------- L5

        /// <summary>
        /// L5. Undo after a ROW PUSH specifically, not just a boat move. PushRowCoroutine calls
        /// SaveState itself, and push consolidation (R1) is the next thing we touch - so this is
        /// the undo path most likely to be disturbed.
        /// </summary>
        [UnityTest]
        public IEnumerator L5_UndoRestoresExactStateAfterARowPush()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneFixture.Load(FixtureMode.Playing, "Levels/01_06_TestLevel");

            var grid = SceneFixture.Grid;
            var lem = UnityEngine.Object.FindFirstObjectByType<LevelEditorManager>();
            Assert.IsNotNull(lem, "no LevelEditorManager");
            Assert.IsNotNull(HistoryManager.Instance, "no HistoryManager");

            var before = lem.CreateCurrentStateSnapshot();
            string beforeJson = JsonUtility.ToJson(before);
            Debug.Log($"[L5] pre-push snapshot: tiles={before.tileStates.Count} " +
                      $"hand={before.playerHandState.Count} moves={before.boatMovementPoints}");

            // Act: push a row. PushRowCoroutine calls SaveState at its end.
            const int row = 2;
            var type = SceneFixture.PlayableTileTypes().First(t => t.displayName == "TileCross");
            yield return grid.PushRowCoroutine(row, true, new PuzzleHandTile(type) { isFlipped = false });
            yield return new WaitForSecondsRealtime(0.8f);

            var afterPush = lem.CreateCurrentStateSnapshot();
            string afterPushJson = JsonUtility.ToJson(afterPush);
            Assert.AreNotEqual(beforeJson, afterPushJson,
                "L5 setup: the push changed nothing, so undoing it would prove nothing.");

            // Undo.
            HistoryManager.Instance.Undo();
            yield return new WaitForSecondsRealtime(4.0f);   // undo fades out, rebuilds, fades in

            var restored = lem.CreateCurrentStateSnapshot();
            var diffs = CompareSnapshots(before, restored);

            Debug.Log($"[L5] post-undo: tiles={restored.tileStates.Count} " +
                      $"hand={restored.playerHandState.Count} moves={restored.boatMovementPoints} " +
                      $"diffs={diffs.Count}");

            Assert.IsEmpty(diffs, "L5: state after undo does not match state before the push:\n  " +
                                  string.Join("\n  ", diffs));
        }

        static List<string> CompareSnapshots(GameStateSnapshot a, GameStateSnapshot b)
        {
            var d = new List<string>();
            if (a.tileStates.Count != b.tileStates.Count)
                d.Add($"tile count {a.tileStates.Count} vs {b.tileStates.Count}");
            else
            {
                // Order-independent: compare by grid coordinate.
                var byCoord = b.tileStates.ToDictionary(t => (t.gridX, t.gridY));
                foreach (var t in a.tileStates)
                {
                    if (!byCoord.TryGetValue((t.gridX, t.gridY), out var o))
                    { d.Add($"tile ({t.gridX},{t.gridY}) missing after undo"); continue; }
                    if (t.tileTypeName != o.tileTypeName)
                        d.Add($"tile ({t.gridX},{t.gridY}) type {t.tileTypeName} vs {o.tileTypeName}");
                    if (t.isFlipped != o.isFlipped)
                        d.Add($"tile ({t.gridX},{t.gridY}) isFlipped {t.isFlipped} vs {o.isFlipped}");
                    if (t.isHardBlocker != o.isHardBlocker)
                        d.Add($"tile ({t.gridX},{t.gridY}) isHardBlocker {t.isHardBlocker} vs {o.isHardBlocker}");
                    // rotationY compared with tolerance - float noise in the shipped data.
                    if (Mathf.Abs(Mathf.DeltaAngle(t.rotationY, o.rotationY)) > 1f)
                        d.Add($"tile ({t.gridX},{t.gridY}) rotationY {t.rotationY} vs {o.rotationY}");
                }
            }

            if (a.playerHandState.Count != b.playerHandState.Count)
                d.Add($"hand count {a.playerHandState.Count} vs {b.playerHandState.Count}");
            if (a.boatMovementPoints != b.boatMovementPoints)
                d.Add($"boatMovementPoints {a.boatMovementPoints} vs {b.boatMovementPoints}");
            if (a.boatStarsCollected != b.boatStarsCollected)
                d.Add($"boatStarsCollected {a.boatStarsCollected} vs {b.boatStarsCollected}");
            if (a.collectibleStates.Count != b.collectibleStates.Count)
                d.Add($"collectible count {a.collectibleStates.Count} vs {b.collectibleStates.Count}");

            if ((a.lockedRowsState?.Length ?? -1) != (b.lockedRowsState?.Length ?? -1))
                d.Add("lockedRowsState length mismatch");
            else if (a.lockedRowsState != null)
                for (int i = 0; i < a.lockedRowsState.Length; i++)
                    if (a.lockedRowsState[i] != b.lockedRowsState[i])
                        d.Add($"lockedRowsState[{i}] {a.lockedRowsState[i]} vs {b.lockedRowsState[i]}");
            return d;
        }
    }
}
