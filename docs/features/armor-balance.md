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

**Since 2026-09-13 (#583) the curve is the kingdom-cap model in the next section.** The baseline
tables and cultural protection mods below remain the model for the civilian tier, for weights
(`baseline_weight x weight_mult`, unchanged) and for any folder without a cap (the troll); they are
kept here because the two-tier invariant test still pins them.

### The kingdom-cap curve (2026-09-13, #583)

Each kingdom's armour power is one number, the chest (`body_armor`) of its elite band, set by the
maintainer; the other slots and the bands below are fixed ratios of it
(`rebalance_armor.KINGDOM_CAPS`, `SLOT_CAP_RATIO`, `BAND_RATIO`, `cap_value`):

| Cap | Kingdoms | Chest | Helmet .9 | Bracer .6 | Pauldron .6 | Greaves .5 |
|----:|---|---:|---:|---:|---:|---:|
| 70 | Erebor, Iron Hills | 70 | 63 | 42 | 42 | 35 |
| 68 | Rivendell, Lindon | 68 | 61 | 41 | 41 | 34 |
| 63 | Mirkwood | 63 | 57 | 38 | 38 | 32 |
| 60 | Lothlorien (no item yet) | 60 | 54 | 36 | 36 | 30 |
| 57 | Gondor, Rhun, Black Numenoreans, Arnor | 57 | 51 | 34 | 34 | 29 |
| 49 | Gundabad | 49 | 44 | 29 | 29 | 25 |
| 46 | Dol Guldur, Khand (no item yet) | 46 | 41 | 28 | 28 | 23 |
| 45 | Isengard | 45 | 41 | 27 | 27 | 23 |
| 44 | Dale, Harad, Umbar, mercenary | 44 | 40 | 26 | 26 | 22 |
| 43 | Mordor Black Uruks | 43 | 39 | 26 | 26 | 22 |
| 40 | Rohan, Dunland | 40 | 36 | 24 | 24 | 20 |
| 38 | Mordor orcs, Misty Mountain orcs, goblins (one shared kit) | 38 | 34 | 23 | 23 | 19 |
| 35 | Thenn | 35 | 32 | 21 | 21 | 18 |

Bands: light 0.40, medium 0.64, heavy 0.84, elite 1.0 of the elite value, rounded half up; the lord
band equals the elite band, so nothing but named hero kit (`HERO_NAMES`, `EXCLUDE_ID_SUBSTRINGS`)
sits above a kingdom's cap. The band an item sits in is the band of its LOWEST battle-set troop
wearer (`level_to_band`: light to 13, medium 14 to 18, heavy 19 to 30, elite 31 up), decided by
`--tier-source roster-first`; an id keyword decides only for kit no troop wears, and kit with
neither is left alone. Civilian sets never anchor, and the ladder-exempt troops
(`taom_schema.Validator._ARMOUR_LADDER_EXEMPT`: the Ithilien ranger, the troll, the Harad mount
riders) wear their kit without anchoring it, so the ranger's hood stays a hood. Since 2026-09-25
two narrower mechanisms sit beside it: noble-line troops anchor a band up, and a `(troop, item)`
pair excuses one piece (see the 2026-09-25 subsection below).

Sub-lines that share a folder are routed by id prefix (`LINE_PREFIXES`): `sk_md_num_` and
`sm_md_num_` to the Black Numenorean cap, `sk_uruk_mordor_` to the Black Uruk cap, `sk_md_mor_`,
`sk_md_orc_` and `sk_gn_orc_` to the orc cap (the goblin and Misty Mountain rosters wear those
same items), `sk_dg_` in the rhun folder to Dol Guldur, `urukscout_` to Isengard, `ar_ardunian_`
to Umbar. The `md_num` restat exclusion is lifted: the Black Numenorean set wanted level-anchored
tiering all along and now has its own cap. The Dol Guldur troop line is NAMED "Khamul ..." and is
not his kit, so `HERO_NAME_FALSE_POSITIVE_PREFIXES` keeps it on the curve.

A secondary stat an item carries (a Gondor chest's `arm_armor`, a Rhun chest's `leg_armor`, a
cape's `arm_armor`) is scaled by the same factor as its primary, so each folder keeps its
convention. Weights and `material_type` are not touched (`--keep-weights`,
`--keep-material-type`); `modifier_group` follows the band, and the three extremity slots (arm,
leg, shoulder) share one loot table whose medium row is cloth (+5), because at 0.5 and 0.6 of a
35 cap a +7 roll on a medium piece would pass the elite one
(`analyze_armor_balance.check_kingdom_curve_invariant`, pinned green for every cap). That invariant
is on the primary stat only. A secondary keeps its item's own ratio, and where the ratio is small
the flat modifier bonus outgrows the tier gap: a legendary medium Isengard pauldron rolls arm 5 + 5
against the elite one's 7. The live sweep in `analyze_armor_balance.py` judges every governed stat
per item with its own modifier group and listed 50 roster-backed secondary cases on 2026-09-13 (40
chest `leg_armor`, 10 shoulder `arm_armor`); the restat preserves each ratio, so a case present
before it is present after it (the pauldron pair was inverted in the backup too). They belong to
the roster pass; a secondary floor per band would be a curve change, not a fix. Secondaries are
rounded half up from the item's CURRENT ratio at each apply, so an item that passed through two
bands can sit one point off a single direct pass (`rivendell_torso_heavy_tier3_silvergoldb`, leg 34
where a direct run gives 33); the property that holds is that a dry run after any apply plans
nothing.

`derive_armor_tiers.py`'s map follows the same precedence (anchor first, keyword for unworn kit,
civilian keyword kit civilian whoever wears it), so its `tier`, `target` and `status` columns
describe what the restat does; until the deep review of 2026-09-13 the map was keyword first and
mislabelled 682 of 1,861 worn items. Two engine facts checked on the installed 1.4.8 DLLs: no
armour stat is clamped on load (`ArmorComponent.Deserialize` is a plain `int.Parse`), and the
item's display tier and price follow `DefaultItemValueModel.CalculateArmorTier`
(`(1.2 head + body + leg + arm) x type x 0.1 - 0.4`), so an Erebor chest at 70 now shows the top
"Armor Tier" in its tooltip and prices accordingly. No shop or loot path checked bans an item by
that tier, but the tier is not inert: `DefaultItemCategorySelector` files an armour item as
Garment, LightArmor, MediumArmor, HeavyArmor or UltraArmor by it (Tier2 up to Tier5), and
`WorkshopsCampaignBehavior` picks what a workshop produces by category, so an item whose tier moved
(an Erebor light chest went from Tier3 to Tier4, MediumArmor to HeavyArmor) changes which workshop
makes it and at what price.

**Applied 2026-09-13** to the Steam tree and the `lotraom-assets` mirror (byte-identical before
and after), backups `.bak-kingdomcurve-583` once per file: 2,510 items on the first run, 316
untouched (hero kit, civilian and unworn keyword-less kit), then 7 more after the ladder repair
below; a third run plans nothing. Command:

```
python tools/derive_armor_tiers.py
python tools/rebalance_armor.py --apply --all --tier-source roster-first --keep-weights --keep-material-type --backup-tag kingdomcurve-583
python tools/rebalance_armor.py --apply --all --tier-source roster-first --keep-weights --keep-material-type --backup-tag kingdomcurve-583 --armory-path "E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory\ModuleData\LOTRLOME_items"
```

The re-curve exposed 58 promotion edges where the child wore kit anchored low by a shared wearer
(`UPGRADE_ARMOUR_REGRESSION`); `fix_upgrade_armour_regressions.py --apply` resolved all 58 with 197
slot swaps in 12 troop files, and one derive + restat iteration after it was stable. Post-state
medians (total armour, capstone tiers): Gondor T9 251 (was 194), Erebor T9 335, elves T9 384,
Mordor T9 270, Rhun T9 288, Dol Guldur T9 243, Gundabad T8 248, Isengard T8 209, Rohan T8 203,
Dale T6 190, Dunland T6 165, Harad T6 174, Umbar T6 240 (it wears Black Numenorean pieces, which
carry the 57 cap), goblins T7 110. Left for the roster pass, as the cap-scaled
`CROSS_CULTURE_ARMOUR_INVERSION` cells: goblin T7, Mirkwood T7 to T10 (its tree shares a handful
of items across every tier), Lindon and Rivendell T10 (the golden-flower fan-out under its own T9);
also the vanilla `aserai_scale_armor_on_chain` (51) on Harad's camel lancer, out of the Armory's
reach.

Legacy baseline (primary stat, from `SLOT_BASELINES`; the fallback and the weight ladder):

| Slot | light | medium | heavy | elite | lord |
|------|------:|-------:|------:|------:|-----:|
| body (`body_armor`) | 20 | 32 | 42 | 50 | 60 |
| body (`leg_armor`, secondary) | 10 | 16 | 22 | 28 | 36 |
| head (`head_armor`) | 15 | 24 | 32 | 40 | 48 |
| leg (`leg_armor`) | 12 | 20 | 28 | 34 | 42 |
| arm (`arm_armor`) | 8 | 14 | 20 | 26 | 34 |
| shoulder (`body_armor`) | 5 | 9 | 13 | 19 | 25 |
| shoulder (`arm_armor`, secondary) | 3 | 6 | 11 | 17 | 22 |

Secondary rows were previously undocumented (they exist only in code). Civilian is omitted above but
is part of the curve; **shoulder civilian `arm_armor` is `0` on purpose** — see the invariant below.

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

### The two-tier invariant (modifier-aware curve, 2026-07-31)

The curve above describes **base** stats, but the player never only sees base stats. Bannerlord item
modifiers add a **flat** armor bonus — `legendary_plate +12`, `chain +9`, `leather +7`, `cloth +5`,
`cloth_unarmoured +3` — and `EquipmentElement.GetModifiedBodyArmor()` / `GetModifiedArmArmor()` apply
it **independently to every nonzero stat**, each guarded by `num > 0`. A two-stat cape therefore takes
the bonus twice, and a stat of exactly `0` is **modifier-immune**.

The old shoulder curve spanned 16 points across all six tiers — less than a single `legendary_plate`
roll — so a legendary tier-2 pauldron (9/9 → 21/21) out-armored a plain tier-6 one (20/14). The curve
now satisfies, for every slot, governed stat, tier pair `(n, n-2)` and cultural protection value:

```
base[n] > base[n-2] + legendary(modifier_group[n-2]) + VARIANT_CAP
```

Beating the **adjacent** tier on a great roll is intended loot excitement and is never flagged;
beating one **two or more** tiers up is the defect.

Three constants enforce it, all in `rebalance_armor.py`:

| Constant | Purpose |
|---|---|
| `LEGENDARY_ARMOR` | The native ladder, mirrored from `Native/ModuleData/item_modifiers.xml`. Pinned against the live install by a test, so an engine bump can't move it silently. |
| `SLOT_MODIFIER_GROUPS` | Per-slot loot-roll magnitude. Shoulders cap at `chain` (+9) instead of `plate` (+12): the native deltas are sized for chest-scale bases (30-60), not cape-scale (2-25). **`material_type` is deliberately NOT changed** — it drives hit sounds/FX and stays lore-correct; the engine reads the two attributes independently. |
| `VARIANT_CAP` | `extract_variant_number()` maps roman numerals to `+0..+17` straight into the stat. Uncapped it eats the invariant margin, which runs as thin as +1. |

`GOVERNED_STATS` replaces the single-stat view: shoulder now governs **both** `body_armor` and
`arm_armor`. `_get_primary_stat` still returns one scalar (14 call sites depend on it) but reads the
head of that table; use `governed_stats()` for any balance judgment. Shoulder `arm_armor` being
invisible to the primary lookup is exactly the blind spot that let the inversion ship.

**Regression guards:** `analyze_armor_balance.check_curve_invariant()` is a pure function of the
constants (reads no XML), runs before the per-culture loop, and exits non-zero on violation.
`_check_tier_inversions()` reports the *item-level* version against the live tree, preferring roster
tiers over the keyword guess and splitting severity (roster-backed = ERROR, keyword-tiered = WARN).
`tools/tests/test_armor_curve_invariant.py` pins the whole thing, including the four per-culture
generators, which carry their own copies of the tier table and do **not** import the curve.

### Tier assignment: roster-derived, not keyword-guessed (the Phase-2 principle)

`rebalance_armor.detect_tier` currently **guesses** an item's tier from name keywords + a value-threshold fallback. This is brittle: a Dale `Archer Helmet A01` (15 armor) and `A03` (24 armor) both lack a tier keyword, so both fall to "light" and a blanket apply would flatten them to one value — destroying the progression.

The authoritative signal is the other direction: **an item's intended tier = the level of the troop that wears it** × which weight-line (light `a` / heavy `b`) the troop sits on. A level-6 troop's chest is "light" by definition; a level-31 troop's is "elite". Because **items are deliberately reused across troops** (not enough meshes for every soldier), a shared item anchors its tier to its **primary/lowest** wearer so it never over-arms the lower troops.

This roster walk is implemented in **`tools/derive_armor_tiers.py`** (Phase 2). It joins every `troops_*.xml` roster (equipment slot → item id, via `Item.<id>`; slots map `Head/Body/Leg/Gloves→arm/Cape→shoulder`) to the live armory by item id, anchors each item to its lowest wearer, and writes the map to `tools/data/armor_roster_tiers.json` + a report at `tools/reports/armor-balance/ROSTER-TIERS.md`.

Tier-signal precedence per item, since #583 (2026-09-13): (1) the roster anchor band, the lowest BATTLE-set troop wearer's level, ladder-exempt troops never anchoring; (2) an explicit tier keyword in the id (`_light_`/`_med_`/`_heavy_`/`_elite_`/`_lord_`) for kit no troop wears, with `_civ`/`civilian` ids civilian whoever wears them; (3) unworn and keyword-less, no tier. Until 2026-09-13 the keyword came first, which is how the Fountain Guard's `_heavy_` helmet, worn only at level 46, stayed on the heavy row, and why the map mislabelled 682 of 1,861 worn items against what `--tier-source roster-first` writes.

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
   #   --materials-only  writes ONLY material_type + modifier_group (no armor, no weight);
   #                     safe with --all, skips hero kit. Run it before any curve pass.
   #   --keep-materials  freezes them during a full re-stat (this used to be implied by
   #                     --no-lower-armor, which is how ~1000 items kept an unearned `plate`).
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
| `tools/analyze_kingdom_armour.py` | READS. Cross-culture overview by engine tier (#581): matrices, inversions, ceilings, curve view, gate preview. Reports to `tools/reports/kingdom-armour/`. |
| `tools/taom_schema.py` `cross_culture_armour_inversions` | The `CROSS_CULTURE_ARMOUR_INVERSION` rule as a pure function; the validator and the overview's gate preview both call it. |
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

## Gondor/Mordor parity pass (2026-07-13, #342)

User reports "Mordor armor beats Gondor" confirmed and fixed. `tools/oneoff/fix_gondor_mordor_armor_parity.py` (one-off; reuses the curve via `import rebalance_armor`) applied 371 stat changes to the live tree, backed up as `*.bak-parity-20260713`:

- **Mordor cap-only (153):** items worn *exclusively* by Mordor troops pulled down to the Mordor (−1) curve. The `sk_uruk_mordor_*_heavy_*` Black Uruk set — the pending relabel-vs-split decision above — was resolved as **roster-ELITE** (worn L26–36): bracers 30→25, pauldrons 25/20→14/11, greaves kept at 30 (≤ elite 33). Uruk `_medium_` kit capped at medium (bracers 20→13, pauldrons 15/12→7/7); the L11-anchored `chainmail_captain_*` capped at roster-light (35→19).
- **Shared-pool constraint:** `sk_gn_orc_*` + `sk_md_orc_*` (also worn by goblin/mistymountainorcs/isengard), `urukscout_*` (isengard, misfiled in mordor/), and `ar_ardunian_*` (umbar) are NOT capped — nerfing them would hit other cultures as collateral. Ties on shared kit are broken by the Gondor top-up instead: Gondor leads shared kit by 1, Mordor-exclusive kit by 2.
- **Gondor top-up-only (218):** keyword-tiered stats sitting exactly at the plain baseline raised to the Gondor (+1) curve (e.g. heavy chest 42→43, elite helmet 40→41). Off-pattern regional specials untouched.
- **Roster fixes (repo `troops_{gondor,mordor}.xml`):** L6 Gondor levies got the light helmet (they had none — orc recruits out-armored them); L16 Gondor line infantry/nobles upgraded from light gloves/greaves to medium (they wore med chest+helm with light extremities, losing to fully-slotted orc medium kit); mirrored `gondor_militia_archer`/`_veteran_*` topped up; `mordor_warg_rider` (L6) dropped its medium chest + cape/gloves to band-correct light.

Post-fix verification: Gondor > Mordor at every slot×tier max (uruk set judged as elite), and per-troop total armor Gondor-dominant (median AND max) at every shared level band (L6–L36). `validate_moduledata.py` PASS; analyzer still 2 errors (the pre-existing elf-shoulder flags). In-game smoke owed (restart required — armor stat changes only load at launch).

## Dale ladder repair (2026-09-04, #541)

Dale's four-tier per-line ladder (`a01..a04` bronze, `b01..b04` silver) had been statted on a
different axis by `generate_dale_armor.py`, which treats the eight suffixes as one light-to-elite
ladder. A roster-tiered restat could not repair it on its own because the tier map anchors an item
to its LOWEST wearer, and three low troops wore the mid items (the L11 Squire in `a02`, the L16
militia veterans in `a03`). Those rosters moved first (`a01`, and the silver `b01`), then
`rebalance_armor.py --cultures dale --tier-source roster --no-lower-armor --keep-materials` raised
`sk_dale_*_infrantry_a02`, `sk_dale_*_infrantry_a03` and `sk_dale_*_archer_a03` to the heavy row,
applied to the Steam install and to `E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory`. Raise-only,
so the over-target silver line the roster report lists was left where it is. The same pass restatted
the two Dunland `wulf_helmet_medium_*` from 45 (the lord row) to 22: an item value that made every
L21 Dunlending an armour downgrade from the L16 warriors. Equipment ladders are now gated per upgrade
edge by `UPGRADE_ARMOUR_REGRESSION` (see `troop-skill-balance.md`, "Armour is a ladder too").

## Kingdom armour ladder: cross-culture (2026-09-12, #581)

A report that a Gondor tier-9 troop wore less armour than a Dunland tier-5 troop. Every tool
above judges items within one culture or one upgrade edge, so nothing had ever put the kingdoms
side by side by tier. `tools/analyze_kingdom_armour.py` does that now (read-only, reports under
`tools/reports/kingdom-armour/`, gitignored like the other analyzer output), and the validator
carries a `CROSS_CULTURE_ARMOUR_INVERSION` warning so the class stays visible.

**How a troop is measured.** The four engine regions (head, body, arm, leg), each summed over the
five armour slots exactly as `Equipment.GetHeadArmorSum` / `GetHumanBodyArmorSum` /
`GetArmArmorSum` / `GetLegArmorSum` do (v1.4.8 `Equipment.cs:271-325`): a chest contributes arm
armour, a cape body armour. Each region is the mean over the troop's battle sets, an unfilled
slot counting 0, civilian sets excluded; the total equals `fix_upgrade_armour_regressions.total`.
Troops are placed by ENGINE tier, `clamp(ceil((level - 5) / 5), 0, 10)`, the number the party
screen shows, not by the curve's five level bands. Creature troops, the two bespoke mount riders
and any troop without a `level=` are excluded; militia and standalone troops are tagged; the
bare-chested-by-design troops are compared on Head, Gloves and Leg only; the validator's
`_ARMOUR_LADDER_EXEMPT` troops leave the matrices, the pair list and the gate together and stay in
the per-culture troop tables tagged `ladder_exempt` (the first draft kept them in the pairs, and
the Ithilien ranger topped every worst-pairs list as if it were a live regression). The per-slot
draw is the campaign spawn path's behaviour: `Equipment.GetRandomEquipmentElements` redraws per
slot whenever the seed is not -1, and `CharacterHelper.GetPartyMemberFaceSeed` never yields -1
(`.claude/rules/troops.md` carries the exception, settlement guards). In the ceiling tables an
unworn item's tier is judged on the curve of the Armory FOLDER it lives in, because that is the
curve it was statted on: Umbar wears Harad's folder (-3) while carrying its own -1, and judged on
Umbar's curve Harad's elite pieces read as heavy and dropped out of Umbar's reserve list. Items
worn only by an excluded troop (the troll plate on `cave_troll`) count as worn, so they appear in
no reserve list, and their folder counts as worn by nobody, so they appear in no ceiling table. The
curve-view aliases (`dolguldur`, `rhun_new`, `lindon`, `goblin` to the folder each wears) are for
the armour curve only; the skill curve in `rebalance_troops.detect_culture` keeps goblin as its own
weaker culture, on purpose. Since the Codex pass of 2026-09-13 an unworn item's tier is also routed
by its id, so a `sk_dg_` helmet in the rhun folder is judged on Dol Guldur's cap (62 Dol Guldur items
had read as Rhun heavy and left Rhun's reserve list; it is 176 items, not 114), and the report ends
with two observation tables for the roster pass, not findings: **kit off the culture's line** (a
worn item whose line cap differs from the wearer's own; a `mordor_num_` or `mordor_uruk_` troop is
held to its own line, as `tools/ranged_ladders.json` routes it) and **uncurved kit above the
ceiling** (a vanilla item, or one from a folder off the curve, whose primary stat sits above the
culture's elite value for the slot; no restat reaches it). On 2026-09-13: 162 rows over 33 troops
(Isengard orcs in Mordor orc kit 54 rows, Rhun troops in Dol Guldur's `sk_dg_` kit 53, Mordor
militia in Black Uruk kit 36, Umbar nobles in Black Numenorean plate 15, the elves in Gondor kit
4) and 11 uncurved rows (Dunland's vanilla `tall_helmet` 38 on four troops and `plumed_helmet` 47
over an elite value of 36; Harad's `aserai_scale_armor_on_chain` 51 over 44 and
`strapped_mail_chausses` 23 over 22 on three troops).

**What the first run found (854 troops, 16 cultures).**

- The literal pair is not inverted: the weakest Gondor T9 (`gondor_ith_moon_guard`, L46) totals
  180, the strongest Dunland T5 (L26 nobles) 163. The head slot is: Dunland T5 helmets are 40 and
  T6 helmets 45, while Gondor's head median is 33 at every tier from T4 to T9 and its best helmet
  in the whole folder is 41 (Dale 41, Dunland 45, Isengard 46, Mordor 50, Rohan 50, Rhun 56,
  Erebor 78). Dunland T6 (182) beats Gondor's T9 Moon Guard (180) and five of its twelve T8s.
- Dunland is not the outlier; Gondor is. At threshold 20 and a two-tier gap the run lists 7,803
  troop pairs over 150 culture pairs, and Gondor is the weaker side of 2,766 of them (goblin next
  at 998; Dunland is the stronger side of 43, none of them against Gondor: at threshold 0 a
  Dunlending tops a Gondor troop two or more tiers up in 61 pairs, the widest by 17 points).
  Gondor's T8 median (177) sits under the T6 median of the elves (280), Erebor (228),
  Isengard (215), Dale (202), Dol Guldur (195), Umbar (190), Rhun (188) and Gundabad (184); its T5
  median (150) is above only Goblin-town (126), Mordor (126), Rohan (128) and Rhun (149) among the
  fifteen cultures with a T5 cell.
- The curve's one "elite" row for L31 to L51 means Gondor's T6 through T10 all target 221 and
  land at 171, 172, 177, 194 and 125 (the ranger). The curve view's leg column reads 31 to 35 under
  target for Gondor at every tier from T4, but that view is a generic benchmark, not a per-item
  target (the culture's default line at the troop's band, every slot filled, secondaries at the
  legacy proportion; a Black Numenorean in `troops_mordor` is held against the orc cap), and part
  of the leg gap is a convention, not a deficit: the
  curve's body row carries a `leg_armor` secondary and Gondor chests carry `arm_armor` instead (1
  of 116 has any leg armour; Dunland, Erebor, Rohan and Dale are the same, Rhun and Isengard carry
  it). Read "d leg" against the culture's own convention.
- The Armory holds a reserve. Gondor has 20 unworn elite/lord-row items: seven helmets at 40
  (Dol Amroth foot, cavalry and warden, Linhir lord), six chests at 50 to 51 (Cair Andros, Dol
  Amroth B, Linhir, Lond Galen, Osgiliath, Arndir), five elite capes, the Serelond lord bracer and
  greaves. Nothing sits above body 51 / head 41 in the folder, so re-slotting can lift the T8/T9
  capitals to the folder ceiling (the Serelond set: 41 / 51 / 20 / 27 / 35, all worn) and no further.
- The gate warns on four cells: `goblin/tier7` (135 vs a 178 field at T5), `gondor/tier9` (194 vs
  220 at T7), `lindon/tier10` and `rivendell/tier10` (205 vs 227 at T8, the golden-flower fan-out
  capstones at 191 under their own T9 at 376). The exemptions are `cave_troll`, the two Harad
  riders and `gondor_ithilien_ranger` (a light ranger kit by design; without it Gondor T10 warns too).

**The gate.** Per (culture, engine tier) cell, on medians: a cell warns when its median total sits
more than 20 points (about 0.4 of one curve row) under the median of the other cultures' medians two
tiers lower, with at least three other cultures holding such a cell. Per-troop pairs were rejected
(thousands, unreadable) and culture-pair medians too (the elves are twice everyone by design). A
culture uniformly OVER the field is not flagged; the report's pairwise section shows that side. The
arithmetic is one pure function, `taom_schema.cross_culture_armour_inversions`, which the report's
"gate preview" section calls with the validator's own constants.

**What was decided (2026-09-13, #583).** The three options on the table were (A) re-slot rosters
only, (B) restat the top-tier items, (C) rebuild the curve. The maintainer took (C) in its own
form: a chest cap per kingdom with fixed slot and band ratios, applied to the whole Armory (the
"kingdom-cap curve" section above), followed by the ladder repair on the 58 promotion edges the
re-curve exposed. The gate scales every item to the 57 reference cap of its own line since
then, because kingdoms now differ in armour power by design.

## Mesh-tier ladder: which artist tier a troop level may wear (2026-09-16, #609)

A screenshot: `[Gundabad] Uruk Medium Chest IV` at 31 body armour (tier 6, 14.9 kg) beside
`[Gundabad] Uruk Lord Chest III` at 20 (tier 4, 25.3 kg). The kingdom-cap curve above had priced
both correctly for the rosters it was given: the six `sk_gb_uruk_chest_lord_*` were worn by the
level-11 snaga and hunter (the 2026-05-19 fan-out, `b88a25e7`, padded three low troops with the
whole lord line as extra battle sets), so the line anchored at the light band, 49 x 0.40 = 20,
while the medium line, worn from level 16, sat at 31. "Lord" is the mesh the artist named. Nothing
tied it to a troop level, so the plate chest was gutted to fit the recruit and the level-41 rider
fought in it.

**The ladder** (`rebalance_armor.MESH_TIER_LADDER`, the maintainer's table). The tier is read
from the id token (`mesh_tier_of`: `_light_`, `_med_`/`_medium_`, `_heavy_`, `_elite_`, `_lord_`;
`_civ`/`civilian` is off the ladder, an id with no token is not judged). Rows are inclusive upper
bounds; TAOM levels run 6, 11, 16 ... so a level between rows takes the row above it.

| Troop level | Allowed mesh tiers |
|---|---|
| <= 6 | light |
| <= 16 | light, medium |
| <= 21 | medium |
| <= 26 | heavy |
| <= 31 | heavy, elite |
| <= 36 | elite |
| >= 41 | elite, lord |

Lord kit is for level 41+ troops or lords; hero kit never anchors and is out of scope. The
ladder-exempt and bare-chested-by-design troops are skipped, as are civilian sets. Only
`troops/troops_*.xml` is judged (villagers and notables are not on the ladder).

**Two directions, one gate.** Over-dressed (the tier above the highest allowed) is the harmful
one: the low wearer anchors the mesh, so every higher troop in it loses armour. Under-dressed
(below the lowest allowed) is cosmetic; the troop-level gates already price it. The validator's
`ARMOUR_MESH_TIER_LADDER` warning reports both with the direction in the message; the pure function
is `rebalance_armor.mesh_ladder_violations`, shared with the fixer, and it needs no install because
the tier is in the id. Baseline on 2026-09-16 before the fix: 251 over-dressed (troop, item) pairs
over 147 troops in 9 cultures, 1,283 under-dressed over 344 troops.

**The fixer**, `tools/fix_armour_mesh_ladder.py` (dry-run default, `--apply`), swaps each
over-dressed pair to the SAME line (the id up to its tier token, in the same slot file, so a helmet
is never offered as a chest) at the substitute tier, the same old item becoming the same new item
in every set of the troop so its sets stay interchangeable. Three rules decide the target, in
order, and each exists because the naive choice recreated the bug:

1. **Tier**: `substitute_mesh_tiers(level)`, the allowed tiers not above the troop's STAT band
   (`level_to_band`) highest first, then the allowed tiers above it lowest first. A level-11 troop
   may wear medium, but its band is light: put in `_med_a` it anchors that variant to the light
   band, one notch down from the original bug. So light first; medium stays on the list because a
   line with no light variant (the Mordor orc infantry chest) still has a ladder-legal swap, and a
   medium mesh dragged to the light band beats a heavy one left there.
2. **Variant**: among a tier's variants, the one whose current anchor band (`line_anchors`, the
   lowest in-scope wearer) is nearest the troop's band wins; at equal distance, below beats above.
   Two `_med_` chests can be two bands apart, because each is priced by its own lowest wearer: the
   first apply put a level-21 guardsman on `anf_inf_helmet_med_a` (a level-11 wearer, 21) when
   `med_b` sat beside it at 33, and nine upgrade edges regressed. Troops are placed lowest level
   first and every pick moves the anchor it lands on, so two troops converging on an unworn
   variant in one run see each other (the deep review found the level-16 grunt and the level-21
   orcs stacked on `sk_md_orc_inf_chest_med_d`; one run now spreads them over `med_b`/`med_c`).
   A pick that lands off the troop's band because the line has nothing at it is marked on its
   row (`anchor L16, a band below`), 85 of 186 on 2026-09-16, most of them level-21 troops whose
   only ladder-legal mesh is medium while their stat band is heavy.
3. **Suffix**: same variant letter, else the nearest one of the same shape (`_cape_a` before `_a`).
   A digit may follow the tier token (`rivendell_torso_lord3_silver`, `thenn_armor_med1`), so the
   split reads every id `mesh_tier_of` tiers; a token followed by a letter (`_lordly`) is not a tier.

A line with nothing at any allowed tier is REPORTED and left alone (hand decision). Under-dressed is
reported, never written (`--report-under` lists the rows). After `--apply` the rosters are re-read
and the run exits 1 if an over-dressed pair with a substitute remains. Then re-derive and restat,
because the anchors moved:

```
python tools/fix_armour_mesh_ladder.py            # read the table
python tools/fix_armour_mesh_ladder.py --apply
python tools/derive_armor_tiers.py
python tools/rebalance_armor.py --dry-run --all --tier-source roster-first --keep-weights --keep-material-type
python tools/rebalance_armor.py --apply   --all --tier-source roster-first --keep-weights --keep-material-type --backup-tag <tag>
python tools/rebalance_armor.py --apply   ... --armory-path "E:\repos\lotraom-assets\v1.5\LOTRLOME_Armory\ModuleData\LOTRLOME_items"
```

**What the 2026-09-16 pass did.** 186 over-dressed pairs swapped over 118 troops in 11 troop
files (249 equipment lines), the Uruk warrior's nine capeless sets given the `pauldron_medium_b` it
already wore in the tenth (its demoted heavy chest had been masking the gap as an upgrade
regression), 60 Armory items restatted on the live install and the `lotraom-assets` v1.5 mirror
(weights and materials untouched; `.bak-meshladder-609` beside each Armory file): the six Gundabad
lord chests 20 to 49, Iron Hills heavy chests 28 to 59, the Noldor gold heavy torso 44 to 68, the
Mordor orc heavy chests 15 to 32, five Gondor heavy helmets 33 to 43, and so on. One item went
down, `sk_md_orc_inf_chest_med_d` 24 to 15: the line has no light variant, the level-11 goblin
snaga and hunter had to land on a medium mesh, and the picker gave them the unworn one, so nobody
else pays. `CROSS_CULTURE_ARMOUR_INVERSION` stayed at the same 7 cells. Four
`UPGRADE_ARMOUR_REGRESSION` warnings appeared, all pre-existing under-dress that surfaced when the
parent's freed mesh climbed to its real band (the level-36 goblin veterans in `_heavy_` helmets
under a parent whose `arc_helmet_heavy_a` rose 29 to 34; the Imladris and Lindon guardsmen in
untokened tier-3 kit under a swordguard whose `torso_heavy_tier1` rose 44 to 68); they belong to
the under-dressed pass, not to `fix_upgrade_armour_regressions.py`, whose cascade would re-dress
the capstone's lord torsos and push the beastmaster into medium mesh.

**65 hand decisions** (over-dressed, nothing in the line at an allowed tier). Dunland's whole tree
sits one notch high because the artist named that kit a tier up (`heavy` is its mid mesh); Gondor's
level-6 peasants have no light helmet in the Anorien infantry line; the Rhun and Iron Hills lines
have no medium greaves or chest. The choices are: author the missing mesh tier, accept the
kit and add the troop to `_ARMOUR_LADDER_EXEMPT` with a reason, or move the troop to another line.

| Culture | Troop | L | Slot | Item | Mesh | Allowed |
|---|---|---|---|---|---|---|
| rhun_new | `black_sun_scout` | L21 | Leg | `sk_rh_drag_grvs_heavy_a` | heavy | medium |
| rhun_new | `darkhun_horseman` | L21 | Leg | `sk_dg_khml_grvs_scale_heavy_a` | heavy | medium |
| rhun_new | `darkhun_infantry` | L21 | Leg | `sk_dg_khml_grvs_hplate_heavy_a` | heavy | medium |
| dunland | `dunland_bear_chosen` | L21 | Body | `dunland_wulf_scalemail_heavy_e` | heavy | medium |
| dunland | `dunland_boar_spearman` | L21 | Body | `dunland_wulf_scalemail_heavy_c` | heavy | medium |
| dunland | `dunland_dragon_crossbowman` | L21 | Body | `dunland_caerdh_chainmail_heavy_f` | heavy | medium |
| dunland | `dunland_dragon_crossbowman` | L21 | Cape | `dunland_caerdh_pauldron_heavy_cape_a` | heavy | medium |
| dunland | `dunland_dragon_crossbowman` | L21 | Head | `dunland_caerdh_helmet_heavy_d` | heavy | medium |
| dunland | `dunland_falcon_archer` | L21 | Head | `dunland_caerdh_helmet_heavy_e` | heavy | medium |
| dunland | `dunland_lizard_horseman` | L21 | Body | `dunland_caerdh_chainmail_heavy_b` | heavy | medium |
| dunland | `dunland_lizard_horseman` | L21 | Cape | `dunland_caerdh_pauldron_heavy_cape_a` | heavy | medium |
| dunland | `dunland_lizard_horseman` | L21 | Head | `dunland_caerdh_helmet_elite_b` | elite | medium |
| dunland | `dunland_ox_pikeman` | L21 | Body | `dunland_wulf_scalemail_heavy_b` | heavy | medium |
| dunland | `dunland_raiders_boss` | L21 | Body | `dunland_wulf_scalemail_heavy_f` | heavy | medium |
| dunland | `dunland_raven_archer` | L21 | Body | `dunland_caerdh_chainmail_heavy_a` | heavy | medium |
| dunland | `dunland_raven_archer` | L21 | Head | `dunland_caerdh_helmet_heavy_f` | heavy | medium |
| dunland | `dunland_raven_warrior` | L16 | Head | `dunland_caerdh_helmet_heavy_e` | heavy | light/medium |
| dunland | `dunland_stag_lancer` | L21 | Body | `dunland_caerdh_chainmail_heavy_a` | heavy | medium |
| dunland | `dunland_stag_lancer` | L21 | Cape | `dunland_caerdh_pauldron_heavy_cape_b` | heavy | medium |
| dunland | `dunland_stag_lancer` | L21 | Head | `dunland_caerdh_helmet_elite_a` | elite | medium |
| dunland | `dunland_wolf_raider` | L21 | Body | `dunland_wulf_scalemail_heavy_f` | heavy | medium |
| rhun_new | `far_rhun_cavalry` | L21 | Leg | `sk_rh_loke_grvs_heavy_a` | heavy | medium |
| rhun_new | `far_rhun_horse_master` | L21 | Leg | `sk_rh_loke_grvs_heavy_a` | heavy | medium |
| rhun_new | `far_rhun_infantry` | L21 | Leg | `sk_rh_loke_grvs_heavy_a` | heavy | medium |
| gondor | `gondor_anf_cavalry` | L21 | Head | `sk_gd_anf_cav_helmet_heavy_a` | heavy | medium |
| gondor | `gondor_anf_cavalry` | L21 | Head | `sk_gd_anf_cav_helmet_heavy_b` | heavy | medium |
| gondor | `gondor_anf_infantry` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | heavy |
| gondor | `gondor_anf_levy` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_ano_mt_cavalry` | L21 | Head | `sk_gd_ano_cav_helmet_heavy_a` | heavy | medium |
| gondor | `gondor_ano_peasant` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_bel_infantry` | L21 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | medium |
| gondor | `gondor_bel_recruit` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_bel_recruit_merc` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_bel_vet_infantry` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_b` | elite | heavy |
| gondor | `gondor_lam_hill_warden` | L31 | Head | `sk_gd_lam_nob_helmet_lord_a` | lord | heavy/elite |
| gondor | `gondor_lam_hill_warden` | L31 | Head | `sk_gd_lam_nob_helmet_lord_b` | lord | heavy/elite |
| gondor | `gondor_lam_hill_warden` | L31 | Head | `sk_gd_lam_nob_helmet_lord_c` | lord | heavy/elite |
| gondor | `gondor_lam_hill_warden` | L31 | Head | `sk_gd_lam_nob_helmet_lord_d` | lord | heavy/elite |
| gondor | `gondor_lam_vet_swordman` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | heavy |
| gondor | `gondor_leb_infantry` | L21 | Head | `sk_gd_ano_cav_helmet_heavy_a` | heavy | medium |
| gondor | `gondor_loss_axe_thrower` | L21 | Cape | `sk_gd_los_pauld_inf_heavy_a` | heavy | medium |
| gondor | `gondor_loss_lumberman` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_loss_lumberman_merc` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_loss_noble` | L16 | Cape | `sk_gd_los_pauld_nob_heavy_a` | heavy | light/medium |
| gondor | `gondor_loss_noble_captain` | L36 | Body | `sk_gd_los_nob_chest_lord_a` | lord | elite |
| gondor | `gondor_loss_noble_veteran` | L21 | Cape | `sk_gd_los_pauld_nob_heavy_a` | heavy | medium |
| gondor | `gondor_loss_vet_axebearer` | L21 | Cape | `sk_gd_los_pauld_inf_heavy_a` | heavy | medium |
| gondor | `gondor_osg_archer` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | heavy |
| gondor | `gondor_osg_infantry` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | heavy |
| gondor | `gondor_pg_archer` | L21 | Head | `sk_gd_pin_arc_helmet_heavy_a` | heavy | medium |
| gondor | `gondor_pg_cavalry` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | heavy |
| gondor | `gondor_pg_spearman` | L21 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | medium |
| gondor | `gondor_pg_spearman` | L21 | Head | `sk_gd_pin_spear_helmet_heavy_a` | heavy | medium |
| gondor | `gondor_pg_vet_spearman` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_b` | elite | heavy |
| gondor | `gondor_pg_volunteer` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_ring_peasant` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_ser_noble` | L16 | Head | `sk_gd_sere_helmet_heavy_a` | heavy | light/medium |
| gondor | `gondor_ser_veteran` | L21 | Head | `sk_gd_sere_helmet_heavy_a` | heavy | medium |
| erebor | `ironpass_arbalest` | L21 | Body | `sk_dwarf_iron_chest_heavy_a` | heavy | medium |
| erebor | `ironpass_arbalest` | L21 | Body | `sk_dwarf_iron_chest_heavy_b` | heavy | medium |
| erebor | `ironpass_arbalest` | L21 | Body | `sk_dwarf_iron_chest_heavy_c` | heavy | medium |
| erebor | `ironpass_infantry` | L21 | Body | `sk_dwarf_iron_chest_heavy_a` | heavy | medium |
| erebor | `ironpass_infantry` | L21 | Body | `sk_dwarf_iron_chest_heavy_b` | heavy | medium |
| erebor | `ironpass_infantry` | L21 | Body | `sk_dwarf_iron_chest_heavy_c` | heavy | medium |
| erebor | `ironpass_ram_rider` | L21 | Body | `sk_dwarf_iron_chest_heavy_c` | heavy | medium |

### 2026-09-25: Gondor after KEYforce's Lamedon drop, and the noble-line rule

KEYforce's drop (lotraom-assets `429746b2`) re-kitted Gondor into new Lamedon and Ringlo Vale
armour. His spec (`tools/gondor_armors_and_troops.md` in the mirror) splits each Gondor region into
a regular line and one or more "Noble" lines, and dresses the nobles in their own `_nob_` kit from
the first tier. Mike's rule (2026-09-25): **nobles wear better armour than regular troops of their
level.** It is implemented in the tools, not as exemptions:

- **`taom_schema.Validator._NOBLE_LINE_TROOPS`** names the 92 noble troops at engine tier 2 to 7
  (level 11 to 36) of the spec's sixteen noble lines. Tier 8+ nobles already sit on the elite and
  lord rows.
- **The mesh gate and fixer judge a noble one tier up:** `rebalance_armor.allowed_mesh_tiers(level,
  noble=True)` adds the tier above the level's ceiling, so the level-11 Ringlo militia may wear the
  heavy Anorien helmet KEYforce gave it.
- **A noble anchors an item a stat band up, never below the item's own tier:**
  `rebalance_armor.noble_anchor_level(level, item_id)`, read by `derive_armor_tiers.py` and
  `fix_armour_mesh_ladder.line_anchors`. The floor matters: without it the level-11 militia
  anchored the regular Anorien heavy helmet at the medium band and dragged it from 43 to 33 for
  every regular troop wearing it (the lowest at level 26). An elite-band noble (level 31 and 36)
  anchors at its own level: elite is the top band.
- Nobles stay judged by every gate and stay in the cross-kingdom cells. The first attempt put them
  in `_ARMOUR_LADDER_EXEMPT` instead; that stopped them anchoring at all, so their `_med_` pieces
  priced by the id keyword and most landed below a regular of the same level (Ithil Guard armour 57
  to 36 on a level-31 noble), while the gates stopped seeing Gondor's tier 3 to 7. Deep review
  wave 2 caught it the same day.

**Hand decisions are (troop, item) pairs:** `_ARMOUR_LADDER_EXEMPT_ITEMS` holds the 18 regular
troops' over-dressed pieces whose line ships no lower-tier variant (Anorien infantry helmets at
level 6, Lamedon and Lossarnach heavy pauldron-capes, Anfalas, Anorien cavalry and Pinnath Gelin
heavy helmets, Anorien and Osgiliath elite pauldron-capes). Only that item is excused on that troop:
it is not judged there and does not anchor its price, and the troop's other slots stay on the
ladder. Whole-troop exemption had repriced slots nobody decided on (the Osgiliath chest 63 to 47).

