# Hapi's Havoc — Agent Brief

Unity 6.3 LTS (6000.3.21f1) URP mobile puzzle game. Full detail: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## 1. Project snapshot

A grid of domino-shaped river tiles, each printed with curved path segments between 6 edge
"snap points". A boat travels along whatever connected path the tiles happen to form, and the
player reshapes the river by shoving new tiles into a row from the left or right, pushing the
far tile off the board. The goal is to reach a target tile or a river bank before the move
budget runs out.

Three operating modes, selected by `GameManager.currentMode` (`OperatingMode` enum):

| Mode | What it is | Scene |
|---|---|---|
| `Editor` | Level authoring: paint/rotate/flip tiles, set start/end, build the player's hand, save JSON | `Assets/_Project/Scenes/LevelEditor.unity` |
| `Playing` | Puzzle mode: play a level loaded from `Assets/Resources/Levels/*.json` | same scene |
| `Endless` | Procedural infinite river with stamina/AP, storm pushes, PlayerPrefs save | same scene |

**All three modes live in the one `LevelEditor.unity` scene.** There is no per-mode scene.
`MainMenu.unity` and `LevelSelect.unity` are the only other live scenes; `PuzzleGame.unity`,
`TestingBed.unity`, `SampleScene.unity` are legacy. Mode is chosen by the static string
`LevelSelectManager.LevelToLoad` — a level resource path, `"ENDLESS_MODE"`, or
`"RESUME_ENDLESS_MODE"` — read once by `GameManager.LoadLevelAfterSceneIsReady()`.

## 2. How to navigate

| File | Responsibility |
|---|---|
| `Assets/_Project/Scripts/GameManager.cs` (530) | Mode + `GameState`, win/loss evaluation, star scoring, next-level flow. Singleton. |
| `Assets/_Project/Scripts/GridManager.cs` (1892) | **God object.** Grid array, tile spawn, all three row-push coroutines, ejection physics, blocker/vortex visuals. |
| `Assets/_Project/Scripts/BoatController.cs` (1919) | **God object.** Pathfinding across snap points, valid-move search, highlights, all boat animation, collectibles. |
| `Assets/_Project/Scripts/LevelEditorManager.cs` (2333) | **God object.** Editor tools, hand palette, level save/load JSON, full level reconstruction (also used by Undo and Playing mode). |
| `Assets/_Project/Scripts/EndlessModeManager.cs` (1229) | **God object.** Endless game loop, stamina/AP, world streaming, forecast pushes, camera proxy, PlayerPrefs save/resume. |
| `Assets/_Project/Scripts/RiverControls.cs` (813) | Push arrows (Editor) and drop zones (Playing/Endless), row lock states, row hover slide. |
| `Assets/_Project/Scripts/UIManager.cs` (441) | Panel switching per mode, win/fail/endless-score screens. Singleton. |
| `Assets/_Project/Scripts/TileInstance.cs` (34) | Per-tile data: `snapPoints[6]`, `connections`, `IsReversed`, `IsHardBlocker`. |
| `Assets/_Project/Scripts/PathVisualizer.cs` (275) | Draws one `LineRenderer` GameObject per tile connection; path highlight colouring. |
| `Assets/_Project/Scripts/LevelData.cs` (80) | The JSON schema (`LevelData`, `TileSaveData`, `CollectibleSaveData`, `HandTileSaveData`, `GoalData`). |
| `Assets/_Project/Scripts/HistoryManager.cs` (194) | Undo stack of `GameStateSnapshot`; replays via `LevelEditorManager.ReconstructLevelFromDataCoroutine`. Singleton. |

**Do not grow the four files marked god object.** They are 55% of the 13.7k-line codebase.

## 3. Domain conventions

## Snap points
Tiles are domino-shaped (horizontal, 2 cells wide) with 6 snap points:
  0 = top-left      1 = top-right
  2 = bottom-left   3 = bottom-right
  4 = right edge    5 = left edge
When a tile is NOT rotated: left-side snaps = {0, 2, 5}, right-side = {1, 3, 4}.
A tile rotated 180 degrees swaps these sets.
All path connections are bidirectional.

