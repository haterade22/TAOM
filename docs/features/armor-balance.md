# Armor Balance

## Overview

TAOM's armor lives in the external `LOTRLOME_Armory` module (~2,800 items across 18 culture folders). This doc owns the armor balance system: the baseline + cultural-modifier curve, the **read-only analyzer** that reports drift, the **writing rebalancer** that applies the curve, and the **roster-derived tiering** method that anchors each item's tier to the troop that wears it. It is the armor analog of [troop-skill-balance.md](./troop-skill-balance.md) and [lord-perk-review.md](./lord-perk-review.md).

## Why This Exists

Two problems motivated this system (found during the 2026-06-30 armor audit):

1. **The rebalancer was aimed at a dead tree.** `tools/rebalance_armor.py` targeted `taommod/src/data/armory/` — a stale Node/TS web project (last touched 2026-03-11), **not** the `LOTRLOME_Armory` module the game loads. Confirmed by diff: the dead tree's first Gondor body item is `citidel_guard_armor1` (body 51); the live tree's is `faramir_armor` (body 35, with `material_type`). Any `--apply` against the dead tree was a silent no-op in-game. This is the live-vs-shadow trap (cf. `feedback_taom_map_live_vs_stale_shadow.md`).
2. **There was no way to see armor drift.** Troops and lords each have a read-only analyzer; armor had none — every rebalance to date was a blind write.

The audit also surfaced a dominant, recurring defect across cultures: **monolithic per-slot weight** — a slot frozen at one weight across all tiers, so a recruit's boots/bracers/chest weigh the same as a lord's. The analyzer makes that (and the integrity gaps) machine-detectable.

## Architecture

```
SLOT_BASELINES + CULTURAL_MODS   (rebalance_armor.py — single source of truth)
        │
        ├──► rebalance_armor.py   (WRITES: applies baseline+mod to the live armory; scoped/guarded)
        │
        └──► analyze_armor_balance.py  (READS: imports the curve, reports drift; never writes armor)
                    │
                    └──► tools/reports/armor-balance/{REPORT.html, REPORT.md, armor-balance.json}
```

The analyzer `import rebalance_armor`s the curve verbatim, so the two tools can never diverge (same pattern as `analyze_troop_balance.py` importing `rebalance_troops`).

### The curve

Per-tier, per-slot baseline (primary stat), with a per-culture additive **protection** mod and multiplicative **weight** mod. `final_primary = baseline[tier][slot] + protection`; `final_weight = baseline_weight × weight_mult`. Secondary stats (leg-on-body, arm-on-shoulder) get 60% of the protection mod.

Primary-stat baseline (from `SLOT_BASELINES`):

| Slot | light | medium | heavy | elite | lord |
|------|------:|-------:|------:|------:|-----:|
| body (`body_armor`) | 20 | 32 | 42 | 50 | 60 |
| head (`head_armor`) | 15 | 24 | 32 | 40 | 48 |
| leg (`leg_armor`) | 12 | 20 | 28 | 34 | 40 |
| arm (`arm_armor`) | 8 | 14 | 20 | 26 | 32 |
| shoulder (`body_armor`) | 5 | 8 | 12 | 15 | 18 |

Cultural mods (`CULTURAL_MODS`) express identity — e.g. dwarves high-protection/high-weight, elves high-protection/low-weight, orcs cheap/heavy-for-protection:

| Culture | protection | weight_mult | | Culture | protection | weight_mult |
|---------|-----------:|------------:|-|---------|-----------:|------------:|
| iron_hills | +5 | ×1.10 | | rivendell | +5 | ×0.70 |
| erebor | +4 | ×1.05 | | mirkwood | +5 | ×0.65 |
| arnor | +2 | ×1.00 | | gondor | +1 | ×1.00 |
| isengard | +2 | ×1.15 | | **dale** | +1 | ×1.05 |
| rhun | 0 | ×0.90 | | gundabad | 0 | ×1.15 |
| dol_guldur | 0 | ×1.10 | | mercenary | 0 | ×1.00 |
| rohan | −2 | ×0.90 | | dunland | −2 | ×0.85 |
| mordor | −1 | ×1.10 | | umbar | −1 | ×0.90 |
| harad | −3 | ×0.85 | | thenn | −3 | ×1.05 |
| troll | +8 | ×2.00 (boss — excluded) | | lothlorien* | +5 | ×0.70 |

