# Hapi's Havoc — Board Game Design Review

Review of *Full Rulebook (Narrative Edition • Draft 2)*.
Written for the designer, not for a publisher. Blunt on purpose.

---

## 0. The headline finding

**The river has no mechanical connection to the win condition.**

Trace the path from "resource" to "victory point" as the rules are actually written:

1. `Quarry Stone` worker → *"Gain 1 Stone token"* → goes to your warehouse.
2. Warehouse Time → *"Collect resources from workers; freely move tokens between docked Barges and your warehouse."*
3. Construction → *"move 1 Stone from warehouse onto the highest reachable cell."*

Quarry → warehouse → pyramid. The boat is never in that chain. Nothing in §4.2, §4.4 or §4.5 requires a resource to have travelled by River-runner. Cargo Barges explicitly *do not move* — they sit in the dock on your player board. So the barges are a second warehouse with extra steps, and the River-runners are sailing around a beautifully chaotic board, dodging obstacle dice, risking cargo they were never required to carry.

Everything distinctive about this game — the shifting grid, the path-connection routing, the push-your-luck obstacle runs — is currently a **side activity you can ignore**. A competent player will quarry, build, and treat the Storm phase as ambient noise.

I'm fairly confident this isn't what you designed; it's what got lost in the writing. The obvious intent is that quarry and forest sites sit **across the river**, workers must be **ferried there**, and stone must be **shipped back**. But the rulebook implements worker placement as a plain shared-board action selection with no location requirement, which severs it.

**Fixing this is the single highest-value change, and it makes the game simpler, not more complex.** Everything else in this document is secondary.

---

## 1. What the game is actually about (and what it thinks it's about)

You pitch it as Cascadia / Dorfromantik — *"easy teach, crunchy choices."* Those games have **two** systems each.

Hapi's Havoc as written has **eight**:

| # | System | Rules real estate | Decisions it generates |
|---|--------|-------------------|------------------------|
| 1 | Shifting river grid | small | **high — this is the game** |
| 2 | Boat path routing | small | **high** |
| 3 | Cargo weight vs. speed | medium | medium (buried in arithmetic) |
| 4 | Worker placement | large | **near zero** (see §3.2) |
| 5 | Resource conversion | medium | low — linear, no engine |
| 6 | Warehouse upgrades | small | near zero |
| 7 | Ability card deck (52) | **largest** | swingy, not strategic |
| 8 | Initiative Feast track | medium | **near zero** (see §3.4) |

Systems 1–3 are the game you invented. Systems 4–8 are the game you assembled from the euro parts bin, and they consume roughly 80% of the rulebook while generating a small fraction of the interesting decisions.

**The pitch and the design are describing two different games.** Pick one. My strong recommendation is to build the game the pitch promises, because the pitch is describing your actual innovation.

---

## 2. Economy math — the numbers don't hold

### 2.1 Food is not scarce, so nothing that costs Food is a real cost

Starting position: 4 Food, 4 Workers.

`Gather Food` is **free to place** and yields **4 Food**. Every other action costs **1 Food**.

So the dominant opening is trivially: **1 worker on Gather (+4), 3 workers on paid actions (−3) = net +1 Food per round**, while taking three real actions.

Food therefore *accumulates* from round one and never binds. Which means:

- Every action in the game is effectively **free**.
- The Initiative Feast track (fed by Food spent) is fed by a resource nobody is short of.
- `Pray` at 1 Food, `Quarry` at 1 Food and `Build Site` at 1 Food are all priced identically at zero real cost, so the price tags convey no information and create no tradeoffs.

**Fix direction:** either Gather Food yields 2 (making the ratio tight), or Food stops being the universal action currency and becomes purely the initiative bid. Doing both is cleaner — see §5.

### 2.2 Game length is roughly triple the pitch

You need 14 stones (which, note, is exactly a 3-layer pyramid: 9 + 4 + 1 — nice, keep that).

Throughput as written:
- `Quarry Stone`: 1 stone per worker per round.
- `Build Site`: 1 stone placed per worker per round.

Assuming one worker on each, that's **1 stone placed per round → 14 rounds minimum**, using half your workforce. Add ramps: 3 + 5 + 7 = **15 Wood** for three extensions, at 2 Wood per worker-round ≈ **8 worker-rounds**, plus 3 more Build Site actions.

Realistic length: **16–20 rounds**. Each round is six phases including a dice roll, alternating worker placement, two boat movements, warehouse shuffling, construction, and an initiative check.

That's a **2–2.5 hour game**. Cascadia is 30–45 minutes. This is not a small gap you can trim with better teaching — it's structural.

