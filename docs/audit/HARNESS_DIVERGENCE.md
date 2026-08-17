# Harness / game divergence audit

Everything `DeterministicContext` (and other harness code) disables, hides, pins, overrides or
substitutes, ranked by whether it could conceal a real defect.

Three bugs have already hidden here — the hand palette, `BoardFraming` being harness-only, and
`CinemachineBrain`. Each suppression was individually reasonable.

**Nine divergences are now on record.** #1–#5 are in the tables below. Written up in full after
them:

- **#6** — tile scale is forced, which hid the framing readiness problem.
- **#7** — game time runs ~15× slower than real in batchmode, plus **7b**'s rule that animated
  effects must have their phase SET, not waited for.
- **#8** — the reporter truncated its own output.
- **#9** — batchmode's screen is landscape, so the live game never frames portrait.

They share one shape: **the instrument concealing a difference between what it measured and what it
showed.** Not all of them are suppressions — #7 is a property of the environment, #8 was a defect in
the reporting step, #9 is a property of the batchmode window — which is why this document is broader
than the `X18` allowlist.

> **Careful with the numbering.** The tables below number *suppressions* 1–15. The `#N` divergences
> discussed in prose are a separate, older sequence and the two do not line up: suppression 11 is
> the tile-scale forcing that prose calls divergence #6. Read the prose headings, not the table
> indices, when a commit message cites a `#N`.

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

---

## Divergence #6 — TILE SCALE IS FORCED, AND IT HID THE READINESS PROBLEM

`DeterministicContext` writes `localScale = Vector3.one` on every `BoardTile` (suppression 11).
Tiles arrive via a `ScaleIn` animation, so a tile caught mid-spawn renders at a fraction of size
and a capture of it would be non-deterministic. Individually reasonable, like all the others.

**What it hid.** A tile stuck mid-`ScaleIn`, or never finishing one, renders perfectly in every
capture. That is not hypothetical: the very same fact defeated `BoardFramingDriver`'s first
readiness gate. The grid reported 9 of 9 tiles spawned while the framing bounds were still
degenerate at `(2.10, 0.00, 0.00)`, because most tile renderers were mid-ramp and reported
**exactly zero** bounds, so the filter that drops degenerate renderers discarded them — and the
four that survived were about a twelfth of full size. The harness could never have shown this,
because the harness forces the scale the driver was waiting for.

**Why it stays.** Determinism genuinely requires it: the alternative is capturing a frame of an
animation whose phase depends on wall-clock timing. But it is now recorded as a suppression that
has cost real debugging time, not as an obviously safe one.

**What detects drift.** `BoardFramingDriver.BoardIsReady` gates on tile scale in the LIVE game,
where nothing is forced, and names the offending tile and its scale on timeout. That is the check
the harness cannot perform. `C7` asserts the driver's result matches `BoardFraming`, so a board
framed against unfinished tiles shows up as a pose mismatch rather than as a silently wrong image.

---

## Divergence #7 — GAME TIME RUNS ~15× SLOWER THAN REAL TIME IN BATCHMODE

**An environment property, not a suppression.** Nothing disables anything here; the harness simply
runs in a place where two clocks that agree on a device disagree badly. It belongs in this document
because it has exactly the effect a suppression has: it makes the harness's reading of the game
differ from the game's own, silently.

Measured: batchmode renders frames at roughly **1 ms** each while game time advances about **15×
slower than real time**. So a frame count means nothing, and the two clocks are not
interchangeable in either direction.

**Three defects it has already caused:**

1. **A blend misread as frozen.** Logging `Time.time` at frame granularity showed the same value
   twice and was reported as a stopped clock. It was advancing 0.0008 per frame — a 2-second blend
   moving 0.0008 prints as `0.02` twice at `F2`. A precision artifact read as a mechanism.
2. **`study_3x3` framed against a board that had not finished arriving.** A five-consecutive-frame
   stability gate was satisfied in about **four milliseconds** of batchmode time, before the banks
   had spawned. Larger boards passed only because tile spawning burned enough frames for the banks
   to land first — luck, not correctness.
3. **`X17`'s wait assumptions silently invalidated.** Waits written against one clock were read
   against the other, and the control stopped breaking what it was written to break.

**The rule this produced**, now in CLAUDE.md: *no gate may be expressed in frames.* Shipping code
gates on **game time**, because the coroutines it waits for run on game time. Harness waits use the
**wall clock**, because a hang must fail loudly rather than block forever. `BoardFramingDriver`'s
readiness gate is the worked example: bounds must be unchanged for 0.25 s of **game** time, with a
15 s **wall-clock** timeout as the backstop.