`dale` was **added 2026-06-30** (it was previously absent → ran on the neutral default). `lothlorien*` has a mod but no live folder/troop (reconciliation item).

### Tier assignment: roster-derived, not keyword-guessed (the Phase-2 principle)

`rebalance_armor.detect_tier` currently **guesses** an item's tier from name keywords + a value-threshold fallback. This is brittle: a Dale `Archer Helmet A01` (15 armor) and `A03` (24 armor) both lack a tier keyword, so both fall to "light" and a blanket apply would flatten them to one value — destroying the progression.

The authoritative signal is the other direction: **an item's intended tier = the level of the troop that wears it** × which weight-line (light `a` / heavy `b`) the troop sits on. A level-6 troop's chest is "light" by definition; a level-31 troop's is "elite". Because **items are deliberately reused across troops** (not enough meshes for every soldier), a shared item anchors its tier to its **primary/lowest** wearer so it never over-arms the lower troops.

This roster walk is implemented in **`tools/derive_armor_tiers.py`** (Phase 2). It joins every `troops_*.xml` roster (equipment slot → item id, via `Item.<id>`; slots map `Head/Body/Leg/Gloves→arm/Cape→shoulder`) to the live armory by item id, anchors each item to its lowest wearer, and writes the map to `tools/data/armor_roster_tiers.json` + a report at `tools/reports/armor-balance/ROSTER-TIERS.md`.

Tier-signal precedence per item: (1) an explicit tier keyword in the id (`_light_`/`_med_`/`_heavy_`/`_elite_`/`_lord_`) — the author's own label; (2) the roster anchor band (lowest wearer level) for keyword-less items (the Dale case); (3) **unworn** — no troop references it (arnor, mercenary, thenn have no dedicated roster, so all their items fall here and keep name/value tiering).

Level→tier bands (calibrated on Dale ground truth + the owner's elite=31-51 decision): `≤13 light · 14-18 medium · 19-30 heavy · 31-51 elite`. The armor `lord` tier is hero-only (named lords/heroes, excluded from rosters) — no troop is roster-tiered `lord`.

Reading the map: **UNDER** = the item is weaker than its wearer's level implies (an under-progressed line — the primary actionable signal). **OVER** is often *intended* — a heavy `b`-line item worn by a heavy troop at its level, or a deliberately-strong culture (rivendell); the report's `Line` column + the culture's identity disambiguate, so OVER is reviewed, not auto-applied. The map computes the level-band target as a **reference** — whether to scale an under-progressed line up or accept it (e.g. Dale's flat `a`-line) is a Phase-3 design call.

## Configuration

| Knob | Where | Notes |
|------|-------|-------|
| Baseline curve | `SLOT_BASELINES` in `tools/rebalance_armor.py` | per-tier × per-slot primary + weight |
| Cultural identity | `CULTURAL_MODS` in `tools/rebalance_armor.py` | per-culture protection + weight_mult |
| Live armory path | `_default_armory_dir()` | `$BANNERLORD_GAME_DIR` override → Steam fallback → `--armory-path` |
| Apply scope | `PRESERVE_CULTURES` + `--cultures` / `--all` | blanket `--apply` is refused; see below |
| Hero/boss exclusion | `HERO_NAMES` + `EXCLUDE_ID_SUBSTRINGS` (analyzer) | excluded from all curve judgments |

### Apply-safety guard