> **Ambiguity to resolve:** Is `Build Site` one slot or can you stack multiple workers there? §4.2 marks it "(personal)" with no slot count. If it's unlimited, a player can place 3 stones in a round and the game is 6 rounds instead of 16. This one unstated number swings game length by 3×.

### 2.3 The obstacle risk is under-priced to the point of being a non-decision

- Obstacle die: **1–3** → one occupied cargo slot is hit. **4–6** → nothing.
- If the slot held Stone → it becomes **Damaged**.
- Damaged Stone still scores — **1 VP instead of 2**.

So: a 50% chance to lose **1 victory point**, out of a ~35-point game.

The correct play is always *"take the risk"*. That means your most thematic, most tense-sounding system resolves to a mandatory button-press. Push-your-luck is dead on arrival when the downside is this cheap.

Two things are wrong and both need fixing:

1. **Flat odds.** Real push-your-luck ramps: entering your 2nd obstacle should be worse than your 1st. Right now you roll the same 50/50 every time (§4.1 confirms one roll per obstacle entered), so a 4-obstacle dash is just four independent coin flips — mathematically punishing but psychologically flat, with no rising-tension curve.
2. **Trivial penalty.** Losing a Stone should cost you *tempo*, which is what actually hurts in a race. Damaged-but-still-scoring is the weakest possible penalty.

### 2.4 Scoring is dominated by a single term, and there's no catch-up

| Source | Max contribution |
|---|---|
| 14 undamaged Stones × 2 VP | **28** |
| First-to-finish bonus | 5 |
| Sphinx markers (3 layers × 3 VP) | 9 |
| **Total ceiling** | **~42** |

Stone count is ~67–80% of the final score, and stone count is a **pure function of tempo**. Whoever converts workers to stones fastest wins; sphinxes, damage and card play are rounding error. Worse, the leader completes layers first (more sphinxes) *and* finishes first (+5), so the bonuses **amplify** the lead rather than compressing it.

A two-player race game with no rubber-banding and a 16-round runtime means the trailing player usually knows they've lost by round 8 and plays out the remaining hour. That is the most damaging property a game can have.

### 2.5 The pyramid is a track wearing a 3-D costume

*"Place Stone: move 1 Stone from warehouse onto the highest reachable cell."*

"Highest reachable" is deterministic. You never choose **where** — only ramp timing gates **when**. So the raised 3-D pyramid grid, one of your most expensive components, hosts **zero decisions**. It's a score counter shaped like a pyramid.

There's a good game hiding here: make placement **spatial**. If a stone must be supported (both cells beneath it filled), and ramps grant access to specific *faces* rather than a global height, then the pyramid becomes a small construction puzzle and your component earns its cost.

---

## 3. System-by-system audit

### 3.1 Cargo slots — right idea, wrong implementation

*"Slot usage = 2 (Stone) + 1.5 (Wood) + 1 (Food) = 4.5 → rounds to 5 slots → Speed = 4 − 2 = 2 tiles."*

**Keep the idea.** "Heavier boat = slower boat" is thematic, creates a real load-out decision, and ties directly into the obstacle risk (a light boat outruns danger). This is one of the better things in the design.

**Kill the implementation.** Fractional slot sizes (Stone 1, Wood 0.5, Food 0.25), free redistribution within slots, partial-fill for damage mitigation, then rounding, then a division to get speed — that's mental arithmetic *every turn, for both boats*, in a game that claims "easy teach."

Fix: everything is 1 slot. Boat is 6 slots. Speed = 4 − floor(filled ÷ 2). Same decision, no arithmetic. The "partial-fill to reduce losses" trick is clever but it's a rules-lawyer optimization that most tables will never find, and it costs you more complexity than it returns.

### 3.2 Worker placement — no blocking, plus one hard lockout

The engine of every worker-placement game is **contention**: I take the slot you wanted.

Your numbers: 2 players × 4 workers = 8 placements per round, against 4 + 4 + 4 = 12 shared slots plus personal Build Sites plus a free Upgrade Barge action.

**Supply exceeds demand.** Contention essentially never occurs. So you're paying the full complexity price of worker placement — a phase, a resolution sub-phase, turn-order alternation, a cost table — and receiving none of its tension.

Except in exactly one place, where it's *too* sharp: **`Pray` has 1 slot.** One player can occupy Pray every single round and cut their opponent off from the entire 52-card ability deck permanently. That's not tension, it's a degenerate lock — and it's especially bad because card access is already a bottleneck (§3.3).

So the phase is simultaneously **too loose everywhere** and **too tight in one spot**. Both symptoms of the same thing: slot counts were set by feel, not by `players × workers`.

