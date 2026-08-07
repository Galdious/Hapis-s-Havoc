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

**R1 — push rotation divergence. FIXED** in `d66dc80`, at all three sites; `L1` and `M1`
are green. Retained here because it explains the re-baked push golden.
`GridManager.PushRowCoroutine` overload 2 built
`Quaternion.Euler(0, rotationY, isFlipped ? 180 : 0)` — a **Z-axis** flip, where every other
path in the project flips on **X**. Two more sites share the fault:
`LevelEditorManager:766` (the dragged hand tile) and `EndlessModeManager:801` (which recovers
`isFlipped` by reading `eulerAngles.x`, the readback trap of CLAUDE.md gotcha 3).
**Test `L1` is RED on this.**

> **It is not a mirror.** Unity's Euler order is ZXY, so
> `Euler(0, y, 180) = Ry(y)·Rz(180) = Ry(y)·Rx(180)·Ry(180) = Euler(180, y, 0)·Ry(180)`.
> R1 is the correct orientation plus an extra **180° local yaw**. Measured on a pushed tile:
> snap points swap `0↔3`, `1↔2`, `4↔5` and land on the *same six world positions*, relabelled.
> A reversed tile is forced straight along `{0-2, 1-3, 4-5}` — a set that permutation maps onto
> itself — so **the drawn paths are pixel-identical and R1 is gameplay-invisible while the tile
> stays reversed**. It becomes real when the labels are next used: un-flipping the tile, or any
> code that reads `snapPoints[i]` by index. The only pixel signature is the mesh/vortex decal
> not being 180°-yaw symmetric: **0.5364 % for one tile, 1.6414 % for three.**

**`push/post-push_01_06_row2_flipped.png`** — the one golden captured *after* a push rather than
at load. Row 2 of `01_06` filled with three flipped tiles pushed through the hand path. Three
tiles rather than one deliberately: a single tile clears the 0.5 % threshold by only 0.036
points, which is not a margin worth trusting.

> **Re-baked on the fixed code.** It was first captured pre-fix to bake R1 in, then replaced
> once R1 was fixed — the change it recorded was **1.6414 %**, confined to a single contiguous
> band (`y 772..905`, the row-2 tiles), with every other pixel in the frame identical. That
> figure matches the predicted R1-only delta exactly, which is the evidence that the
> `PushRowInternal` consolidation changed nothing else. The pushed tiles now read
> `yaw180=False faceFlipped=True`, matching how they were authored.

**R3 — LineRenderer leak on reversed tiles.** `GridManager.InitializeTile` writes six
connections for a reversed tile (`0-2, 2-0, 1-3, 3-1, 4-5, 5-4` — three logical paths written
twice) while `PathVisualizer` registers them under canonical `(min,max)` keys, so only three
land in the dictionary. `CleanUpPaths()` destroys dictionary values only, orphaning **three
`LineRenderer` GameObjects per reversed tile**. Measured growth: exactly **+3 per flip cycle**
(3, 6, 9 … 63 across 20 cycles). **FIXED in `33d74f9`** — `L4` is green. The images did not move
beyond tolerance, so these captures remain valid.

**`BankClickHandler` leak.** `BoatController:890` adds the handler to `renderer.gameObject`
— the **child** mesh object — while `ClearHighlights:963` removes it via
`bankGO.GetComponent<BankClickHandler>()` on the **parent**. `GetComponent` does not search
children, so it is never destroyed. The `AddComponent` also sits outside the
already-highlighted guard, so it fires on every qualifying call. Measured accumulation on the
bottom bank across three select/deselect cycles: 1 → 2 → 3, with `onParentOnly = 0` every time,
confirming the parent/child mismatch directly. **FIXED in `51c5c35`** — `L8` is green.

**R2 — highlight material mutation.** `BoatController:885` and four other sites do
`renderer.material.color = X` (silently instantiating a per-renderer material) and restore via
`renderer.sharedMaterial = original` (orphaning that instance). This is the root of the
recurring "highlight stuck on" family — `BoatController` carries a v03 changelog header about
banks staying cyan. **FIXED in `51c5c35`** via `HighlightService` (`MaterialPropertyBlock`).
`V1`, `V1b`, `V1c` guard the observable symptom and `X7` is the control for the new mechanism.
The leak was never visible in these images, so the captures remain valid.

**Boat state desync after a row push. FIXED** in `2eb1678` — `L2` is green, with `X8` as its
control. Not the documented cause: the push tracks the boat correctly (distance to its own snap
point was 0.1500 both before and after). `FindTileAndSnapPointAtWorldPos` compared in 3D against
a 0.5 threshold while a resting boat sits 0.5 *above* the snap plane, so Y alone exhausted the
budget and the lookup fell through to a coincident snap point on the neighbouring tile. It now
compares in the horizontal plane. `ResynchronizeStateWithTransform` is retained as a safety net
that should never fire.

## Regeneration policy

> **A golden may also be replaced for an INTENTIONAL VISUAL CHANGE**, under exactly the same
> conditions as a defect fix: a human compares before and after, approves, and the commit names
> what changed and why the new image is right. Blind regeneration stays forbidden — the whole
> value of a golden is that a human looked.
>
> **Baselined for the orthographic adoption** (`c89dcd6`). All 8 moved 23.4–28.7 %: the board is
> no longer a perspective trapezoid, every tile reads at the same size, and path width is uniform
> across the board. Approved as the PRE-SHADER baseline so the channel-geometry work starts from
> a clean suite.



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
