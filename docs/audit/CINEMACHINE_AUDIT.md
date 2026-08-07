# Cinemachine audit — LevelEditor.unity

Read from the scene YAML and the source, not inferred.

## 1. Every virtual camera

| GameObject | position | pitch | OrthographicSize | Follow | LookAt | extensions |
|---|---|---|---|---|---|---|
| `VCam_Editor`  | (0, 10, −2) | 75° | **5** | none | none | none |
| `VCam_Player`  | (0, 9, −4)  | 70° | **5** | none | none | none |
| `VCam_Endless` | (0, 12, 0)  | 70° | **5** | none | none | none |

Serialised `Priority` is 0 on all three; `CameraManager` sets 20/10 at runtime. No damping, no
noise, no extension components on any of them.

**All three are static poses with no targets.** Nothing follows, nothing looks at anything. They
are three hardcoded transforms behind a priority switch — none of what Cinemachine exists to do.
All three also carry an *identical* lens, so they do not even differentiate zoom.

## 2. Which vCam is active, and where 5.00 comes from

`CameraManager` switches by priority only: `SwitchToEditorView` / `SwitchToPlayerView` /
`SwitchToEndlessView` set the chosen vCam to 20 and the other two to 10. Callers are
`GameManager:434` (Editor), `LevelEditorManager:2205,2328` (Player) and `EndlessModeManager:222,
1021,1146` (Endless).

**Playing mode activates `VCam_Player` at (0, 9, −4) with OrthographicSize 5 — exactly the pose
C7 reads from the live camera.** That closes ITEM 2 from the previous brief: the scene edit to the
`Camera` component *did* persist (`orthographic: 1`, `orthographic size: 7.5` in YAML, one camera
in the scene), but the Brain overwrites the Camera's lens from the active vCam every frame. The
hand-set 7.5 is dead state.

## 3. The Brain

On `Main Camera`. `UpdateMethod: 2` (LateUpdate), `BlendUpdateMethod: 1`,
`DefaultBlend { Style: 1 (EaseInOut), Time: 2 }`, no custom blends.

**A 2-second EaseInOut blend is very perceptible** — and the project notes describe camera
transitions as instant. Either the notes are wrong, or vCam priority never actually changes during
normal play (mode switches happen at load). Not resolved here; it matters because "we would lose
the blends" is the main argument for keeping Cinemachine, and the blend may be both unwanted and
unseen.

Each frame the Brain overwrites the Camera's **transform and lens** from the active vCam. That is
why `BoardFramingDriver` writing `Camera.main` had no effect.

## 4. What else depends on Cinemachine

| Thing | Depends? | Breaks if removed from the BOARD scene? |
|---|---|---|
| `LevelSelect.unity` + `LevelSelectCameraController` | **Yes** — 1 Brain, 1 vCam, and the controller references Cinemachine | **No** — different scene, untouched |
| `MainMenu.unity` | No — 0 brains, 0 vCams | No |
| `EndlessModeManager.LateUpdate` | **Yes** — holds an `endlessVCam` field and drives `cameraProxy` | **Yes** — this is the one real dependency |
| `UniversalCameraController.cameraProxy` | Writes a proxy Transform | See below |

**`CameraProxy` is a plain GameObject at the origin. It is not a vCam, and no vCam Follows it**
(Follow is 0 on all three). So in **Playing and Editor, UCC's pan moves a transform that nothing
reads** — the pan appears to be inert in those modes. Only Endless consumes it, via
`EndlessModeManager.LateUpdate` reading `GetEndlessCameraOffset()` and writing
`cameraProxy.position`.

**Not verified:** how `VCam_Endless` actually receives the proxy position, given `Follow` is 0.
Either it is parented under `CameraProxy` or `EndlessModeManager` moves the vCam transform
directly. This matters for any removal plan and I did not confirm it.

## 5. What would actually be lost

Concretely: the 2-second blend between three *static* poses, and a priority-based way of choosing
among three transforms. That is all. No follow behaviour, no damping, no noise, no composition,
no dead zones — none of it is configured.

## Recommendation: REMOVE Cinemachine from the board scene

The three vCams are doing nothing Cinemachine is for. They are static poses with an identical
lens, selected by priority — which is a `switch` statement wearing a package.

Four arguments, in order of weight:

1. **The harness and the game disagree by construction.** `DeterministicContext` disables the
   Brain, so *every capture ever taken* has run with Cinemachine off. Removing it makes harness
   and game converge. **Driving the vCams does not** — the harness would still disable the Brain,
   and the divergence would remain permanently.

2. **Four owners of one transform produced two wrong diagnoses in two sessions.** Three vCams, the
   Brain, UCC and now the framing driver all have an opinion about `Main Camera`. I blamed UCC;
   it was the Brain. Before that I blamed the push path; it was the reverse lookup. Reducing the
   owners to one is the structural fix.

3. **The framing work cannot land otherwise.** `BoardFraming` computes an orthographic *size* per
   level. Feeding UCC's proxy addresses position only; the lens comes from the vCam. Driving vCams
   would work but keeps (1) and (2).

4. **Nothing configured is being used.** Removal costs a blend that is probably neither wanted
   nor visible.

### What replaces it

- **Resting pose: `BoardFramingDriver`.** Writes `Camera.main` transform + `orthographicSize`
  directly from `BoardFraming.Fit`, on level load and on `OnTileSpawned` with hysteresis.
- **Pan: `UniversalCameraController`,** re-pointed to apply its accumulated offset *relative to*
  the driver's resting pose rather than to a proxy nothing reads. Its `returnToOrigin` then means
  "drift back to the framed pose", which is what was wanted all along.
- **Mode switching:** delete `CameraManager`'s priority juggling. A mode change becomes
  "recompute the resting pose with that mode's `BoardLayout`" — which the driver already does,
  since it selects the layout by `OperatingMode`. The three static vCam poses become one computed
  pose, which is strictly better: they were fixed and could not fit different board shapes, which
  is the original bug.
- **Endless keeps its follow behaviour**, but reads the driver's resting pose as its base instead
  of a vCam. This is the only part with real work in it, and the `endlessVCam` question above must
  be answered first.

**Scope warning:** this moves every golden again, and it touches input-adjacent code in Endless.
It should be its own job with its own approval, not a rider on a framing fix.