### 3.3 The ability deck — unbalanced by construction

52 cards, distribution: 12 / 8 / 8 / 6 / 6 / 4 / 3 / 2 / 2 / 1.

- **Wrath of Bastet — 1 copy in 52.** A single card that removes two of your opponent's workers, buried in a deck you draw from at ~1 card per round through a contested single slot. Whoever happens to draw it gets a large swing for zero skill. Singleton cards in a shuffled deck are variance, not design.
- **Hapi's Fury — 12 copies (23% of the deck).** Nearly a quarter of every draw is "roll the Storm dice again," i.e. *add more chaos to an already-chaotic board*. That's the most common card and the least interesting one.
- **Access rate is ~1 card/round through a 1-slot bottleneck**, with a 3-card hand limit checked at end of Warehouse Time.

Meanwhile the five distinct **play windows** (§5, between every pair of phases) is a genuinely elegant piece of design that costs the player almost nothing to learn — that part I'd keep.

Fix direction: shrink the deck hard (16–24 cards), make copies of each card 2–4 (never 1), delete the pure-chaos cards, and open up access so the deck is a reliable tool rather than a lottery.

### 3.4 The Initiative Feast — not actually a bid

*"As you pay Food for actions, place those tokens on the Feast Track."*

Initiative is therefore a **byproduct of taking actions**, not a cost you weigh. The player who does more stuff automatically leads the track. You can spend extra Food to push further, but per §2.1 Food is abundant, so this isn't a sacrifice.

And the payoff — with 2 players, choosing "Start or Second" — is a modest lever.

**But note the interaction with §3.5:** the Start Player unilaterally chooses the insertion side for *all four* Storm dice, which reshapes the entire board. That makes Start Player enormously powerful — far more than the Feast track prices it at. The track undervalues the very thing it awards.

### 3.5 The Storm — too much churn, and one player controls all of it

Four dice per round (3 blue + 1 red). Each pushes one row of a 6×6 grid.

- **~4 of 6 rows shift every single round.** Any route the non-active player planned is destroyed before they sail. Planning becomes pointless, which drains the boat-routing system — your best mechanic — of its strategic weight and reduces it to tactical improvisation.
- **The Start Player picks the side for all four dice.** The opponent has zero input into the phase that rearranges the board they're about to navigate. In a two-player game this is the single largest agency imbalance in the design.
- **The red die injects one obstacle tile every round**, and obstacles only leave by being pushed off the far edge. Over 16 rounds the board silts up. Escalating havoc may be the intent — but it needs an explicit check, because "the board becomes unnavigable by round 12" is a real possible outcome and the rulebook doesn't address it.

Fix direction: fewer dice (2, maybe 3), and **split the choice** — each player picks the side for their own die, or the roller picks rows and the opponent picks sides. Splitting the decision costs nothing in complexity and fixes the agency problem completely.

### 3.6 Warehouse extensions — cut it

9 slots, upgradeable to 12 for 1 Wood + 1 Food each. A whole subsystem, its own paragraph, its own tokens, and its own placement timing rule — to move a storage cap that (per §2.1) is unlikely to bind. This is complexity with no payload. Delete.

### 3.7 Component cost — a publishing problem

52 double-sided river tiles, 52 cards, a physical dice tower, 4 dice, 8 wooden worker minis, 4 river-runners, 4 cargo barges with slots, three resource token types, 10 sphinx markers, two player boards with recessed docks *and raised 3-D pyramid grids*, plus a reference sheet.

That's a **$60–70 retail box**. For an unpublished designer pitching a first title, that's a hard sell — publishers evaluate cost-to-produce early and ruthlessly. Every subsystem you cut also cuts components, which makes the pitch dramatically easier.

The dice tower doubling as a card vault is a lovely touch and probably worth keeping as the one indulgence.

---

## 4. Smaller notes

- **The theme is doing real work.** "Hapi swells the Nile and rearranges it under you" is a genuinely strong marriage of theme to mechanism. The flavour text is good. Don't lose this in the cuts — it's an asset.
- **Player count is fixed at 2** throughout (4 workers each, 8 total; "both players" in card text). Worth deciding deliberately whether that's the design or a limitation — a strict 2p game is a smaller market but a much easier design problem, and honestly it might be the right call here.
- **§4.1 wording is unclear:** *"The results show two rows (1‑6)"* — with four dice, this reads as a typo or a leftover from an earlier version. Needs rewriting.
- **Obstacle rules are stated in two places** (§4.1 for ejection, §4.3 for sailing) with slightly different framing. Consolidate into one box.
- **"Both roll a blue die; high roll becomes Start Player"** and the Feast track both determine turn order. Fine, but the rulebook should say plainly that the die roll is round 1 only.
- **`Upgrade Barge` is free to place** but no slot count is given, and it's the only free non-Gather action. Almost certainly needs a cost or a cap.