## Tile catalogue (13 types + 1 disabled)
  1  TileSimple_01        0-5, 2-1, 3-4
  2  TileSimple_02        2-5, 0-3, 1-4
  3  TileFace (U-shape)   0-5, 1-4, 2-3
  4  TileCross            0-2, 1-3, 5-4
  5  TileTurn_01          0-5, 2-4, 1-3
  6  TileTurn_02          0-2, 1-4, 3-5
  7  TileTurn_03          0-5, 2-5, 2-1, 3-4
  8  TileTurn_04          0-5, 1-4, 2-5, 0-3
  9  TileChange           0-5, 2-5, 1-4, 3-4
 10  TileTurnChange_01    0-5, 2-5, 2-4, 1-3
 11  TileTurnChange_02    3-5, 0-2, 1-4, 3-4
 12  TileThroughTurn_01   0-5, 0-2, 1-3, 3-4
 13  TileThroughTurn_02   2-5, 0-2, 1-3, 1-4
  -  (max tile, disabled) 0-5, 2-5, 0-3, 1-4, 3-4, 1-2

## Tile states
- Normal (blue/passable): boat follows the drawn path connections.
- Flipped / "reversed" (vortex visual): passable but movement across it is
  FORCED STRAIGHT — no turning inside a reversed tile. Pathfinding "sees
  through" a run of adjacent reversed tiles to find the next normal tile.
  This is the push-your-luck mechanic.
- Hard blocker (rock visual): impassable, full stop. Distinct from flipped.

## Two separate budgets (puzzle mode)
- Move points: spent by moving the boat. Set per level by `maxMoves`.
- Hand tiles: spent by pushing a row. A finite inventory in `playerHand`.
These are INDEPENDENT currencies. Pushing a row does not cost move points.
`lockedRows` marks rows that cannot be pushed at all.

## Banks
Top and bottom edges are banks. The boat starts at a bank, embarks onto an
edge row, and may return to a bank. Embark arrows are non-interactive and
point inward (away from the bank) regardless of which bank the boat is on.

> **Naming trap:** items 12/13 above are spelled **`TileThriughTurn_01` / `TileThriughTurn_02`**
> (typo) in `HapiTileLibrary.asset` and in all six shipped level JSONs. Tile lookup is by exact
> `displayName` string, so the typo is load-bearing — do not "fix" it without migrating the assets.

## 4. Gotchas — recurring bug classes

The changelog headers on `BoatController.cs`, `GridManager.cs` and the inline `--- THIS IS THE
FIX ---` comments throughout record the same handful of failures over and over:

1. **Stuck highlights from direct material mutation.** `BoatController.cs` header v03 is
   literally "Fixes the bank material bug… banks staying cyan". The code writes
   `renderer.material.color = X` (which silently instantiates a material) and restores with
   `renderer.sharedMaterial = original` (which orphans that instance). Every highlight path in
   `BoatController.HighlightTile`, `HighlightBankForDocking`, `LevelEditorManager.HighlightPaletteTile`
   / `ClearPaletteHighlight` / `ClearHandHighlight` still does this. See House rule 2.

2. **A FALSE boat desync after a row push.** The obvious diagnosis is wrong, and the wrong one
   sat in this file for months: the push does **not** fail to update `currentTile` /
   `currentSnapPoint`. Measured through a push, with the boat riding a sliding tile:

   | stage | boat position | distance to its own snap point | reverse lookup |
   |---|---|---|---|
   | before the push | `(0.500, -0.250, 0.750)` | `0.1500` | agrees |
   | immediately after | `(2.600, -0.250, 0.750)` | `0.1500` | agrees |
   | after settling to rest | `(2.600, `**`+0.250`**`, 0.750)` | `0.5220` | **disagrees** |

   The push tracks the boat correctly — `currentTile` follows the sliding tile and the offset is
   preserved to the digit. The failure appeared only once the boat settled, and the only thing
   that changed was Y.

   The real cause was `GridManager.FindTileAndSnapPointAtWorldPos` comparing in **3D** against a
   `0.5` threshold while a resting boat sits `0.5` **above** the snap plane. The Y offset alone
   exhausted the entire budget, so any in-tile XZ offset tipped it over; the lookup then fell
   through to a coincident snap point on the neighbouring tile across a shared edge, and
   `ResynchronizeStateWithTransform` "corrected" the boat onto the wrong tile.

   **Fixed:** the lookup compares in the **horizontal plane only**. Every snap point lies on the
   same flat Y, while the boat's Y swings with resting height, selection lift and idle bob —
   none of which says anything about *which* snap point it is on. The small inward `boatOffset`
   is what distinguishes the two coincident snap points at a shared edge, and dropping Y is
   precisely what lets it do that job: the boat now resolves to its own tile at `0.115` instead
   of the neighbour.

   `ResynchronizeStateWithTransform()` is **retained as a safety net and should now never fire.**
   If `Boat Desync Detected!` appears again, something genuinely moved the boat without updating
   its state — diagnose that, do not widen the threshold. `L2` (the warning must not fire after a
   push) and `X8` (a boat shoved a full tile sideways must still be caught) guard both directions.

