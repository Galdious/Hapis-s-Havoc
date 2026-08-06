# Golden image baseline

Committed reference images for `V8_GoldenImagePerLevel`. Regenerated only under the rules at
the bottom of this file.

## Capture conditions

| | |
|---|---|
| Unity editor | **6000.3.21f1** (LTS) |
| Render pipeline | **URP 17.3.0** |
| Quality level | `PC` (pinned by name; `DeterministicContext` throws if unavailable) |
| Build target | `StandaloneOSX` |
| Colour space | Linear |
| RenderTexture | 1080×1920, sRGB |
| Captured from | `chore/assertion-suite`, forked from tag **`v0.2-unity63`** (`8378bba`) |

Every `.png` has a matching `.conditions.txt` recording these values as measured at capture
time. **A golden without its conditions sidecar is not reproducible** — keep them together.

Captures render through an explicit synchronous `Camera.Render()` into a fixed RenderTexture,
never the frame loop, and the camera pose is derived from the grid bounds rather than
hardcoded. The player hand palette is hidden during capture because it is level- and
mode-dependent and would inject false diffs.

### Comparison tolerance

`4/255` per pixel, `0.5%` of pixels allowed to exceed it.

Not arbitrary. Diffing the same scene rendered under URP 17.5.0 against URP 17.3.0 measured
**mean 0.067/255, max 2/255, 0.00% of pixels differing by more than 4/255** — the observed
noise floor of a real renderer change that looks identical. The tolerance sits just above
that. On a clean re-run all seven levels currently report **0.0000% differing, mean 0.000,
max 0**.

### Not drift

`Boat_Base_Material.mat` and `Tile_Base_Mat.mat` had `m_CustomRenderQueue` re-serialized
`-1 → 3000` during the Unity 6.3 migration. Both use `Lit_ZWrite.shadergraph` with
`_Surface: 1` (Transparent), whose default queue **is** 3000 — semantically identical, merely
now explicit. Recorded here so nobody later mistakes it for a rendering change.

## KNOWN DEFECTS BAKED INTO THIS BASELINE

These images were captured from a build with the following unfixed defects. They are visible
in, or affect, the baseline.

**R1 — push rotation mirroring.** `GridManager.PushRowCoroutine` overload 2 builds
`Quaternion.Euler(0, rotationY, isFlipped ? 180 : 0)` — a **Z-axis** flip, where every other
path flips on **X**. `Rz(180)` inverts local +X and `Rx(180)` does not, so a hand-pushed
reversed tile renders mirrored relative to the same tile placed by the loader.
**Test `L1` is RED on this.**

**R3 — LineRenderer leak on reversed tiles.** `GridManager.InitializeTile` writes six
connections for a reversed tile (`0-2, 2-0, 1-3, 3-1, 4-5, 5-4` — three logical paths written
twice) while `PathVisualizer` registers them under canonical `(min,max)` keys, so only three
land in the dictionary. `CleanUpPaths()` destroys dictionary values only, orphaning **three
`LineRenderer` GameObjects per reversed tile**. Measured growth: exactly **+3 per flip cycle**
(3, 6, 9 … 63 across 20 cycles). **Test `L4` is RED on this.**

**`BankClickHandler` leak.** `BoatController:890` adds the handler to `renderer.gameObject`
— the **child** mesh object — while `ClearHighlights:963` removes it via
`bankGO.GetComponent<BankClickHandler>()` on the **parent**. `GetComponent` does not search
children, so it is never destroyed. The `AddComponent` also sits outside the
already-highlighted guard, so it fires on every qualifying call. Measured accumulation on the
bottom bank across three select/deselect cycles: 1 → 2 → 3, with `onParentOnly = 0` every time,
confirming the parent/child mismatch directly. Scheduled to be fixed alongside R2.

**R2 — highlight material mutation.** `BoatController:885` and four other sites do
`renderer.material.color = X` (silently instantiating a per-renderer material) and restore via
`renderer.sharedMaterial = original` (orphaning that instance). This is the root of the
recurring "highlight stuck on" family — `BoatController` carries a v03 changelog header about
banks staying cyan. **Tests `V1`, `V1b` and `V1c` guard the observable symptom**; they pass
today, so the leak is invisible in these images, but the leaked materials are real.

**Boat state desync after a row push** (CLAUDE.md gotcha 2). A push re-parents and slides the
boat without updating `currentTile` / `currentSnapPoint`; `ResynchronizeStateWithTransform`
corrects it on the next `SelectBoat` and logs `Boat Desync Detected!`.
**Test `L2` is RED on this.**

## Regeneration policy

> **These goldens are a BEFORE baseline. Fixing R1, R2, R3 or the `BankClickHandler` leak is
> EXPECTED to change them. When that happens it is a fix, not a regression.**
>
> The CLAUDE.md rule against regenerating goldens to make a test pass still applies — but
> replacing a golden **IS permitted** when all three of these hold:
>
> 1. the change is traceable to one of the defects listed above,
> 2. a human has compared the before and after side by side, and
> 3. the commit message names which defect caused the change and why the new image is correct.
>
> Blind regeneration remains forbidden. It is the standard failure mode of golden-image
> testing and it launders visual regressions into the baseline.

To regenerate after satisfying the above:

```bash
HAPI_REGENERATE_GOLDENS=1 ./tools/run-tests.sh GoldenImageTests
```

Then re-run without the variable to confirm the new baseline compares clean.
