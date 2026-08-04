# Claude Code — Phase C1: capture harness + assertion suite

**Built on your current Unity (6000.2.0b8), before the 6.3 upgrade.**

## Why this order

The harness is what gives Claude Code a closed loop: run Unity headless → produce labelled PNGs and pass/fail on disk → read them back → iterate → repeat, with no human in the middle. Building it *before* the upgrade means you capture a **golden baseline of the known-good build**, and upgrade verification becomes a pixel diff rather than you clicking through 40 checklist items by hand.

It lives entirely in a test assembly. No gameplay code is touched. `v0.1-prepolish` is behind you.

## Three rules that make or break this

1. **`-batchmode` WITHOUT `-nographics`.** `-nographics` disables the render loop entirely and every screenshot comes back black. This is the single most common way this setup fails.
2. **The Unity Editor must be CLOSED when the harness runs.** The project lock is exclusive. This rule prevents more problems than anything else — it goes in `CLAUDE.md`.
3. **Every assertion must be proven to FAIL against a deliberately broken input before it is trusted.** Your own session already demonstrated why: the first verification harness reported clean on deliberately broken control files. A green suite that can't go red is worse than no suite, because you stop looking.

---

## THE PROMPT

```
Build a headless capture-and-assertion harness for the Hapi's Havoc Unity
project. Read CLAUDE.md and docs/ARCHITECTURE.md first.

Work on a branch: chore/test-harness
Do NOT modify anything in Assets/_Project/Scripts except where STEP 7
explicitly requires a test hook, and flag each one to me before making it.

Unity is at /Applications/Unity/Hub/Editor/6000.2.0b8/Unity.app/Contents/MacOS/Unity
(verify this path before relying on it — check what Unity Hub actually has
installed and tell me if it differs).

===============================================================================
STEP 1 — Test assembly scaffolding
===============================================================================
Create Assets/Tests/PlayMode/ with an assembly definition referencing the game
assembly, UnityEngine.TestRunner, UnityEditor.TestRunner and the URP assemblies.
Editor-only helpers go in Assets/Tests/Editor/ with their own asmdef.

Confirm com.unity.test-framework (1.5.1) is present and PlayMode tests are
enabled in the Test Runner settings.

===============================================================================
STEP 2 — Determinism first. This is the foundation; get it right before
         writing a single assertion.
===============================================================================
If captures are not byte-stable across two identical runs, every diff is noise
and the whole harness is worthless. Build a DeterministicContext helper that,
for the duration of a test:

  - Renders through a fixed-size RenderTexture (1080x1920) rather than the
    screen backbuffer, so batchmode window size is irrelevant.
  - Pins the camera to an explicit position/rotation/FOV, bypassing
    UniversalCameraController entirely. Do not let live camera state leak in.
  - Seeds GridManager's generation seed explicitly (the project already
    supports seeded generation — use it).
  - Sets Time.captureDeltaTime so animation advances by fixed steps, and
    disables or phase-locks the boat's idle bob (BoatController.isBobbing)
    and any tween that would otherwise be mid-flight at capture time.
  - Disables FPSCounter and any debug UI overlay.
  - Waits in WALL-CLOCK time (WaitForSecondsRealtime), never frame counts.
    Batchmode runs uncapped; frame-count waits either complete instantly or
    never settle.

ACCEPTANCE GATE: run the same capture twice in two separate Unity invocations
and assert the PNGs are pixel-identical. Do not proceed to STEP 3 until this
passes. Report the result to me explicitly.

===============================================================================
STEP 3 — CaptureRig
===============================================================================
A static helper that saves a labelled PNG to TestOutput/Captures/<suite>/<name>.png
(gitignored except the golden set, see STEP 6).

  Capture(string label)                 - full framed board
  CaptureTile(TileInstance t, string)   - single tile isolated, fixed angle
  CaptureRegion(Rect, string)           - sub-area, for seam sampling

Filenames must be deterministic and self-describing — theme, tile type,
state, mode. Claude Code reads these back, so the name must carry meaning
without opening the file.

Also add a PixelUtil helper: load PNG to Color32[], fraction of pixels within
tolerance of a colour, fraction differing between two images, mean/max delta,
and a WCAG-style contrast ratio between two sampled colours.

===============================================================================
STEP 4 — SceneFixture
===============================================================================
Load LevelEditor.unity in a chosen mode (Editor / Playing / Endless) with a
chosen level, driving LevelSelectManager.LevelToLoad as the real game does.
Wait until GameManager.currentState == Playing and the grid is fully populated,
with a wall-clock timeout that fails loudly rather than hanging.

Verify it works for all three modes and all seven levels in
Assets/Resources/Levels before continuing.

===============================================================================
STEP 5 — The assertion suite
===============================================================================
Write all of these. Where one depends on a system that does not exist yet,
write it fully and mark it [Ignore("blocked on ThemeDefinition")] so the hook
is ready rather than forgotten.

VISUAL
  V1 HighlightClearsCompletely
     Capture tile -> highlight -> clear -> capture. Assert <=0.5% of pixels
     differ from the original.
     *** This is the highest-value test in the suite. It is an automated
     tripwire for the bug class that has recurred through this project's
     entire history: banks staying cyan, risk R2, the reason BoatController
     is on v03. Get this one right. ***

  V2 HighlightIsVisible
     Same capture pair, assert >=5% of pixels DID change when highlighted.
     Guards the opposite failure (a highlight that silently does nothing).

  V3 PathRendersOnEveryTileType
     For each of the 13 library tiles, capture isolated, assert the fraction
     of path-coloured pixels falls inside a sane band. Catches paths that
     fail to render and paths rendering absurdly thick.

  V4 PathContinuityAcrossSeam
     Two connected tiles. Sample a vertical line at the join; assert path
     pixels are present on both sides at matching heights. Catches
     snap-point and rotation-mirroring bugs visually.

  V5 ThemeContrastRatio          [Ignore - blocked on ThemeDefinition]
     Contrast between path colour and tile base above a threshold, per theme.
     This is what stops a grey-on-grey Viking theme shipping unreadable.

  V6 PaletteConformance          [Ignore - blocked on ThemeDefinition]
     Every pixel on a themed tile within the declared palette + tolerance.

  V7 ReversedTileDistinctFromBlocker
     Capture a reversed (vortex) tile and a hard blocker (rock); assert they
     differ by >20% of pixels. These two have been visually confused before.

  V8 GoldenImagePerLevel
     Capture each of the 7 levels at load; compare against a committed golden
     PNG with a small perceptual tolerance. This is the upgrade diff.

LOGIC (same harness, no pixels)
  L1 PushParity
     Drive all three PushRowCoroutine overloads with identical input; assert
     identical resulting grid state including rotation and flip flags.
     Risk R1 says these three copies have already diverged in seven ways
     including a Z-flip vs X-flip rotation bug. This test should FAIL on the
     current codebase. If it passes, you have written it wrong — tell me.

  L2 BoatStateSyncAfterPush
     After a row push, assert currentTile/currentSnapPoint match the reverse
     transform lookup and that ResynchronizeStateWithTransform logs no desync.

  L3 LevelJsonRoundTrip
     Load all 7 levels, save, reload, assert deep equality. Guards the schema
     against every future change.

  L4 NoLineRendererLeak
     Flip a tile 20 times; assert the LineRenderer count under it is constant.
     Risk R3 says reversed tiles are initialised with six connections where
     three are expected, orphaning renderers CleanUpPaths can never destroy.
     This should also FAIL on the current codebase.

  L5 UndoRestoresExactState
     Snapshot, act, undo, assert full state equality.

  L6 DrawCallCeiling
     Assert UnityStats.drawCalls stays under a threshold on a 6x6 board.
     Record today's real number first and set the threshold just above it.

===============================================================================
STEP 6 — Golden baseline
===============================================================================
Once the suite is green (except L1 and L4, which are expected to fail — see
below), capture the golden set from the CURRENT build and commit it to
Assets/Tests/Golden/. This is the known-good reference the Unity 6.3 upgrade
will be diffed against.

Gitignore TestOutput/ but NOT Assets/Tests/Golden/.

===============================================================================
STEP 7 — Test hooks in gameplay code
===============================================================================
You will probably need a small number of internal accessors or [InternalsVisibleTo]
entries to reach private state. Before adding ANY of them:
  - list each one, what it exposes, and why the test cannot work without it
  - stop and ask me

House rule 1 says do not grow the four god-object files. Prefer
InternalsVisibleTo over adding public API to them.

===============================================================================
STEP 8 — Meta-tests: prove the suite can go red
===============================================================================
Non-negotiable. Your own earlier harness in this project reported clean on
deliberately broken control files; that is exactly what this step exists to
prevent.

Create Assets/Tests/BrokenControls/ containing deliberately broken fixtures,
and a MetaTests suite asserting the corresponding assertion FAILS on each:

  X1  Tile prefab with the path material stripped        -> V3 must fail
  X2  Theme with near-identical water and stone colours  -> V5 must fail
  X3  A highlight path that mutates renderer.material
      directly and never restores it                     -> V1 must fail
  X4  A golden image with a deliberate 10% region change -> V8 must fail
  X5  A push overload with a deliberately wrong rotation -> L1 must fail

Report the meta-test results explicitly. If any meta-test does NOT produce the
expected failure, the corresponding assertion is broken — fix the assertion,
not the control.

===============================================================================
STEP 9 — Runner script and human bridge
===============================================================================
9a. tools/run-tests.sh:
    Unity -batchmode -runTests -testPlatform PlayMode
          -projectPath <repo> -testResults TestOutput/results.xml
          -logFile TestOutput/unity.log
    NOTE: -batchmode WITHOUT -nographics. -nographics kills the render loop
    and every capture comes back black.
    Script must fail loudly if the Editor holds the project lock, with a clear
    message telling me to close Unity.

9b. An editor menu item "Hapi's Havoc/Capture Screenshot" that saves the Game
    view to TestOutput/Captures/manual/. When I see something wrong while
    playing, I press this and you read exactly what I saw.

===============================================================================
STEP 10 — Update CLAUDE.md
===============================================================================
Append a "Testing" section with these standing rules:
  - The Unity Editor MUST be closed before running the harness (exclusive lock).
  - Run the suite before every commit.
  - After running, READ YOUR OWN CAPTURES before presenting results. Iterate at
    least once on what you see. Report what you changed after looking.
  - Any new assertion must ship with a matching meta-test proving it can fail.
  - -batchmode without -nographics, always.

===============================================================================
REPORT BACK
===============================================================================
  - Determinism gate result (STEP 2) — the two-run pixel-identity check
  - Which assertions pass, which fail, and for L1/L4 whether the failure
    matches the bug predicted in the risk register
  - Meta-test results, one line each
  - Real draw-call count on a 6x6 board
  - Every test hook you want in gameplay code, before adding it
  - Anything you could not verify, stated plainly rather than assumed
```

