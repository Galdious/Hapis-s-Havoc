# Claude Code — Job 01: Architecture documentation

Paste the block below into Claude Code, run from the repo root.

**Why this job first:** the codebase is ~13.5k lines across 39 gameplay scripts, and four files are 54% of it. Without a written map, every future session burns context rediscovering the same structure. This job is a one-time cost that makes everything after it cheaper and more accurate.

**Before you run it:** the conventions in Part 2 below are things Claude Code *cannot* reliably infer from the code. They're pre-filled from your rulebook and prototype doc so it doesn't have to guess. Read them and correct anything I got wrong — a wrong convention baked into `CLAUDE.md` will propagate into every future session.

---

## THE PROMPT

```
You are documenting an existing Unity 6 mobile puzzle game so that future sessions
can work on it efficiently. Produce TWO files. Do not change any gameplay code.

===============================================================================
DELIVERABLE 1 — /CLAUDE.md  (short, high-signal, target 150-250 lines)
===============================================================================

This is the file you will read at the start of every future session. It must be
dense and immediately actionable. No filler, no "this project is a game" prose.

Include:

1. PROJECT SNAPSHOT
   - What the game is, in 3 sentences.
   - The three operating modes (Editor / Playing / Endless) and which scene
     each lives in.

2. HOW TO NAVIGATE
   - The 8-10 files that matter, one line each on responsibility.
   - Explicit note of which files are god-objects and should not be grown.

3. CONVENTIONS THAT ARE NOT OBVIOUS FROM THE CODE
   - Copy the "Domain conventions" section verbatim from the block I provide
     at the end of this prompt. Do not paraphrase it. Do not re-derive it.

4. GOTCHAS
   - Document every pattern you find that has caused bugs. Look especially at
     the comment headers at the top of BoatController.cs, GridManager.cs and
     LevelEditorManager.cs — several of them are changelogs describing past
     bugs. Summarise the recurring bug classes.

5. HOUSE RULES FOR FUTURE WORK
   - State the rules I list in the "House rules" section at the end of this
     prompt, verbatim.

===============================================================================
DELIVERABLE 2 — /docs/ARCHITECTURE.md  (thorough, no length limit)
===============================================================================

1. SYSTEM MAP
   - A Mermaid diagram of the manager/controller relationships. Show which
     objects hold references to which, and mark every reference that is
     resolved via FindFirstObjectByType rather than a serialised field.

2. PER-FILE REFERENCE
   For each script in Assets/_Project/Scripts (excluding /backup and /_debug):
   - Responsibility in one sentence.
   - Public API surface (public methods/properties other scripts actually call).
   - Who calls into it, and who it calls out to.
   - Line count, and a complexity flag if it exceeds 400 lines.

3. THE FOUR LARGE FILES — detailed breakdown
   BoatController.cs (~1919), GridManager.cs (~1892),
   LevelEditorManager.cs (~2333), EndlessModeManager.cs (~1229).
   For each, list the DISTINCT RESPONSIBILITIES it currently holds, and propose
   a split into focused classes. Propose only — do not perform the split.

4. DATA FLOW
   - Level JSON -> loaded objects -> runtime state -> save/snapshot -> restore.
   - Document the LevelData JSON schema exhaustively, field by field, with
     types and meaning. Include what happens to unknown/missing fields.
   - How HistoryManager / GameStateSnapshot / EndlessStateSnapshot relate.

5. RENDERING & VISUALS INVENTORY
   - Every material in Assets/_Project/Materials and where it is used.
   - Every shadergraph in Assets/_Project/Shaders and where it is used.
   - How tile path visuals are currently produced (PathVisualizer), including
     an object/draw-call count estimate for a 6x6 board.
   - Which URP asset is assigned per platform and the notable settings.

6. DEAD CODE & DUPLICATION REPORT
   - Unreferenced public methods.
   - Duplicated logic across the three modes (Editor/Playing/Endless) — this
     is the most likely place to find copy-paste divergence.
   - Contents of Scripts/backup and whether anything there is still referenced.
   - Anything in the repo that should be gitignored but isn't.

7. RISK REGISTER
   - Ranked list of the things most likely to cause bugs or block future work.
   - For each: severity, why it's risky, and the smallest fix that addresses it.
   - Do not fix anything. Just rank and describe.

===============================================================================
METHOD
===============================================================================
- Read every .cs file under Assets/_Project/Scripts. Do not sample.
- Read the .unity scene files and .prefab files as text where useful for
  understanding wiring — they are YAML and are readable.
- Read Assets/Resources/Levels/*.json — all six.
- Read Assets/Settings/*.asset for the render pipeline configuration.
- Ignore Assets/TextMesh Pro, Library/, Temp/, obj/, BUILDS/.
- Where you are uncertain, say so explicitly in the doc rather than guessing.
  A documented unknown is useful; a confident wrong statement is not.

===============================================================================
DOMAIN CONVENTIONS  — copy these into CLAUDE.md verbatim
===============================================================================

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

===============================================================================
HOUSE RULES  — copy these into CLAUDE.md verbatim
===============================================================================

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
- Do not run Unity or attempt to build. Report what needs testing instead.
```

---

## After this job — suggested order for the cleanup work

Run these as separate Claude Code sessions, each starting fresh with `CLAUDE.md` in context. Do not bundle them.

**Job 02 — Get off the Unity beta.**
Currently on `6000.2.0b8`. Shipping a mobile title on a beta editor is a real liability. Move to the current stable release, resolve breakage, verify all three modes. Do this before building more content on top.

**Job 03 — MaterialPropertyBlock migration.**
Replace every `renderer.material` / `sharedMaterial` mutation used for highlighting with `MaterialPropertyBlock`. This kills an entire recurring bug class (the "banks stay cyan" family documented in your own file headers) and reduces material instancing. Low risk, high return.

**Job 04 — Split `BoatController`.**
Using the split proposed in `ARCHITECTURE.md`. It currently owns input handling, pathfinding, highlight rendering, animation, UI text updates and turn state. Extract highlighting and UI first — those are the cleanest seams and the least likely to regress movement logic.

**Job 05 — Path rendering.**
Replace the per-connection `LineRenderer` GameObjects. Currently a 6×6 board churns ~140 renderer objects, instantiated and destroyed on every row push. This is simultaneously the biggest cheap perf win and the biggest cheap *visual* win, since the current paths read as wires rather than water.

**Job 06 — URP settings pass.**
Shadow distance is 50 on a board ~5 units across, so the shadowmap is spending almost all its resolution on empty space. Also: no colour grade on the game scenes, and MotionBlur is enabled in `SampleSceneProfile` (bad idea on mobile). Free quality.

---

## One thing to check before you start

Claude Code cannot press Play, read the Unity console, or screenshot the Game view on its own. Before Job 02, look for a **Unity MCP server** — several community ones exist and they bridge exactly this gap. Without one, you'll be manually pasting every screenshot and every console error, which works but roughly doubles the iteration cost on anything visual.