`rebalance_armor.py --apply` **refuses a blanket run.** It requires either `--cultures a,b,c` (scoped) or an explicit `--all`. This protects the hand-authored cultures (`PRESERVE_CULTURES` = gondor, mordor, isengard, dol_guldur, gundabad, erebor, iron_hills) from being flattened by the keyword-tier guesser. Even when fixing a preserved culture's genuine bug (e.g. iron_hills arm slot), scope the apply to that culture.

## Workflow

```
1. python tools/analyze_armor_balance.py --stdout      # structural defects (monolithic/integrity/ceiling)
2. python tools/derive_armor_tiers.py --stdout         # roster-derived tiers: per-item under/over re-stat list
3. (open REPORT.html + ROSTER-TIERS.md)                # defects + the re-stat candidates
4. python tools/rebalance_armor.py --dry-run --cultures <c>   # preview a scoped re-stat
5. python tools/rebalance_armor.py --apply --cultures <c>     # write (scoped; never blanket)
6. python tools/analyze_armor_balance.py               # re-run: confirm the defect cleared
7. python tools/validate_all_troop_refs.py             # underwear-bug gate (no broken refs)
```

Never run `--apply` without first reading the analyzer report and a dry-run.

## Key Files

| File | Purpose |
|------|---------|
| `tools/rebalance_armor.py` | WRITES. Curve source of truth; repointed at live armory 2026-06-30; scope-guarded. |
| `tools/analyze_armor_balance.py` | READS. Per-culture HTML/MD/JSON report; monolithic + integrity + ceiling detection. |
| `tools/derive_armor_tiers.py` | READS. Roster→item→tier join (Phase 2); reuse-anchored; under/over re-stat candidates. |
| `tools/data/armor_roster_tiers.json` | The derived map (item → wearers → anchor → tier → current vs target). |
| `tools/reports/armor-balance/{REPORT,ROSTER-TIERS}.{html,md}` | The viewable reports (regenerated each run). |
| `tools/validate_all_troop_refs.py` | Cross-checks troop→item refs (underwear-bug gate). |
| Live armory `…/LOTRLOME_Armory/ModuleData/LOTRLOME_items/<culture>/*.xml` | The only tree the game loads. |

## What the analyzer flags

- **MONOLITHIC weight/armor** (ERROR) — a slot's weight or armor frozen at one value across ≥4 combat items. The #1 recurring defect; per decision #5, per-tier weight scaling is treated as a project-wide requirement, so any monolithic slot fails.
- **Data integrity** — missing `material_type` (ERROR) / `modifier_group` (WARN); 0/None combat armor (WARN); helmets missing hair/beard cover (WARN); legs/arms missing cover attrs (INFO).
- **Ceiling shortfall** (WARN) — top combat armor well under the culture's elite target (a near-full-tier gap, not a 1-2 point rounding miss).

It does **not** judge qualitative identity (e.g. "rhun weighs too much for cavalry") — that stays a human call. The analyzer is structural; the per-culture identity verdicts live in the audit.

## Current state (2026-06-30 baseline)

18 cultures, ~2,800 items: **21 errors, 21 warnings** (down from 25 errors after the harad fix). The analyzer independently reproduced the audit's structural findings (iron_hills arm 25/3.5, mirkwood no progression, etc.).

**Phase 3 progress:** `harad` is fixed — `--weights-only` laddered its 4 monolithic-weight slots (head/body/arm/leg) onto the tier curve with armor byte-untouched. `dol_guldur` pauldron_med_c `body_armor=8`→30 typo fixed (matches its `_med_a/b` siblings; was weaker than the lights).

**Owner decisions encoded (2026-06-30):** elite = troop levels 31-51 (→ bands above; lord = hero-only); dunland are raiders/skirmishers → light (`weight_mult` 0.95→0.85); rhun has the best cavalry in the game → mobile (`weight_mult` 1.00→0.90).