---

## 5. Three ways forward

Ordered by how much you'd have to throw away. All three assume **§0 is fixed first** — the river must be load-bearing.

### Option A — "The River Game" *(recommended)*

Cut to two systems: **shifting grid + boat logistics**.

- Quarry and Forest sites are **on the far bank**. Workers must be ferried there by River-runner; stone and wood must be shipped back. This alone makes every Storm roll matter to every player, every round.
- **Delete** worker placement as a shared-board phase. Workers become boat crew — a small pool you deploy to sites you can physically reach. Site capacity creates the contention that the current placement phase fails to create.
- **Delete** the ability deck, warehouse upgrades, fractional slots and the Feast track.
- **Keep** cargo weight vs. speed (integer), obstacle push-your-luck (re-tuned per §2.3), the pyramid (made spatial per §2.5).
- Target: **45 minutes, 2 systems, ~$35 box.**

This is the game your own pitch describes. It's also the version closest to your Unity build, which means the two projects would start reinforcing each other instead of diverging.

### Option B — "Trim the Euro"

Keep worker placement, cut everything else non-essential.

- Fix §0 by requiring boat delivery of stone to the Build Site.
- Fix slot counts to `players × workers − 2` so contention actually happens; give `Pray` 2 slots minimum.
- Cut the card deck to ~20 cards, min 2 copies each.
- Cut warehouse upgrades, fractional slots, and the Feast track (initiative passes or is a simple 1-Food auction).
- Target: **75 minutes, 4 systems.** A solid mid-weight euro, but competing in a very crowded space where your one novel mechanic is diluted.

### Option C — "Commit to the Heavy Euro"

Keep everything, but fix it properly: rebalance the entire economy, rebuild the deck, add catch-up mechanisms, restructure worker slot counts, tune obstacle risk, and make the pyramid spatial.

Honest assessment: this is **12–18 months of iteration** and lands you in the most competitive segment of the hobby, going up against designers with a decade of published credits. I don't recommend it, but if the heavy euro is genuinely the game you want to play, it's a legitimate path — it just isn't the game your rulebook is currently pitching.

---

## 6. What to test on the plywood prototype next

Don't test the whole game. Test the three claims the design rests on. Each is a short session.

**Test 1 — Is the river fun on its own? (30 min, no economy at all)**
Two boats, the 6×6 grid, Storm rolls, and a single goal: be first to touch the far bank and return, three times. No resources, no workers, no cards, no pyramid.
*What you're watching for:* do players lean in during the Storm phase? Do they audibly react to a route being destroyed? Do they take obstacle risks and feel something?
**If this isn't fun, nothing built on top of it will be.** This is the most important 30 minutes you can spend.

**Test 2 — Is the push-your-luck live? (15 min)**
Same setup, but tune the obstacle penalty upward until players visibly *hesitate* before entering a red tile. Try: lose the cargo outright; or lose your remaining movement; or the boat is pushed back to the bank.
*What you're watching for:* the point where the decision stops being automatic. Note that number. That's your real penalty value, and you cannot derive it from a spreadsheet.

**Test 3 — Does the Storm need splitting? (one session)**
Run identical setups with (a) 4 dice, roller picks all sides, versus (b) 2 dice, each player picks their own side.
*What you're watching for:* whether players can form a plan that survives to their own turn. My prediction is that (b) is dramatically better and you'll know within one game.

**Only after all three pass:** reattach the economy — and reattach it in the smallest form that works (Option A), not the form in Draft 2.

---

## 7. The short version

- **The river isn't connected to winning.** Fix that first; it's the whole ballgame. *(§0)*
- **Food never binds**, so no action has a real cost. *(§2.1)*
- **The game runs 16–20 rounds** against a pitch of 30–45 minutes. *(§2.2)*
- **The obstacle risk costs 1 VP**, so nobody hesitates. *(§2.3)*
- **Stone count is 70%+ of score with no catch-up** — the loser knows by round 8. *(§2.4)*
- **The pyramid hosts zero decisions** despite being your priciest component. *(§2.5)*
- **Worker placement has no contention** — except `Pray`, which is a hard lockout. *(§3.2)*
- **One player controls the entire Storm phase.** *(§3.5)*
- **The shifting grid, the routing, and the theme are genuinely good.** Cut hard around them.

The instinct that led you to build a *puzzle game* out of this in Unity was correct, and it was correct for the same reason this review keeps circling back: **the river grid is the design, and everything else is packaging.**
