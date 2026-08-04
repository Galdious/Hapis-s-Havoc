# Claude Code — Job 02: Repo hygiene, baseline tag, Unity 6.3 LTS upgrade

Three phases. **Run them in order and stop between each.** Phase B is the only irreversible one, and Phase A exists specifically to make Phase B verifiable.

**Why this order:** you cannot judge whether a Unity upgrade broke something if the console is already full of noise you'd learned to ignore. Phase A silences known noise so that every error you see during Phase C verification is genuinely caused by the upgrade.

---

## Phase A — Hygiene and baseline (do this yourself, ~30 min, no AI needed)

### A1. Fix `.gitignore`

Your `.gitignore` is the standard GitHub Unity template and it's mostly fine. Four gaps:

```gitignore
# --- Project-specific additions ---

# macOS
.DS_Store
**/.DS_Store

# Unity crash-recovery scene dumps — never source of truth
/[Aa]ssets/_[Rr]ecovery/
/[Aa]ssets/_[Rr]ecovery.meta

# Unity AI / generated content cache
/GeneratedAssets/
/GeneratedAssets.meta

# Explicit build output — the template's /[Bb]uilds/ only matches BUILDS/
# by accident on this machine because core.ignorecase=true.
# On any case-sensitive filesystem (Linux CI, another contributor) it would not.
/BUILDS/
```

> The `BUILDS/` one matters more than it looks. It's currently ignored only because your Mac's filesystem is case-insensitive and `git config core.ignorecase` is `true`. The moment this repo is cloned on Linux, or a collaborator joins, your entire build output starts getting committed.

### A2. Untrack what's already committed

`.gitignore` does not retroactively remove tracked files. Currently tracked and shouldn't be: **5 `.DS_Store` files** and the whole **`Assets/_Recovery/`** folder (8 crash-dump scenes + metas).

```bash
git rm --cached .DS_Store Assets/.DS_Store Assets/TutorialInfo/.DS_Store \
                Assets/_Project/.DS_Store Assets/_Project/Scripts/.DS_Store
git rm -r --cached "Assets/_Recovery"
git commit -m "chore: untrack .DS_Store and crash-recovery scenes"
```

Delete `Assets/_Recovery/` from disk too, but **only after** confirming none of those eight scenes contains work that isn't in `LevelEditor.unity`. They're auto-generated crash dumps, so almost certainly not — but check before deleting, not after.

### A3. Fix the placeholder Player Settings

From the architecture report: `applicationIdentifier` is still `com.UnityTechnologies.com.unity.template.urpblank` and `companyName` is `DefaultCompany`. Both block store submission and both are annoying to change later once a build is installed on test devices under the old bundle ID. Change now, in the Editor: **Project Settings → Player**.

### A4. Tag the baseline

**This is the actual safety net — not the branch.** A tag is an immutable named commit that no branch operation can move or delete.

```bash
git add -A
git commit -m "chore: repo hygiene before visual overhaul"
git tag -a v0.1-prepolish -m "Working ugly build. Unity 6000.2.0b8. Pre-upgrade, pre-visual-overhaul."
git push origin main
git push origin --tags
```

Verify it landed on GitHub before continuing. If anything below goes catastrophically wrong, `git checkout v0.1-prepolish` returns you to exactly this state.

---

## Phase B — Silence known noise (Claude Code job, before the upgrade)

Paste this into Claude Code. It is deliberately tiny — three changes, all with zero design implications.

```
Three small fixes. Do not refactor anything else. Do not touch gameplay logic.
Read CLAUDE.md first and respect the house rules.

1. BoatManager.Update() reads Keyboard.current.bKey every frame with no null
   check. On a device with no keyboard attached, Keyboard.current is null and
   this is a NullReferenceException every frame. Guard it with a null check, or
   better, wrap the whole debug-key block in #if UNITY_EDITOR since it is a
   development shortcut and has no place in a mobile build.

2. The ConnectionDump component is attached to the DominoTile prefab, so it
   logs on every single tile spawn — including in release builds. Wrap its
   logging in #if UNITY_EDITOR, or remove the component from the prefab.
   Report which you did and why.

3. Search the whole project for Debug.Log / Debug.LogWarning calls that fire on
   a per-frame or per-spawn basis (not one-off initialisation logs). List them
   for me with file and line. Do NOT delete them yet — I want to see the list
   first and decide. Just report.

Then run a compile check and report any errors. Do not attempt to open Unity
or run a build.
```

Commit this on `main` before branching. Console should now be quiet on a normal level load.

---

## Phase C — Unity 6.3 LTS upgrade

### C0. Why 6.3 LTS specifically

You're on `6000.2.0b8`. Two independent problems:

1. It's a **beta**.
2. The **6.2 update-release line is no longer supported at all.**

Unity 6.0 LTS support ends October 2026 — about two months out — so upgrading to it would buy you nothing. **Unity 6.3 LTS is supported through December 2027** and is the only sensible destination. Install it via Unity Hub with the **Android** and **iOS** build support modules, plus **WebGL** if you still want browser builds.

### C1. Branch — but plan to merge fast

```bash
git checkout -b chore/unity-6.3-upgrade
```

**Do not let this branch live for weeks.** An editor version bump rewrites `.meta` files and asset serialization across thousands of files. If `main` moves in parallel, the merge becomes a conflict swamp in files nobody meaningfully edited. Upgrade → verify → merge to `main` within a day or two → make that the new baseline. Feature branches come after, not alongside.

### C2. Do the upgrade

Unity will want to reserialize the project on first open. Let it finish completely before touching anything — it takes a while on a project this size and interrupting it is how you get corrupted `.meta` files.