### 7b — ANIMATED EFFECTS MUST HAVE THEIR PHASE SET, NOT WAITED FOR

Recorded now, before 7b is designed, because the obvious approach is the wrong one.

Suppression 6 pins `Shader._Time` to 0 so time-driven shaders capture deterministically. The
flow-scrolling channel geometry 7b introduces is exactly such an effect, so under the current
harness it captures **frozen** — and a frozen capture cannot distinguish "the effect is correct"
from "the effect never ran".

**Do not wait for a phase.** Waiting couples the result to whichever clock the wait used, which is
divergence #7 all over again, and it cannot be deterministic because the phase reached depends on
timing.

**Set the phase instead.** Write `_Time` to chosen values, capture a **matrix** of phases, and
assert the captures **DIFFER between phases**. That gives three things a single frozen capture
cannot:

- an animated effect that silently stopped animating fails, because two phases would be identical;
- every capture stays byte-reproducible, because the phase is an input rather than an outcome;
- the assertion is about the effect being alive, not about one arbitrary instant looking right.

The existing goldens stay at phase 0, which is just one row of that matrix.

---

## Divergence #8 — THE REPORTER TRUNCATED ITS OWN OUTPUT

`tools/run-tests.sh` capped every failure message at **6 lines**, with no indication it had done
so — `msg.text.strip().splitlines()[:6]`.

**What it concealed.** `V8` had **seven** stale goldens and printed **five**. `C2`'s list of
out-of-rect elements was cut mid-list. A shorter failure reads as a smaller problem, and it very
nearly went into a report as "five of seven levels moved" when all seven had. It was caught only
because the goldens were re-measured independently rather than trusted from the summary.

**Same family as the other seven.** Every divergence in this document is the instrument concealing
a difference between what it measured and what it showed. This one is the purest case: the measurement
was completely correct, fully present in `results.xml`, and thrown away at the last step before a
human saw it.

**Fixed.** The cap is 60 and truncation now announces itself:

```
... N MORE LINE(S) NOT SHOWN - see TestOutput/results.xml
```

**The general rule:** a harness may summarise, but it may never silently drop. Any elision must say
that it happened and where the full record is. A cap that announces itself is a formatting choice;
a cap that does not is a defect that scales with how bad the news is.

---

## Divergence #9 — BATCHMODE'S SCREEN IS LANDSCAPE, SO THE LIVE GAME NEVER FRAMES PORTRAIT

Found by `W1`, the warts-and-all capture, on its first run — and it first presented as a false
alarm, which is the more useful half of the story.

Measured: `Screen.width x Screen.height` in batchmode is **640x480**. `BoardFramingDriver` picks
its layout from the screen (`OrientationFor(mode, Screen.width, Screen.height)`), so in every
harness run the live driver frames for **Landscape**. The player's Portrait branch of `BoardLayout`
is never exercised by the live path at all.

**The false alarm.** W1 first captured that landscape-framed camera into a portrait 1080x1920
RenderTexture. Every board came out oversized and cropped at the left edge, and it read exactly
like a live framing defect — a serious one, since the goldens all framed correctly. It was not a
defect: the camera had correctly framed a landscape rect and the capture reinterpreted it as
portrait. `C7` was green at delta 0.000 throughout, on all ten levels, because the driver was
faithfully applying what `BoardFraming` computed.

Two lessons worth keeping:

- **`C7` cannot catch a wrong layout.** It compares the runtime camera against `BoardFraming` — the
  same function that posed it. If both use the wrong rect they agree perfectly. C7 proves the
  driver applies the framing; it cannot prove the framing is right. That is what a human looking at
  W1's images is for.
- **A capture that reinterprets the aspect is not a capture of the thing.** W1 now sizes its
  RenderTexture from the live screen's shape, so the image is a faithful picture of what the live
  camera framed.

**What is still not covered.** Portrait framing in the live path. The deterministic captures cover
portrait by pinning the layout as an INPUT with the driver suppressed; W1 covers "nothing
suppressed" but only at the batchmode aspect. Neither covers both at once.

**The fix direction**, not done here: give the harness a portrait screen so the live driver picks
Portrait, rather than teaching the capture to compensate. Compensating in the capture is how this
divergence became invisible in the first place.

---

## The X18 allowlist

`DeterministicContext.Suppressions` is the declared list and `X18` fails if it drifts from the
table above. Divergences #7 and #8 add **no** entries: #7 is a property of the batchmode
environment and #8 was a defect in the reporter, neither of which suppresses anything the game
runs. #6 was already declared as `TileLocalScale`. The allowlist is therefore unchanged at 15
entries, and X18 is green.
