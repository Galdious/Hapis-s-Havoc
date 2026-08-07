# Harness / game divergence audit

Everything `DeterministicContext` (and other harness code) disables, hides, pins, overrides or
substitutes, ranked by whether it could conceal a real defect.

Three bugs have already hidden here — the hand palette, `BoardFraming` being harness-only, and
`CinemachineBrain`. Each suppression was individually reasonable.

## CRITICAL — has concealed, or is concealing, a real defect

| # | Suppression | What it changes | Absent in play? | What hides behind it |
|---|---|---|---|---|
| 1 | `DisableByTypeName("CinemachineBrain")` | Brain stops driving the camera transform **and lens** | **No** — Brain is live in play | **PROVEN.** The camera in every capture is the harness's; the live camera is a vCam. Framing never applied and nobody noticed for the whole project. |
| 2 | `HideHandPalettes()` | Deactivates `playerHandContainer` / `editorHandContainer` | **No** — the hand is visible in play | **PROVEN.** Hand tiles carry `TileInstance`, entered the framing bounds, and made Editor width bind at 2.54×. Invisible in every golden. |
| 3 | `DisableAll<EndlessModeManager>()` | Endless streaming, camera follow, storm pushes all stop | **No** — it *is* Endless mode | **FOURTH DIVERGENCE.** Every "Endless" capture is a static board with the mode's own manager switched off. Endless streaming visuals, camera follow and row spawning have never been captured truthfully. B1 reports an "Endless (streamed)" row measured with the streamer disabled. |
| 4 | `QualitySettings.SetQualityLevel("PC")` | Pins quality by name | **No** — the game ships **Mobile**, whose renderScale is **0.8** vs PC's 1.0 | **FIFTH DIVERGENCE.** Every golden is rendered at PC quality. Any defect that only appears at renderScale 0.8 — aliasing, thin-line dropout, exactly the path-legibility question the shape study asked — cannot appear in a golden. The code comment names the 0.8/1.0 difference and then pins PC anyway. |

## HIGH — could conceal a defect in an area under active work

| # | Suppression | What it changes | Absent in play? | What hides behind it |
|---|---|---|---|---|
| 5 | `GridManager.StopAllCoroutines()` | Kills tile slide, pop-in, ejection | No | Push animation defects. The push path is the most bug-prone code here (gotcha 7) and its motion is never captured. |
| 6 | `Shader.SetGlobalVector("_Time", 0)` | Freezes shader time | No | Any `_Time`-driven effect. **Directly relevant to 7b**, whose flow-scrolling is time-driven and would capture as frozen. |
| 7 | `DisableAll<BoardFramingDriver>()` | Framing driver stops | No | Added by me this session. A driver that mis-frames cannot show up in a golden. Necessary today; must be removed once the driver *is* the framing. |
| 8 | `DisableAll<UniversalCameraController>()` | Pan stops | No | Pan defects. Currently low-impact only because UCC's proxy is unread in Playing/Editor — but that inertness is itself a bug this conceals. |

## MEDIUM

| # | Suppression | What it changes | What hides behind it |
|---|---|---|---|
| 9 | `boat.StopAllCoroutines()` + `isSelected = false` + `PlaceOnTile` | Pins the boat | Boat animation, bob, lift and resting-height defects |
| 10 | `PathVisualizer.StopAllCoroutines()` | Kills colour fades | Path highlight fade defects |
| 11 | `localScale = Vector3.one` on every `BoardTile` | Forces scale | A tile stuck mid-`ScaleIn` renders correct in every capture |
| 12 | `Camera.targetTexture = 1080×1920 RT` | Substitutes the render target | Real device aspects differ; nothing is captured at a real one |

## LOW — genuinely absent, or deliberate and understood

| # | Suppression | Why it is fine |
|---|---|---|
| 13 | `DisableAll<FPSCounter>()` | A debug overlay; genuinely not wanted in a board capture |
| 14 | `Random.InitState(seed)` | Determinism is the point; D3 asserts the seed is honoured |
| 15 | `Time.captureDeltaTime = 1/60` | Pins the step; captures are explicit `Camera.Render()` anyway |

## Fourth divergence: yes — two of them

**#3, `EndlessModeManager` disabled**, is the clearest: an entire operating mode is captured with
its own manager switched off, so nothing Endless-specific has ever been seen by a golden.

**#4, quality pinned to PC while the game ships Mobile**, is arguably worse because it is silent
and global. renderScale 0.8 versus 1.0 changes every pixel, and the thin-line legibility question
the shape study just spent a session on is exactly the kind of defect it would mask.

Neither is chased here — this is an audit.

---

## RESOLVED: quality pinned to PC (divergence #4)

Fixed. `DeterministicContext.PinnedQualityLevel` is now **`Mobile`**, the shipping level, whose
`Mobile_RPAsset` carries `m_RenderScale: 0.8` against PC's 1.0.

**The hardened pin did its job.** Switching the constant threw rather than falling through:

```
Quality level 'Mobile' is not available for build target 'StandaloneOSX'. Available: [PC]
```

Cause: `QualitySettings.asset` marked Mobile `excludedTargetPlatforms: [Standalone]`, and the
Editor's active build target is StandaloneOSX. **ProjectSettings was edited** to clear that
exclusion, so the shipping quality level is selectable in-Editor. This cannot affect a mobile
build — `excludedTargetPlatforms` only controls per-platform availability.

Every golden and every study capture before this rendered at renderScale 1.0.

---

## Divergence #3 (`EndlessModeManager` disabled) — ATTEMPTED, NOT RESOLVED

Not documented as permanent. It is **not** proven impossible; the attempt failed for a reason I
understand and can name, and the next attempt should start from here rather than from scratch.

### What actually makes Endless non-deterministic

Two sources, found in source rather than assumed:

1. **The turn loop.** `EndlessGameLoop()` is a `while (true)` coroutine that pushes rows and
   drains stamina, so the board mutates with elapsed time. Stoppable, exactly as the boat,
   PathVisualizer and GridManager coroutines already are.
2. **The row count.** `GenerateMissingRows(boatY + leadingBuffer)` grows the board from the boat's
   position. `Random.InitState` is already pinned, but seeding alone is insufficient: the RNG call
   **sequence** depends on how many rows are generated, so the count must be fixed too.

A third source exists but does **not** affect captures: the camera lerp at
`EndlessModeManager:179` is frame-count dependent, but it moves only `cameraProxy` and
`endlessVCam`, neither of which is the capture camera while the Brain is suppressed. It *will*
matter once divergence #1 is closed.

### Why the attempt failed

Making `GenerateMissingRows` internal and calling it from `Quiesce()` broke **D3** as well as D4.

**`LevelEditor.unity` hosts all three modes.** `EndlessModeManager` is present in the scene
regardless of the mode being played, so a `Quiesce()` that pins the Endless row count ran in
**Playing and Editor captures too** — growing the board and consuming RNG in modes whose boards
are authored and fixed. That is what D3 caught.

### What the next attempt should do differently

- **Scope the pin to Endless mode**, not to every capture.
- **Pin the row count ONCE at scene setup** (`SceneFixture`), not per-capture. Two contexts in one
  test each calling `GenerateMissingRows` do different amounts of work — the first grows the
  board, the second finds it grown — which is its own source of divergence between captures.
- Only then re-enable the manager and un-ignore **D4**, which is parked rather than deleted and is
  the proof obligation.

`GenerateMissingRows` has been left `internal` (visibility only, no behaviour change), so the next
attempt does not have to redo that.
