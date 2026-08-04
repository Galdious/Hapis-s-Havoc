# Claude Code — paste-ready prompt: Phase A + B

**Before you paste:** run Claude Code from the repo root (`Hapis-s-Havoc/`). It will ask permission for `git` commands as it goes — approve them. Do **not** use `--dangerously-skip-permissions` for this job; it makes commits and one deletion, and you want to see each one.

There are **three deliberate stop points** where it must ask you before proceeding. That's by design, not friction — each one is a decision I don't want it guessing at.

---

## THE PROMPT

```
Repo hygiene and noise reduction on the Hapi's Havoc Unity project.
Read CLAUDE.md first and respect the house rules.

Scope: git configuration, file tracking, project metadata, and three tiny
code fixes. Do NOT touch gameplay logic, do NOT refactor, do NOT change
anything in Assets/_Project/Scripts beyond the three fixes in STEP 7.

There are three STOP points where you must ask me and wait for an answer.
Do not proceed past them on your own judgement.

===============================================================================
STEP 1 — Report before changing anything
===============================================================================
Run and show me the output of:
  git status --short
  git ls-files | grep -iE "\.DS_Store|_Recovery"
  git config core.ignorecase
  git log --oneline -1

Confirm the working tree state and tell me if anything looks different from
what I describe below. If it does, stop and tell me rather than adapting.

===============================================================================
STEP 2 — Append to .gitignore
===============================================================================
The existing .gitignore is the standard GitHub Unity template and is mostly
fine. Append this block at the end. Do not reorder or remove existing lines.

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

# Explicit build output. The template's /[Bb]uilds/ pattern only matches
# BUILDS/ on this machine because core.ignorecase=true. On a case-sensitive
# filesystem (Linux CI, another contributor) it would not match at all.
/BUILDS/

===============================================================================
STEP 3 — Untrack files that are already committed
===============================================================================
.gitignore does not retroactively untrack. These are currently tracked and
should not be:

  .DS_Store
  Assets/.DS_Store
  Assets/TutorialInfo/.DS_Store
  Assets/_Project/.DS_Store
  Assets/_Project/Scripts/.DS_Store
  Assets/_Recovery/   (8 scenes + 8 .meta files)

Use `git rm --cached` (NOT plain `git rm`) so nothing is deleted from disk yet.
Verify with `git status --short` that they now show as deleted-from-index only.

===============================================================================
STEP 4 — STOP #1: verify before deleting _Recovery from disk
===============================================================================
Assets/_Recovery contains eight files named "0.unity" through "0 (7).unity",
dated July-August 2025, largest 292KB. Assets/_Project/Scenes/LevelEditor.unity
is 566KB and dated September 2025.

These are almost certainly Unity crash-recovery dumps of an older, smaller
version of the main scene. Before I delete them I want that confirmed, not
assumed.

Inspect the scene YAML of the largest one, 0 (5).unity, and compare its
GameObject count and the set of MonoBehaviour script GUIDs it references
against LevelEditor.unity. Report:
  - how many GameObjects each contains
  - whether 0 (5).unity references any script GUID, prefab or asset that
    LevelEditor.unity does NOT reference
  - your confidence that nothing unique would be lost

Then STOP and ask me whether to delete the folder from disk. Do not delete it
on your own judgement. If you find anything unique, list it explicitly.

===============================================================================
STEP 5 — STOP #2: untracked files in the repo root
===============================================================================
`git status` shows these as untracked and they need a decision, not a guess:

  CLAUDE.md
  docs/ARCHITECTURE.md
  CLAUDE_CODE_JOB_01_architecture-docs.md
  CLAUDE_CODE_JOB_02_hygiene-and-upgrade.md
  CLAUDE_CODE_PROMPT_PhaseAB.md
  DESIGN_REVIEW_Boardgame.md
  Hapi's Havoc - Full Rulebook (Narrative Edition - Draft 2)-1.pdf
  Hapi's Havoc - Unity Prototype.pdf
  TEST.json
  Zrzut ekranu 2025-09-12 o 09.41.59.png

Propose a tidy arrangement — my instinct is that CLAUDE.md stays at root,
the design/job markdown and the PDFs move into docs/, and the loose screenshot
and TEST.json either move to docs/ or get ignored. Tell me what you recommend
and why, then STOP and ask before moving or committing any of them.

Note: TEST.json looks like a hand-made level export that is NOT one of the six
in Assets/Resources/Levels. Check whether it is a duplicate of any of them
before recommending. If it is unique it may be worth keeping.

===============================================================================
STEP 6 — Fix placeholder Player Settings
===============================================================================
In ProjectSettings/ProjectSettings.asset:
  - `companyName` is `DefaultCompany` (line ~15)
  - `applicationIdentifier` (line ~166) is a nested per-platform map. Show me
    its current contents. The value under the platform keys is the URP
    template default (com.UnityTechnologies.com.unity.template.urpblank).

Both block store submission, and the bundle ID is annoying to change once test
builds are installed on devices under the old ID.

`productName` is already correct ("Hapi's Havoc") — leave it.

Show me the exact lines you intend to change and what you'd change them to,
using a reverse-domain identifier. Ask me for the company name and domain I
want rather than inventing one. Edit the YAML directly — do not open Unity.

===============================================================================
STEP 7 — Silence known console noise
===============================================================================
Three small fixes. Zero design implications. This must happen BEFORE the Unity
upgrade so that any console error during upgrade verification is genuinely
caused by the upgrade rather than by pre-existing noise I'd learned to ignore.

7a. BoatManager.Update() reads Keyboard.current.bKey every frame with no null
    check. On a device with no keyboard, Keyboard.current is null and this
    throws a NullReferenceException every frame. Wrap the whole debug-key
    block in #if UNITY_EDITOR — it is a development shortcut with no place in
    a mobile build. If that is not possible for some reason, add a null guard
    instead and tell me why.

7b. The ConnectionDump component is attached to the DominoTile prefab, so it
    logs on every tile spawn including in release builds. Either wrap its
    logging in #if UNITY_EDITOR or remove the component from the prefab.
    Choose one, do it, and tell me which and why.

7c. Search the whole project for Debug.Log / Debug.LogWarning / Debug.LogError
    calls that fire per-frame or per-spawn (not one-off initialisation).
    REPORT THEM ONLY — file, line, and what triggers them. Do not delete or
    modify any of them. I want to review the list and decide.

Then verify the project still compiles. Report any errors. Do NOT open Unity
and do NOT attempt a build.

===============================================================================
STEP 8 — Commits
===============================================================================
Make these as separate, logical commits, not one lump:

  1. "chore: extend gitignore for macOS, recovery dumps and build output"
  2. "chore: untrack .DS_Store and crash-recovery scenes"
  3. "chore: set company name and application identifier"   (after STOP #2)
  4. "chore: silence per-frame and per-spawn debug logging"
  5. (docs move, if I approved one in STEP 5)

Show me `git log --oneline -6` and `git status --short` when done.

===============================================================================
STEP 9 — STOP #3: the baseline tag
===============================================================================
Do NOT tag or push until I confirm.

When I confirm, run:
  git tag -a v0.1-prepolish -m "Working build, Unity 6000.2.0b8. Pre-upgrade, pre-visual-overhaul."
  git push origin main
  git push origin --tags

Then verify the tag exists on the remote and tell me the commit SHA it points
at. This tag is the safety net for the Unity 6.3 upgrade that comes next, so
I want to see it confirmed on GitHub, not just locally.
```

---

## After it finishes

Sanity-check these yourself — they're quick and they're the things that actually matter:

- `git tag -l` shows `v0.1-prepolish`, and it's visible under **Releases → Tags** on GitHub.
- `git status --short` is clean, or shows only things you deliberately left untracked.
- Open the project in your **current** Unity (6000.2.0b8, not 6.3 yet) and load a level. Console should be noticeably quieter. **This is the last time you run the old editor** — you're confirming the pre-upgrade state is good so that anything broken afterwards is unambiguously the upgrade's fault.

Then Phase C.

---

## One note on Phase C

You said you want Claude Code to actually run the game and see how it behaves. It can't do that on its own — it needs a **Unity MCP server** bridging it to the editor. So the real Phase C order is:

1. Install Unity 6.3 LTS via Unity Hub (Android + iOS modules)
2. Upgrade on the `chore/unity-6.3-upgrade` branch, let the reimport finish completely
3. **Set up Unity MCP** — do this before verification, not after, so Claude Code can read the console and drive Play mode itself
4. Then work the verification checklist in `CLAUDE_CODE_JOB_02` together

Ping me when Phase A + B is committed and I'll write the Phase C prompt with the MCP setup folded in.
