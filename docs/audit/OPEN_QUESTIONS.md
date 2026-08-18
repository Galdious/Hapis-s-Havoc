# Open questions and logged requests

Things known, reproducible, and deliberately not resolved — recorded so the next person starts
here rather than rediscovering them.

---

## OQ-1 — The 9-pixel residual on `01_01_BasicMoves`

**Status:** open, not blocking. `V8` passes.

After the harness screen was switched to portrait (divergence #9), `01_01_BasicMoves` began
differing from its golden by **9 pixels — 0.0004 %, max 21**, a single 5×4 antialiased cluster on
the boat's red edge at viewport **(0.50, 0.386)**. Every other level is byte-identical at 0.0000 %.

**It is not noise.** The capture is byte-identical across separate processes and across repeated
runs; the difference reproduces exactly every time.

**Three explanations were tested and refuted by measurement:**

1. *The first level loaded gets an extra frame from the portrait switch.* Made the yield
   unconditional so every level got one. `01_01` was unchanged — and `V9` moved by 30.37 %, so the
   experiment was reverted.
2. *Every level should be treated identically.* Same experiment, same result.
3. *The resolution change lands during the first scene load.* Moved the switch into a
   `[SetUpFixture]` that runs before any test loads a scene. `01_01` unchanged.

**The golden was deliberately NOT re-baked** to absorb it. Re-baking would make the question
unanswerable while changing nothing about the game.

**Where to start:** the cluster is on the boat, which is the one object whose resting height, idle
bob and selection lift are all time-dependent, and `01_01` is the only level where the boat starts
docked at a bank. `DeterministicContext` pins the boat with `StopAllCoroutines` + `PlaceOnTile`
(suppression 9); the sub-pixel Y it settles at is the obvious suspect.

---

## OQ-2 — Editor camera wants pan inertia and zoom

**Status:** feature request from playtesting. Its own job; not a defect.

Pawel's report on Editor drag: *acceptable; wants inertia and zoom.*

Current state, for whoever picks this up:

- `UniversalCameraController.editorSettings` is `panMode = Free, panSpeed = 2, friction = 8`,
  `returnToOrigin = false`. Verified by `E2`: the Editor pans freely and does not spring back, which
  is correct and must stay.
- **Inertia already exists in code** — `ApplyStandardMovement` applies `velocity` with a friction
  lerp when the drag ends — but the Editor's `friction = 8` is more than twice Play's `3`, so it
  kills the glide almost immediately. Try the friction value before writing anything new.
- **Zoom does not exist in any mode.** Framing owns orthographic size (`BoardFraming` computes it,
  `BoardFramingDriver` writes it to the vCam lens), so a zoom control has to negotiate with that
  rather than write `orthographicSize` directly — otherwise it fights the driver the way
  `EndlessModeManager` fought it over the vCam transform.

Anything added here must keep `E2` green.

---

## OQ-3 — The harness cannot push a row

**Status:** known limit of `GameDriver`.

`GameDriver` drives real moves through `BoatController.OnTileClicked`, which is enough to spend AP
and end an Endless turn. It **cannot push a row from the hand**, which is what a real player does
when the boat has no valid moves.

Consequence: a stranded boat ends the drive rather than the turn. Measured — a forward-most walk
dead-ended after one move on a generated river. `GameDriver.PlayUntilTurnEnds` therefore defaults to
`preferForward: false` and takes whatever move is available, because AP is what ends a turn and
direction is not.

Closing this would unlock capturing the push animation, which no capture has ever shown.