The roster-first restat ran for Gondor (`--keep-weights --keep-material-type`) three times while
this was built. Against KEYforce's typed stats the net is 91 items up, 67 down and 216 unchanged.
Noble medians now sit above regulars at every tier (level 16: 185 against 124; 21: 204 against 144;
26: 241 against 207; 31: 246 against 217; 36: 250 against 218), and the Gondor lord template reads
215 (202 before). Seven noble troops stay under the regular median by their kit, not their pricing: the Cair
Andros and Osgiliath first tiers, the Tolfalas arbalest and crossbowman, the Methir noble, the
Pelargir skirmisher, and the Blackroot archer by one point. Backups in the
live Armory: `LOTRLOME_items/gondor/*.bak-noble-restat-2026-09-25` (KEYforce's stats),
`*.bak-noble-band-2026-09-25`, `*.bak-noble-floor-2026-09-25`. Validator after:
`ARMOUR_MESH_TIER_LADDER` 1,330 against 1,348 at `HEAD`, `CROSS_CULTURE_ARMOUR_INVERSION` 7
(unchanged), `UPGRADE_ARMOUR_REGRESSION` 4 against 6, none of them Gondor.

The restat lives only in the unversioned Armory, and the mirror still holds KEYforce's stats by
Mike's rule, so a re-sync reverts it silently. Re-run the restat after any Gondor sync:
`python tools/derive_armor_tiers.py`, then
`python tools/rebalance_armor.py --dry-run --tier-source roster-first --keep-weights
--keep-material-type --cultures gondor`; a clean tree reports `Changed: 0`.

**Not fixed here, known:** the inherited-ratio secondaries (#583, Codex review 104): the medium
chest's arm armour (41) still reads above the lord chest's (25) in the tooltip, because secondaries
scale with the primary at each item's legacy proportion; a secondary floor per band is a curve
decision. The under-dressed direction (1,283 pairs) is the next roster pass.

## Dependencies

- `rebalance_armor.py` (curve) — the analyzer imports it.
- The live `LOTRLOME_Armory` module install (read at runtime).
- Python stdlib only (`xml.etree`, `statistics`, `json`).

## Tests / verification

The analyzer is read-only and self-verifying: running it against the live tree must reproduce the known defects (iron_hills arm monolithic, harad body all 9.5, dale clean). No unit-test harness yet; the regression check is "re-run and confirm the executive summary matches this doc's baseline."

The kingdom overview has one: `tools/tests/test_analyze_kingdom_armour.py` (22 cases on a synthetic two-culture tree: region sums, civilian exclusion, classification, ladder-exempt handling, curve aliases, matrices, pairwise and bare-chested inversions, ceilings judged on the item's own folder, gate preview, a `main` run that hashes every fixture XML before and after, a sub-line item in a shared folder kept in the reserve, the two observation tables with a sub-line troop held to its own cap, and one over the shipped troops that fails if a troop the analyzer excludes by name is missing from `_ARMOUR_LADDER_EXEMPT`). The gate's own tests are `CrossCultureArmourInversionTests` in `tools/tests/test_validate_moduledata.py`, including one that fails if an `_ARMOUR_LADDER_EXEMPT` id no longer exists in the shipped troops.

The kingdom-cap curve (#583) has `tools/tests/test_kingdom_caps.py` (23 cases): the cap table and ratios as the maintainer stated them, every cap key a folder or a routed line, the routing of each sub-line (`tier_from_value` included), the cap-model values per band and slot (rounding half up, lord = elite, the four Mordor-folder lines by id), secondaries by legacy proportion, weight on the legacy ladder, `level_to_band` at each band edge and shared with the derivation, the Black Numenoreans back on the curve with hero kit and the Khamul line told apart, the two-tier invariant clean on every cap and reported on a compressed one, anchors (battle sets only, civilian and ladder-exempt troops never anchor, the exempt set is the validator's, the map anchor first like the writer), and the writer (roster-first banding, secondaries by ratio with zero staying zero, weights and material kept while the extremity loot tables move, a commented copy skipped, BOM and CRLF preserved, one `.bak-<tag>` per file that a second apply leaves alone).

## How-To

**Fix a culture's monolithic weight slot:** use `rebalance_armor.py --apply --weights-only --cultures <c>`. It ladders ONLY the weight to each item's armor-derived tier (`tier_from_value`) and leaves armor + material untouched, so it can't mangle hand-tuned armor through the keyword detector. It is guarded — it only touches a slot that is **currently monolithic-weight**, so it can never collapse an already-varied slot (the harad-shoulder regression, fixed 2026-06-30). Back up the culture folder first; re-run the analyzer to confirm the error cleared.

**`--weights-only` applicability (important):** it is correct ONLY when the slot's armor is varied AND correctly tiered, because weight follows armor. It is the right tool where the armor is already right and only the weight is frozen (harad body/head/leg). It is the WRONG tool when: (a) the armor is itself uniform — it can't ladder (no-op, e.g. rohan's combat boots, iron_hills arm), those need an armor mid-tier first; or (b) the armor is over-tiered for the culture's intended weight class — it would propagate that into heavier weights (e.g. dunland body 30-45 → 12-21kg, the opposite of the light-raider intent). Cultures in (b) need the identity decision (lower armor + material) before any weight pass.

**Re-tier a whole culture from its rosters:** `python tools/derive_armor_tiers.py` (refresh the map), then `rebalance_armor.py --dry-run --tier-source roster-first --cultures <c> --keep-weights --keep-material-type` and read the proposed changes. Each worn item is re-stated to its lowest wearer's band on the kingdom-cap curve (hero and civilian kit skipped, unworn kit by its id keyword, keyword-less unworn kit left alone); the older `--tier-source roster` reads the map's `tier` column, which is now anchor first too, so the two differ only in that `roster` skips a keyword-less unworn item (an unworn item with a tier word in its id has a tier in the map and is re-stated by both) and folds `lord` to `elite`. Add `--no-lower-armor` for a "do not nerf" culture (raises under-tiered items, never reduces; on its own it no longer holds material or weight, so pair it with `--keep-materials` (material and loot table frozen) or `--keep-material-type` (loot table still follows the band) and `--keep-weights`). Add `--weights-only` to set weight by roster tier without touching armor (e.g. a mobility trim that spares elite plate). Always dry-run first: the dunland dry-run caught a wrong-direction change before applying.

**Set a kingdom's armour power:** edit `KINGDOM_CAPS` (one chest value; a sub-line that shares a folder gets a `LINE_PREFIXES` row); dry-run with `--tier-source roster-first --keep-weights --keep-material-type`. `CULTURAL_MODS[culture]` no longer moves a capped kingdom's protection (Gondor at protection 99 still writes a 57 chest); it carries `weight_mult` and the protection of the legacy path (the civilian tier and the uncapped folders such as `troll`). The analyzer's "In CULTURAL_MODS" column still flags a culture on the neutral default, which matters for weight.

**Exclude a new hero/boss item from the curve:** add its id substring to `EXCLUDE_ID_SUBSTRINGS` or its name to `HERO_NAMES`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/black-numenorean.md](./black-numenorean.md)
- [docs/features/gondor-armor-revamp.md](./gondor-armor-revamp.md)
- [docs/features/starting-equipment-tuning.md](./starting-equipment-tuning.md)
- [docs/modding/balance-levers.md](../modding/balance-levers.md)
- [docs/modding/items-armor.md](../modding/items-armor.md)
- [docs/modding/module-armory.md](../modding/module-armory.md)

<!-- backlinks-end -->
