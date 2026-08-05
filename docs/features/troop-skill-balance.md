# Troop Skill Balance

## Overview

TAOM keeps every troop's 8 combat skills (Athletics, Riding, OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing) on a single deterministic curve: a **per-(level, group) baseline** plus a **per-culture modifier**. Two tools own this: `tools/rebalance_troops.py` *writes* skills onto the curve, and `tools/analyze_troop_balance.py` *reads* the current state into a per-culture balance overview (HTML + markdown + JSON) without touching anything. This doc covers both, the cultural-modifier system, and the 2026-06-24 full-roster rebaseline.

## Why This Exists

- **Vanilla / ad-hoc behavior:** Troop skills are hand-authored per-`NPCCharacter`, so same-tier units drift apart between cultures and across authoring sessions. Content added after a balance pass (new troops, culture revamps) ships off-curve.
- **TAOM requirement:** Cross-culture parity at each tier (an L26 Gondor infantryman ≈ an L26 culture-X infantryman, shifted only by the culture's deliberate identity), with elven factions intentionally above the line and orc factions below it.
- **Without this:** Post-#212 elites shipped at *half* their tier's skills (e.g. `iron_hills_noble_ironbreaker` at OneHanded 90 where the curve calls for 325), and three cultures (`goblin`, `mistymountainorcs`, `dale`) had no faction identity at all.

## Architecture

### The formula (single source of truth)

`tools/rebalance_troops.py` defines the reference curve:

```
final_skill = BASELINE[group][level][skill] + CULTURAL_MODS[culture].get(skill, 0)
            (then weapon specialization, EQUIPMENT-DRIVEN since 2026-07-13:
             carries crossbow + no bow        -> swap Bow <-> Crossbow
             name has pike/spear/sword/axe/…  -> ±15 flavour shifts (name-based, unchanged)
             carries 2H + no polearm, Pol>2H  -> swap Polearm <-> TwoHanded)
```

- **`GROUP_BASELINES`** — Infantry / Ranged / Cavalry / HorseArcher tables keyed by the 11 tier levels `{1,6,11,16,21,26,31,36,41,46,51}`. Troops at off-grid levels (e.g. L7, L13) are skipped (no reference).
- **`CULTURAL_MODS`** — per-culture skill deltas applied on top of baseline (the faction's identity). Keyed by the **filename culture** (`troops_<culture>.xml`), with two special cases in `detect_culture`: `iron_hills_*` ids (which live in the erebor file) → `iron_hills`, and `rhun_new` (the Rhûn file) → `rhun`.
- **`SKIP_TROOP_IDS`** — troops excluded from the formula entirely (genuine non-humanoid creatures + hand-tuned bespoke mounts): currently `cave_troll`, `harad_elephant_rider`, and `harad_mumakil_rider`.

### Equipment-driven weapon specialization (#340/#341, 2026-07-13)

Weapon detection was originally **name-keyword-only** (`crossbow`/`arbalest`/`naffatun` triggered the Bow↔Crossbow swap), which mis-statted every crossbowman named "Sharpshooter/Marksman/Scout/Sniper" (12 troops shipped Bow-top) and left two-hander troops named "Knight/Berserker/Champion" on the polearm-biased baselines (59 troops shipped Polearm-top — Cavalry L41 is 1H 310 / 2H 160 / Pol 340 by design). The fix:

- `taom_schema.build_item_class_registry(moduledata, game_modules)` maps item id → skill class. It must read **both** vanilla `<Item Type="…">` and Armory `<CraftedItem crafting_template="…">` — the entire install has zero `Type="TwoHandedWeapon"` items; every two-hander is a crafted item.
- `rebalance_troops.troop_weapon_classes(npc, item_classes)` collects the classes actually carried (weapon slots Item0–3).
- The Bow↔Crossbow swap fires iff crossbow-carried-and-no-bow (unambiguous: no troop carries both). The `naffatun` keyword is gone — it had wrongly swapped two javelin throwers.
- A total-preserving sanity post-pass swaps Polearm↔TwoHanded when a troop carries a two-hander, no polearm, and Polearm > TwoHanded. Idempotent; mixed carriers untouched; monotonicity unaffected (totals preserved).
- **The writer hard-fails without the game install** (`--game-modules`, default = the standard Steam path) rather than silently degrading to the name heuristic. The read-only analyzer degrades to name-only with a loud warning instead.
- Contract tests: `tools/tests/test_rebalance_equipment.py`.

**Known off-formula residuals** a `--dry-run` will always report: the hand-tuned `gondor_loss_noble{,_veteran,_sergeant,_warden,_captain}` line (5 troops, intentional — do not `--apply` over them without deciding their fate, tracked in #343) and 28 Mordor/Morannon partial-skill-block troops that report CHANGED but never produce byte changes (the regex writer only rewrites values already present).

**Hand-tuned, protected via `SKIP_TROOP_IDS`** (these no longer appear as residuals because the tool skips them outright): `iron_hills_noble_scout` / `_sharpshooter` / `_veteran_sharpshooter`, Crossbow 175 / 225 / 275 as of 2026-07-30. The formula derives Crossbow from level and `CULTURAL_MODS['iron_hills']` alone, which gave the noble line exactly the regular `ironpass_*` line's values (130 / 170 / 205) — no edge in the only skill the branch specialises in. If the noble/regular split is ever expressed as a modifier rather than a hand-tune, remove these three ids and let the formula own them again.

**Deferred (#343):** 108 troops carrying only 1H weapons with Polearm strictly top (+46 exact ties) need a 3-way redistribution decision, not a mechanical pair swap.

`tools/analyze_troop_balance.py` **imports these tables verbatim** — it never re-derives the curve, so the "ideal" and the "writer" can never disagree. It compares each troop's actual skills to `calculate_skills(...)` and reports the delta.

### Component diagram

```
GROUP_BASELINES + CULTURAL_MODS  (rebalance_troops.py — the curve)
        |                              |
   rebalance_troops.py            analyze_troop_balance.py
   (WRITES skills via regex,      (READS, imports the curve,
    preserves all formatting)      emits HTML/MD/JSON overview)
        |                              |
  troops_*.xml (16 files)        tools/reports/troop-balance/REPORT.{html,md} + .json
```

## The cultural modifiers

Deltas on top of baseline. Elves run high (elite at everything); orcs run low; men sit mild-positive. Authored to keep same-tier cross-culture parity within band.

| Culture | Ath | Rid | 1H | 2H | Pol | Bow | Xbow | Thr | Identity |
|---|---|---|---|---|---|---|---|---|---|
| rivendell | +35 | +30 | +35 | +40 | +40 | +40 | +40 | +40 | High-elf elite (strongest) |
| mirkwood | +45 | +5 | +40 | +30 | +30 | +50 | +50 | +50 | Wood-elf archers |
| lothlorien | +35 | +25 | +30 | +25 | +30 | +35 | +35 | +35 | Galadhrim (dead entry — see below) |
| iron_hills | +10 | −5 | +15 | +20 | +20 | · | +5 | +10 | Elite dwarves |
| erebor | +10 | −20 | +10 | +20 | +10 | · | · | +10 | Dwarf heavy infantry |
| **dale** | **+5** | **−10** | **+5** | **+12** | **+25** | **+12** | **+12** | **−5** | **Men of the North — best non-elf polearm + bows** |
| dunland | +20 | −5 | +5 | +5 | · | · | · | +15 | Wild hillmen |
| gondor | +5 | +5 | +10 | +5 | +5 | · | · | −10 | Elite men, balanced |
| umbar | +10 | −15 | +10 | +5 | · | · | · | · | Corsairs |
| rhun | +5 | +18 | · | · | +15 | −10 | −10 | −5 | Easterling cavalry/pikes |
| rohan | −5 | +20 | · | · | +10 | −5 | −10 | +2 | Riders of Rohan |
| harad | · | +15 | +5 | −10 | −5 | +10 | · | · | Southron horse + archers |
| isengard | +10 | +5 | +10 | +15 | +15 | · | +10 | +10 | Uruk-hai, strong |
| **isengard_orthanc** | **+18** | **+5** | **+22** | **+22** | **+20** | · | **+12** | **+12** | **Orthanc guard — Saruman's elite, the best NON-elf line (net +111)** |
| **dolguldur** | **+12** | **−5** | **+15** | **+25** | **+18** | **−5** | **−5** | **+10** | **Elite dark uruks (~Isengard-tier, 2H-heavy)** |
| **mordor_uruk** | **+10** | **−5** | **+12** | **+18** | **+12** | · | · | **+5** | **Mordor Black Uruks — elite line, between Gundabad & Dol Guldur (Bow/Xbow baseline = real archers)** |
| gundabad | +5 | −5 | · | +10 | +5 | −10 | −10 | +5 | Mountain orcs, poor ranged |
| **mistymountainorcs** | **+5** | **−5** | · | **+5** | **+3** | **−10** | **−10** | **+5** | **Cheap orc swarm, just below Gundabad** |
| mordor | −5 | −5 | · | +5 | −5 | −5 | −5 | +5 | Weak orcs |
| **goblin** | **−10** | **−15** | **−8** | **−5** | **−8** | **+15** | **−15** | **−5** | **Throwaway melee swarm — but dangerous archers (Bow +15, above Dale)** |

(Bold = authored/changed in the 2026-06-24/25 rebaseline work.) `detect_culture` id-routes elite sub-lines to their own modifier so they aren't dragged onto their file's base culture curve: `iron_hills_*` (in the erebor file) → `iron_hills`, `mordor_uruk_*` Black Uruks → `mordor_uruk`, `orthanc_*` (Saruman's elite guard in the isengard file) → `isengard_orthanc`. `lothlorien` is a **dead entry** — Lothlórien fields no troops of its own (full Rivendell reskin: `basic_troop=imladris_recruit`, party templates point at `rivendell_*`), so its troops rebaseline as `rivendell`. Kept as documented intent.

## The 2026-06-24 rebaseline

Goal: bring every troop back onto the curve after the post-#212 content drift, **without** retuning the baseline numbers themselves (Gondor already matches them exactly — they are the validated standard).

Tooling changes (`rebalance_troops.py`):
1. Authored 3 new modifiers (`goblin`, `mistymountainorcs`, `dale`), each independently proposed from the roster + lore and adversarially balance-checked against peer cultures.
2. Bumped `dolguldur` from near-neutral (net −5) to elite (net +65, 2H-heavy) so Sauron's uruks land at ~Isengard tier — strong enough to contest the bordering elf realms — instead of being nerfed to a weak curve.
3. Fixed the `rhun_new`→`rhun` key mismatch in `detect_culture` (the Easterling modifier had been silently un-applied).
4. Added `SKIP_TROOP_IDS` to exclude `cave_troll` + `harad_elephant_rider` from the formula.
5. Fixed a latent `UnicodeEncodeError` (a `Δ` char in the warning output crashed on Windows cp1252 stdout once enough >100 deltas appeared).

Result: **262 troops rewritten across 11 files** (the under-tuned cultures; dunland/harad/rivendell/rohan/umbar were already on-curve and untouched). The standout corrections were the #212 Erebor / Iron Hills nobles (e.g. `iron_hills_noble_ironbreaker` 580 → 1340). The mount-riders (warg/wolf) rebaselined as cavalry; the elephant rider + cave troll were preserved.

Verification: `validate_moduledata` PASS (zero broken refs); diff perfectly balanced (1,701 insertions / 1,701 deletions = skill-values-only, no structural change); overview outliers **186 → 14** (the 14 are benign Mordor partial-skill-block troops whose *present* skills are on-curve); **780 / 798 troops within ±25 of the formula** (up from 593).

## Level monotonicity & militia

A balance pass must never let a lower-level troop out-stat a higher-level one. `analyze_troop_balance.py` checks this (total-skill based, ±25 tolerance for weapon-spec noise) across two axes: **upgrade paths** (the troop you upgrade INTO must be higher-level and not weaker) and **within culture+group** (no lower level out-totals a higher level). Because the baseline is monotonic by construction and rebaselined troops match it, professional troops report **zero inversions**.

**Militia are deliberately excluded.** TAOM militia take the **level-21 baseline regardless of their actual level** (`is_militia` in `rebalance_troops.py`) — so a L6/L11 militia out-stats mid-level regulars. That's intentional: militia exist to make sieges and village defense costly, not to sit in a clean progression (user direction, 2026-06-24). The check skips them and reports the count; don't "fix" militia to satisfy monotonicity. Weapon-spec role-changes on upgrade (a melee troop → an archer) are also not flagged — the upgraded troop's total is ≥, it just specializes differently.

## Key Files

| File | Purpose |
|------|---------|
| `tools/rebalance_troops.py` | Writes skills onto the curve. `GROUP_BASELINES` + `CULTURAL_MODS` + `SKIP_TROOP_IDS` + `detect_culture`. `--dry-run` / `--apply`. |
| `tools/analyze_troop_balance.py` | Read-only overview generator. Imports the curve from `rebalance_troops.py`; emits HTML/MD/JSON. `--outlier-threshold N` / `--stdout`. Never writes troop XML. |
| `Main/_Module/ModuleData/troops/troops_*.xml` (×16) | The troop definitions (the data under management). |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` | Cross-referenced by the analyzer for weight-coverage gaps (army-composition weighting, not skills). |
| `tools/reports/troop-balance/REPORT.{html,md}` + `troop-balance.json` | Generated overview (gitignored under `tools/reports/`; regenerate any time). |

## How to review balance / run a rebaseline

1. **See where things stand (read-only):** `python tools/analyze_troop_balance.py --stdout`, then open `tools/reports/troop-balance/REPORT.html`. The heatmap parity matrix shows under-tuned (red) vs on-curve (green) vs elite (purple) per culture × tier; the data-quality section flags missing modifiers, dead entries, and excluded creatures.
2. **Adjust the curve:** edit `GROUP_BASELINES` (the power curve — affects every culture) or `CULTURAL_MODS[culture]` (one faction's identity) in `rebalance_troops.py`. To exclude a bespoke/creature troop, add its id to `SKIP_TROOP_IDS`.
3. **Preview:** `python tools/rebalance_troops.py --dry-run` — review the per-level deltas and the >100 warnings. Inspect **downward** changes especially (they may signal a too-weak modifier rather than over-tuned troops — that's how the Dol Guldur bump was found).
4. **Apply:** `python tools/rebalance_troops.py --apply` (regex-based — preserves all XML formatting/comments; only rewrites skill *values* already present in a `<skills>` block).
5. **Verify:** `python tools/validate_moduledata.py` (no broken refs), regenerate the overview (outliers should collapse), and `git diff --stat` (expect a balanced insert/delete count = values-only).

Save-compat: troop skills are read from XML at agent spawn, so a rebaseline applies to all troops in new *and* existing saves with nothing serialized per-troop-type — no save migration needed.

## Changelog

- 2026-06-25 — Orthanc elite: routed the `orthanc_*` guard line (Saruman's best, sword+shield, L26–41) to a new `isengard_orthanc` modifier (net +111) — now the best NON-elf line in the game, a clear step above the regular uruk-hai (+75), still far below elves. Only `troops_isengard.xml` changed (4 troops, +36 each). Same id-routing pattern as the Black Uruks. Surfaced by the 2026-06-25 deep review.
- 2026-06-24 — Black Uruk + goblin-archer follow-up: routed the `mordor_uruk_*` Black Uruk line to a new elite `mordor_uruk` modifier (net +52, between Gundabad & Dol Guldur — they were stuck on the weak Mordor-orc curve); raised goblin `Bow −15 → +15` so goblin archers are dangerous while the melee swarm stays throwaway (Uruks > Orcs > Goblins, with the goblin-archer exception). Only goblin + mordor files changed. Added a **level-monotonicity check** to `analyze_troop_balance.py` (upgrade-path + within-culture+group, militia-excluded): 0 inversions among professional troops.
- 2026-06-24 — Added the read-only `analyze_troop_balance.py` overview generator; full-roster rebaseline (262 troops / 11 files); authored `goblin`/`mistymountainorcs`/`dale` modifiers, bumped `dolguldur` to elite, fixed the `rhun_new`→`rhun` key mismatch, added `SKIP_TROOP_IDS` (cave_troll + elephant rider), fixed a latent `Δ` stdout crash.

## Related

- [troop-progression.md](troop-progression.md) — tier cap (6→10), volunteer cap, wage/recruitment tables.
- [troop-weight-system.md](troop-weight-system.md) — army-composition weighting (orthogonal to skills).
- [battle-balance.md](battle-balance.md) — tier power, casualty rates, blunt/cut ratios (GameModel side).
- [troop-tree-revamp.md](troop-tree-revamp.md) — #212 roster changes that drove most of this pass's corrections.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/armor-balance.md](./armor-balance.md)
- [docs/features/lord-perk-review.md](./lord-perk-review.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