**Across-the-board sweep complete (2026-06-30).** Armory-wide: **20 analyzer errors → 2**, `validate_all_troop_refs` PASS (no item id changed). Method: `--no-lower-armor` full re-tiers for cultures with broken armor progression (dale, rohan, arnor, mirkwood — 0 stats lowered, only raised); roster `--weights-only` for weight-only issues (rhun mobile, rivendell light, gundabad, isengard — armor byte-untouched); targeted preserve fixes (iron_hills arm 25→14/18/22/25/31, dol_guldur leg weight, erebor 62 beard-covers, the Dain hero rebase above his troops); thenn body spread via keyword re-tier.

- **Clean (8):** dale, erebor, gondor, iron_hills, mordor, rhun, rohan, troll.
- **Minor (0 errors):** arnor, dol_guldur, dunland, gundabad, harad, isengard, mercenary, thenn.
- **Remaining 2 errors:** mirkwood + rivendell shoulder slots are monolithic-*armor* (few shoulder items, all at the elite value). Clearing them requires lowering low-tier shoulder coverage — vetoed by "do not nerf" — so they are flagged-not-fixed (arguably a heuristic false-positive for cultures with limited shoulder meshes).
- **Deferred:** mercenary's 37 items are mis-tagged `Culture.gondor` (pool into Gondor merchants). Retagging needs the neutral-pool culture decision; left untouched rather than guessed.

Full pre-sweep backup at `…/scratchpad/FULL_armory_backup_*`. Remaining design decisions (optional): mordor relabel-vs-split; whether to nerf the 2 elf shoulder slots; the mercenary retag target.

## Dependencies

- `rebalance_armor.py` (curve) — the analyzer imports it.
- The live `LOTRLOME_Armory` module install (read at runtime).
- Python stdlib only (`xml.etree`, `statistics`, `json`).

## Tests / verification

The analyzer is read-only and self-verifying: running it against the live tree must reproduce the known defects (iron_hills arm monolithic, harad body all 9.5, dale clean). No unit-test harness yet; the regression check is "re-run and confirm the executive summary matches this doc's baseline."

## How-To

**Fix a culture's monolithic weight slot:** use `rebalance_armor.py --apply --weights-only --cultures <c>`. It ladders ONLY the weight to each item's armor-derived tier (`tier_from_value`) and leaves armor + material untouched, so it can't mangle hand-tuned armor through the keyword detector. It is guarded — it only touches a slot that is **currently monolithic-weight**, so it can never collapse an already-varied slot (the harad-shoulder regression, fixed 2026-06-30). Back up the culture folder first; re-run the analyzer to confirm the error cleared.

**`--weights-only` applicability (important):** it is correct ONLY when the slot's armor is varied AND correctly tiered, because weight follows armor. It is the right tool where the armor is already right and only the weight is frozen (harad body/head/leg). It is the WRONG tool when: (a) the armor is itself uniform — it can't ladder (no-op, e.g. rohan's combat boots, iron_hills arm), those need an armor mid-tier first; or (b) the armor is over-tiered for the culture's intended weight class — it would propagate that into heavier weights (e.g. dunland body 30-45 → 12-21kg, the opposite of the light-raider intent). Cultures in (b) need the identity decision (lower armor + material) before any weight pass.

**Re-tier a whole culture from its rosters:** `python tools/derive_armor_tiers.py` (refresh the map), then `rebalance_armor.py --dry-run --tier-source roster --cultures <c>` and read the proposed changes. Each worn item is re-stated to its troop-level tier (hero/unworn skipped), capped at elite. Add `--no-lower-armor` for a "do not nerf" culture (raises under-tiered items, never reduces, preserves material). Add `--weights-only` to set weight by roster tier without touching armor (e.g. a mobility trim that spares elite plate). Always dry-run first — the dunland dry-run caught a wrong-direction change before applying.

**Add/adjust a cultural identity:** edit `CULTURAL_MODS[culture]`; dry-run; the analyzer's "In CULTURAL_MODS" column flags any culture still on the neutral default.

**Exclude a new hero/boss item from the curve:** add its id substring to `EXCLUDE_ID_SUBSTRINGS` or its name to `HERO_NAMES`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/starting-equipment-tuning.md](./starting-equipment-tuning.md)

<!-- backlinks-end -->