3. **Rotation / snap-point mirroring.** A 180° Y rotation swaps the left and right snap sets,
   and several places re-derive that independently. There are **two different**
   `GetOppositeSnapPoint` methods with the same name and different meaning:
   `BoatController` (0↔2, 1↔3, 4↔5 = straight through) vs `GridManager` (0↔3, 1↔2, 4↔5 =
   mirror). Picking the wrong one puts the boat on the wrong edge after an ejection.
   `Mathf.RoundToInt(transform.eulerAngles.y) == 180` is the project's idiom for "is rotated" —
   note the shipped levels store `rotationY` values like `0.000005008956122765085`, so exact
   float comparison is not safe.

   **That idiom is broken for flipped tiles, and this is measured, not theoretical.** A tile is
   built as `Quaternion.Euler(isFlipped ? 180 : 0, rotationY, 0)`. Unity normalises the euler
   representation of that product, so `eulerAngles.y` does **not** read back as the authored
   `rotationY` once the X flip is involved:

   | authored | `eulerAngles.y` reads |
   |---|---|
   | `rotationY=0,   isFlipped=false` | `0` |
   | `rotationY=180, isFlipped=false` | `180` |
   | `rotationY=0,   isFlipped=true`  | **`180`** — inverted |
   | `rotationY=180, isFlipped=true`  | **`0`** — inverted |

   For a flipped tile the value is exactly inverted relative to the authored `rotationY`.
   Confirmed on `01_06_TestLevel`: tiles (1,0) and (1,1) are authored `rotationY≈0` + flipped
   and read back `180`; tile (1,2) is authored `rotationY=180` + flipped and reads back `0`.

   > **Never compare tile rotation by reading `transform.eulerAngles.y` directly.** Use
   > `TileOrientation` (`Assets/_Project/Scripts/TileOrientation.cs`) everywhere — gameplay and
   > tests alike. It reads basis vectors, which are representation-independent:
   > `IsYawFlipped(t)` (local +X points along world −X), `IsFaceFlipped(t)` (local +Y points
   > down), plus `SameOrientation(a, b)`, `MatchesAuthored(t, rotationY, isFlipped)` and
   > `Describe(t)` for failure messages.

   `TileOrientation` also distinguishes an **X flip from a Z flip** (`Rz(180)` inverts local +X,
   `Rx(180)` does not). That is deliberate: `GridManager`'s three `PushRowCoroutine` overloads
   do not agree on which axis they flip (risk R1), and a rotation comparison that hid that
   difference would make the parity test unable to see the bug it exists to catch.

4. **Coroutine collisions / `StopAllCoroutines()`.** `DeselectBoat` has an explicit comment
   removing `StopAllCoroutines()` because it was killing in-flight tile-lowering animations.
   `PrepareForForcedMove`, `OnBankClicked`, `MoveFromBankToTile` and `EndlessModeManager.EndGame`
   all still call it. Anything long-running started on those objects can be cancelled mid-way.