Then, in this order:
1. Let the reimport complete.
2. Open the Package Manager. Several packages will need version bumps for 6.3. Expect movement on `com.unity.render-pipelines.universal` (currently 17.2.0), `com.unity.inputsystem`, `com.unity.cinemachine`, and the `com.unity.ai.*` family (`com.unity.ai.assistant` is on `1.0.0-pre.8`, an old prerelease).
3. Commit **immediately** after the reimport, before fixing anything: `git commit -am "chore: Unity 6.3 reimport (unverified)"`. This separates "what the upgrade changed" from "what I changed to fix it," which makes the diff readable.
4. Then fix compile errors, committing in small logical chunks.

### C3. Verification checklist

The project has three operating modes sharing one scene, so a regression in one can hide behind another working fine. Walk all three.

**Boot / menus**
- [ ] `MainMenu.unity` loads; all buttons respond; Quit works
- [ ] `LevelSelect.unity` loads; camera auto-focuses next unlocked level; vertical pan with inertia works; Menu button is clickable through the pan controller
- [ ] Star ratings persist and the correct levels are unlocked

**Playing mode** — load each of the six levels in `Assets/Resources/Levels`
- [ ] Tiles spawn with correct rotation and flip state
- [ ] Path lines render on every tile (this is `LineRenderer` + a ShaderGraph — a prime upgrade-breakage candidate)
- [ ] Boat selects, lifts, bobs; highlights appear on valid destinations
- [ ] Boat moves along straights, curves and U-turns
- [ ] Boat embarks from a bank and returns to a bank
- [ ] Reversed (vortex) tiles: pathfinding sees through them, movement is forced straight
- [ ] Hard blockers stop movement
- [ ] Drag a hand tile into a drop zone → row pushes → far tile ejects
- [ ] Boat survives being on an ejected tile (fade out, settle, fade in)
- [ ] Locked rows reject pushes
- [ ] Collectibles pick up; counters update
- [ ] Undo restores previous state correctly
- [ ] Restart works
- [ ] Win screen shows correct stars and the achievement checklist
- [ ] Fail screen appears when moves run out

**Editor mode**
- [ ] Paint / rotate / flip tiles
- [ ] Set start and end positions **(note: setting a *bank* start currently throws — see risk R7 in ARCHITECTURE.md. If it throws, that's a pre-existing bug, not upgrade breakage. Don't chase it here.)**
- [ ] Build a hand; counts display correctly
- [ ] Save to JSON, reload, verify round-trip is lossless
- [ ] Push arrows work from both sides
- [ ] Row lock toggles work

**Endless mode**
- [ ] Starts and resumes from PlayerPrefs
- [ ] World streams new rows ahead, cleans up behind
- [ ] Storm forecast displays, then pushes resolve
- [ ] Stamina / AP decrement correctly
- [ ] Camera follows without stutter
- [ ] Both lose conditions fire (stamina exhausted, ejection)
- [ ] Score screen displays

**Rendering / platform**
- [ ] Both URP assets still assigned (Quality level 0 = Mobile, 1 = PC)
- [ ] All four ShaderGraphs still compile: `Lit_ZWrite`, `PathHighlightShader`, `UI_Background_Shader`, `WaterShader`
- [ ] Tile and boat alpha fades still sort correctly (`Lit_ZWrite` forces ZWrite on a transparent surface — exactly the kind of thing an SRP version bump disturbs)
- [ ] Vortex and arrow particle-family materials still render
- [ ] UI scales correctly at 1080×1920 reference
- [ ] Build succeeds for Android
- [ ] Frame rate is in the same ballpark as before (you were above 200fps; the `FPSCounter` is already in the project)

### C4. Merge

Once the checklist passes:

```bash
git checkout main
git merge --no-ff chore/unity-6.3-upgrade
git tag -a v0.2-unity63 -m "Unity 6.3 LTS. All three modes verified."
git push origin main --tags
```

Two tags now. `v0.1-prepolish` is the last known-good pre-upgrade state; `v0.2-unity63` is your new working baseline and the point every feature branch forks from.

---

## Phase D — Unity MCP (right after the upgrade, before any visual work)

Set this up before step 4 of the roadmap. It closes the loop that currently requires you to manually paste every screenshot and console error, and it roughly halves iteration cost on anything visual.

**Option 1 — Unity's official MCP server.** Ships in the AI Assistant package. Gives scene hierarchy, GameObject/component read-write, console access, script editing and build settings. Auto-configures Claude Code from **Project Settings → AI → Unity MCP → Integrations**. Requires a Unity subscription and the project connected to Unity Cloud.

**Option 2 — a community server** (`IvanMurzak/Unity-MCP`, `CoderGamester/mcp-unity`). Free, and some ship `screenshot-scene-view` and `screenshot-isolated` tools — render a GameObject in isolation from a chosen angle. For visual iteration that screenshot loop is the entire value proposition.

**Try the community one first**, specifically for the screenshot tools. Fall back to the official one if you hit reliability problems.

Whichever you pick, verify with: *"Read the Unity console and summarise any warnings or errors."*

---

## What comes after

Roadmap position after this job:

- ~~1. Hygiene + gitignore + tag baseline~~ ← Phase A
- ~~2. Unity 6.3 LTS upgrade~~ ← Phase C
- ~~3. Unity MCP setup~~ ← Phase D
- **4. Cleanup: R2 `HighlightService` → R3 LineRenderer leak → R1 push consolidation** ← next job
- 5. `ThemeDefinition` scaffolding (data only, no art)
- 6. Procedural path mesh — replaces LineRenderers
- 7. Camera framing
- 8. Art direction pass

Steps 4 and 5 come before 7 and 8 deliberately: you can't design a highlight system on top of a broken one, and you can't art-direct a theme system that doesn't exist yet.