---

## What "success" looks like

Counter-intuitively, **a fully green suite means you built it wrong.** Two tests are expected to fail on the current codebase:

- **L1 PushParity** should fail on the Z-flip vs X-flip divergence between the three `PushRowCoroutine` copies (risk R1).
- **L4 NoLineRendererLeak** should fail on the six-connection reversed-tile initialisation (risk R3).

Those two failures are the harness proving it can see real bugs that are genuinely there right now. If they pass, the tests aren't reaching what they claim to reach.

Don't fix either yet — they're the first work item for the cleanup job, and having them red gives you a way to prove the fix actually landed.

---

## Then C2 and C3

**C2 — Unity 6.3 upgrade.** Branch, upgrade, let reimport finish, fix compile errors, then run the harness and diff against the golden set. The manual checklist in `CLAUDE_CODE_JOB_02` drops to whatever the harness can't reach: input feel, camera pan inertia, drag-and-drop, and a fresh PlayerPrefs cycle (play a level, earn stars, quit, relaunch — old saves are gone by design after the bundle ID change, so test a new cycle rather than looking for old data).

**C3 — Unity MCP.** Set up after the upgrade for what the harness genuinely can't do: exploring live scene state, wiring the missing `LevelMarker` for level 1-7 in `LevelSelect.unity`, and interactive debugging. By then it's a supplement, not the primary loop.