5. **Destroyed-object access.** Ejected tiles, drop zones and goal markers get `Destroy()`d while
   coroutines still hold references. `?.` does **not** protect against Unity's fake-null, so
   `push.forecastZone?.HideForecast()` on a destroyed zone throws `MissingReferenceException`.
   Use `if (obj != null)` (Unity's overloaded operator), never `?.`, on `UnityEngine.Object`.

6. **Data structures drifting out of sync during Endless streaming.** `GridManager.ExpandGridForEndless`
   and `RiverControls.ExpandLockStates` must be grown together — the "THIS IS THE FIX" comment in
   `EndlessModeManager.GenerateMissingRows` exists because they were previously expanded inside
   the row loop and diverged.

7. **Three near-identical copies of the push.** `GridManager.PushRowCoroutine` has three
   overloads (~200 lines each) that are copy-paste variants. Fixes applied to one have
   historically not been applied to the others. Assume any bug you find in one is in all three.

## 5. House rules

- **NO GATE MAY BE EXPRESSED IN FRAMES.** Shipping code gates on **game time**; harness waits use
  the **wall clock**. Batchmode runs frames at roughly 1 ms while game time advances about 15x
  slower than real time, so a frame count means nothing and the two clocks are not
  interchangeable. This has caused three defects: a blend misread as frozen (it was advancing
  0.0008/frame and printing as 0.02 twice), study_3x3 framing against a board that had not
  finished arriving, and X17's wait assumptions being silently invalidated.

- **THE HARNESS MUST NOT DISABLE ANYTHING THE GAME RUNS.** Anything suppressed for determinism
  must either be genuinely absent in play, or be documented as a known divergence with a test
  that detects drift. Three real bugs have hidden behind suppressions that were individually
  reasonable: the hand palette (concealed the framing bounds bug), `BoardFraming` being
  harness-only (framing never applied in play), and `CinemachineBrain` (the captured camera was
  never the real one). `DeterministicContext.Suppressions` is the declared list and
  `X18` fails if it drifts from the allowlist in
  [docs/audit/HARNESS_DIVERGENCE.md](docs/audit/HARNESS_DIVERGENCE.md).

- Do not add code to BoatController.cs, GridManager.cs, LevelEditorManager.cs
  or EndlessModeManager.cs. They are already too large. New behaviour goes in
  a new focused class.
- Do not use renderer.material or renderer.sharedMaterial to change colours or
  highlights. Use MaterialPropertyBlock. Direct material mutation is the root
  cause of a recurring family of "highlight stuck on" bugs in this project.
- Prefer serialised references over FindFirstObjectByType. Where a runtime
  lookup is unavoidable, do it once in Awake and cache it. Never in Update.
- Any change to the level JSON schema must be backward compatible with the
  six existing levels in Assets/Resources/Levels.
- Every change must work in all three operating modes (Editor, Playing,
  Endless) or explicitly state which mode it is scoped to.
- This is a mobile target. Per-frame allocation and draw-call count matter.
- Do not run Unity or attempt to build, EXCEPT via `tools/run-tests.sh` (see Testing).
  Outside the harness, report what needs testing instead.

## 6. Testing

Harness lives in `Assets/Tests/` (PlayMode + Editor asmdefs). Run it with
`tools/run-tests.sh [filter]`. Captures land in `TestOutput/Captures/` (gitignored);
the committed reference set is `Assets/Tests/Golden/` (tracked).

Standing rules:

- **The Unity Editor MUST be closed before running the harness.** Unity takes an exclusive
  lock on the project; a second instance silently does nothing. `run-tests.sh` checks for the
  lock and fails loudly, but check yourself first.
- **Run the suite before every commit.**
- **After running, READ YOUR OWN CAPTURES before presenting results.** Look at the PNGs.
  Iterate at least once on what you actually see, and say what you changed after looking.
  "The test passed" is not evidence the image is right — the first framing pass here rendered
  the board in a middle band with the hand palette sliced off at the frame edge, and every
  assertion still went green.
- **Any new assertion ships with a matching meta-test proving it can fail.** A meta-test encodes an assumption
  about **ownership** — which component is allowed to write a thing — so it breaks by design when
  ownership moves. That is the control working. **Never resolve such a break with a tolerance
  change**; rewrite the breakage so it breaks what the new owner will not put back. X17 is the
  worked example: displacing `Camera.main` stopped being a breakage once Cinemachine's Brain
  began restoring it every LateUpdate. A test that has
  never been observed to go red is not a test. Broken controls live in `Assets/Tests/BrokenControls/`.
- **`-batchmode` WITHOUT `-nographics`, always.** `-nographics` kills the render loop, every
  capture comes back black, and every image comparison then passes vacuously.
- Determinism is the foundation. If `DeterminismGateTests` is red, every other visual result
  is noise — fix that first and do not interpret anything downstream.
- Captures render through an explicit synchronous `Camera.Render()` into a fixed 1080x1920
  RenderTexture, never the frame loop. Waits are wall-clock (`WaitForSecondsRealtime`) with
  loud timeouts, never frame counts — batchmode runs uncapped.
- **Do not enable *Disable Domain Reload*** in Enter Play Mode Options. The project relies on
  six singletons (`GameManager`, `UIManager`, `HistoryManager`, `FloatingTextManager`,
  `ScreenFader`, `CameraManager`) plus the static `LevelSelectManager.LevelToLoad`; without
  domain reload they leak across tests and failures become non-reproducible.
