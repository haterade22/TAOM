# Ranged Range Ladders

> Reach is the launcher's `missile_speed`. Tier climbs it inside a line, kingdom rank orders it
> inside a band, one generated item per cell, one validator gate. Issue #582, 2026-09-12.

## Overview

Every bow and crossbow a troop carries is a generated `ladder_<line>_<bow|xbow>_<band>` item whose
`missile_speed` comes from a grid: `speed = band_base[band] + rank_step * (n_lines - rank)`. The
band is the troop's engine tier range, the line is its kingdom (or an elite sub-line inside one)
in the order the user ranked the kingdoms' archers. The spec is `tools/ranged_ladders.json`; two
tools apply it and the validator's `RANGED_LADDER_INVERSION` keeps it true.

## Why This Exists

An archer's reach is the launcher's `missile_speed` and nothing else. Verified in the 1.4.8
decompile:

- `Mission.cs:4943` launches the missile at the wielded bow's `GetModifiedMissileSpeedForCurrentUsage()`;
  the arrow's own `missile_speed="10"` is a dead field (every vanilla arrow carries it).
- `SandboxAgentStatCalculateModel.cs:978` sets `MissileSpeedMultiplier = 1f`; the only things that
  change it afterwards (lines 1445 to 1467) are two Throwing perks and wet campaign weather (-20%).
  No Bow skill term, no Bow perk. `DefaultSkillEffects` has no bow-speed effect (`ThrowingSpeed`
  feeds ready speed, not missile speed).
- `AgentStatCalculateModel.cs:159-223`: Bow skill feeds `WeaponInaccuracy` (via `BowAccuracy`,
  -0.09% per level), `AiShootFreq`, `AiWaitBeforeShootFactor`, the `AiRangerLeadError*` and
  `AiRanger*ErrorMultiplier` terms and, mounted only, `AiRangedHorsebackMissileRange =
  0.3 + 0.4 * aiLevel` (the fraction of max range a horse archer opens fire at). A foot archer
  with Bow 60 and one with Bow 300 draw the same arc from the same bow.
- Range itself is native ballistics of that speed (`Agent.GetMissileRange` -> `IMBAgent`), gravity
  9.806 and `AirFrictionArrow = 0.003` for bows and crossbows alike
  (`ItemObject.GetAirFrictionConstant`). Mission-level modifiers: rain or snow x0.9 on bow and
  crossbow speed, fog x0.8 on range (`SandboxApplyWeatherEffectsModel`).

On 2026-09-12 the 227 troops carrying a bow or crossbow across the 16 troop files had no order:
six trees handed a higher tier a slower launcher (Gondor T6 `crossbow_e`, Dale T5 `crossbow_e`,
Dunland T4 `crossbow_e`, Mordor T5 `mountain_hunting_bow`, Isengard uruk crossbows flat at 60, Rhun
T5 `crossbow_a`); seven kingdoms gave T2 militia vanilla `hunting_bow` (64) and T3 veteran militia
vanilla `noble_long_bow` (85) over their own T4 to T6 regulars; Gondor's Ithil Guard T7 to T9 sat on
the 85-speed steel bow under Blackroot Vale's 92 to 94; and Dol Guldur (90) outranged every Gondor
regular. The gate function counted 1,741 inverted pairs.

## Architecture

### The two rules

Per launcher class (Bow and Crossbow are never compared with each other):

1. Inside a **line**, a lower tier is never faster than a higher tier.
2. Inside a **band**, a better-ranked line is never slower than a worse-ranked one.

Rank does not reach across bands: a Dunland T9 may outrange a Mirkwood T2. Making rank dominate
every tier would need about 85 distinct speeds up to 140 and was rejected.

### Bands, lines, grid

| Term | Definition |
|---|---|
| Engine tier | `clamp(ceil((level - 5) / 5), 0, 10)`, `ranged_ladder.engine_tier`, pinned to `taom_schema.Validator._troop_tier` by a test. The formula is vanilla `DefaultCharacterStatsModel.GetTier`; the cap of 10 is TAOM's `TaomCharacterStatsModel.MaxCharacterTier` override (vanilla caps at 6), so a reader checking the dump against `MAX_TIER = 10` is looking at the wrong model |
| Band | E = T0-2, R = T3-4, V = T5-6, X = T7-8, C = T9-10 (`bands` in the spec) |
| Line | A troop file (`files`, the `troops_<culture>.xml` token) or an id prefix inside one (`prefixes`, matched first). Militia, bosses and horse archers ride their file's line. A prefix line may sit anywhere in the order relative to the whole-file line it carves from; only two WHOLE claims on one file are refused |
| Rank | The line's position in `lines`, 1 = the best archers |
| Speed | `band_base[band] + rank_step * (n_lines - rank)`; `band_base = {E:58, R:62, V:66, X:70, C:74}`, `rank_step = 2` |

The 18 lines, best first: mirkwood; rivendell (`troops_lindon.xml` shares it); gondor_special
(`gondor_ith_*`, `gondor_ithilien_*`, `gondor_brv_*`); mordor_num (`mordor_num_*`); dale; isengard;
harad; mordor_uruk (`mordor_uruk_*`); rhun_new; umbar; gondor (the rest of the file); goblin;
erebor (with Iron Hills and Ironpass); mordor (orcs, Morannon, militia); gundabad; dolguldur; rohan;
dunland. Grid corners: Dunland E 58 (about 205 m under the drag model) to Mirkwood C 108 (about
385 m); the previous span was 210 to 361 m.

### Items

Every band of every (line, class) the spec declares a donor for is a generated item, 130 in all
(18 bow lines and 8 crossbow lines, 5 bands each), a pure function of the spec so the item set
never depends on which troops exist today. Each is a verbatim clone of the line's donor (`donor`
per class, `donor_by_band` where the line has a visual progression: elf Longbow I to IV, Ithilien I
to III, Numenorean I/II, Erebor I/II, Dale recurve to longbow, Rhun steppe to Dragon longbow) with
only `id`, `name` and `missile_speed` replaced (and `item_usage` where the line overrides it,
below) and `is_merchandise="false"`. That flag is `ItemObject.NotMerchandise`, and it does
more than keep 130 near-duplicate bows out of the shops: vanilla
`DefaultBattleRewardModel.GetRandomItem` (v1.4.8 lines 125 and 153) skips every
`NotMerchandise` slot when it rolls casualty loot, so a ladder bow never drops from a fallen
archer either. The ladder items are troop-only by construction; the player's own bows are the
donors, which stay in the shops and the loot pool untouched. (The first draft of this page
said loot still dropped them; the Codex review of 2026-09-13 read the model and it does not.)
Damage, accuracy, difficulty, mesh and flags are the donor's, so a vanilla donor keeps its vanilla
`culture=` and an Ithilien donor its `difficulty="100"`; neither matters to an AI troop
(`CharacterObject.IsRanged` and `GetFormationClass` read the item TYPE; `difficulty` is read only
by the player's inventory screen, `CharacterHelper.CanUseItem` for the equip gate and the tooltip,
never by spawn or loot code). One coupling to know before ever flipping `is_merchandise` back on:
vanilla `DefaultTournamentModel.GetRegularRewardItems` sorts candidate prizes by
`item.Culture == town.Culture`, and about half the 130 clones carry a vanilla culture
(`Culture.vlandia` on every `crossbow_c` clone, for instance). Today `is_merchandise="false"`
excludes them from that pool before the culture is ever compared.

**A rider cannot draw a `long_bow`.** Native's `item_usage_sets.xml` gives the `long_bow` usage
set `base_set="bow"` plus two flags, `requires_no_mount` and `requires_no_shield`, and nothing
else; the engine's inventory tooltip says "Can't use on horseback" from the first flag
(`CampaignUIHelper`, `ItemUsageSetFlags.RequiresNoMount`) and a mounted AI archer holding one
spawns with it on its back and never fires. Seven horse archers (Harad, Dunland, Rohan E) shipped
that way on 2026-09-12 because their line's donor was a `long_bow` bow. The Armory's own answer
is `wm_mirkwood_bow_a02` "LongBow II - Horse": the a01 mesh with `item_usage="bow"`. The spec
does the same per line with `"usage": {"Bow": "bow"}` (Harad and Dunland today), which the
generator writes onto the clone's `<Weapon>` and `--verify` checks, so the kingdom keeps its own
bow mesh; Rohan instead dropped its E-band `glen_ranger_bow` override for the line's
`composite_steppe_bow`. `planned_edits(barred=)` refuses to roster a mounted troop into a cell
whose effective usage (override, else donor) is in the install's `requires_no_mount` set, and the
validator's `RANGED_MOUNT_USAGE` warning names any such troop that exists; both read the set from
`Modules/*/ModuleData/item_usage_sets.xml` (`ranged_ladder.mount_barred_usages`) and skip, saying
so, when no such file can be read. The second flag is not gated: Native itself ships twelve
Wolfskins sets (`default_group="Ranged"`) with a `long_bow` bow beside a shield.

Names are `{=<id>}<donor name, its own numeral and "- Starting" / "- Horse" suffix stripped>
<band numeral I..V>`, and the generator registers each English row in the Armory's
`Languages/loc_<folder>.xml` between `<!-- TAOM-RANGED-LADDER:START/END -->` markers so
`translate_with_claude.py --module Armory --sync-ids` can seed the other eleven languages.

### Rosters

`rebalance_ranged_ladders.py --apply` moves every `Item0..Item3` slot holding a bow or crossbow,
in every battle set of every troop, onto `ladder_<line>_<class>_<band(tier)>`. Ammo slots are never
touched and a class never changes, so arrows and bolts stay valid; multi-set troops stop mixing
three bows. Civilian sets are skipped. First run: 258 slot edits over 227 troops, 559 id lines in
16 files, 1,741 inverted pairs to 0.

### Component Diagram

```
tools/ranged_ladders.json  (bands, band_base, rank_step, lines in rank order, donors)
        |
        v
tools/ranged_ladder.py     engine_tier, band_of, rank_of, grid_speed, ladder_id, line_of,
        |                  index_launchers, load_ranged_troops, troop_speed, inversions,
        |                  summarize, unassigned, planned_items, planned_edits, flight_range
        |
        +--> tools/generate_ranged_ladder_items.py   LOTRLOME_items/<folder>/ranged_ladder.xml
        |                                             + Languages/loc_<folder>.xml marker block
        |                                             (live Armory + lotraom-assets mirror)
        +--> tools/rebalance_ranged_ladders.py       tools/reports/ranged-ladders/{REPORT.md,json}
        |                                             --apply: troops/troops_*.xml launcher slots
        +--> tools/taom_schema.py                    RANGED_LADDER_INVERSION, RANGED_MOUNT_USAGE (warnings)
```

## Configuration

### Config File: `tools/ranged_ladders.json`

```json
{"bands": {"E": [0, 2], "R": [3, 4], "V": [5, 6], "X": [7, 8], "C": [9, 10]},
 "band_base": {"E": 58, "R": 62, "V": 66, "X": 70, "C": 74}, "rank_step": 2,
 "lines": [
   {"id": "gondor_special", "folder": "gondor", "files": ["gondor"],
    "prefixes": ["gondor_ith_", "gondor_ithilien_", "gondor_brv_"],
    "donor": {"Bow": "wm_ithilien_bow"},
    "donor_by_band": {"Bow": {"X": "wm_ithilien_bow_b", "C": "wm_ithilien_bow_c"}}},
   {"id": "harad", "folder": "harad", "files": ["harad"],
    "donor": {"Bow": "wm_harad_bow_a01"}, "usage": {"Bow": "bow"},
    "donor_by_band": {"Bow": {"X": "wm_harad_bow_a02", "C": "wm_harad_bow_a02"}}},
   ...]}
```

Change a knob, re-run both tools with `--apply`, restart the game. Moving a line up or down the
list moves every one of its items by `rank_step` per place. `validate_spec` refuses a spec whose
bands leave a tier uncovered, whose bases do not increase, whose `rank_step` is not positive,
whose `folder` the Armory's `SubModule.xml` does not register, whose donor is missing or of
the wrong class, whose `files` token names no `troops_<token>.xml` on disk (a typo would
otherwise be a line with no troops, which reads as clean), whose prefixes overlap across two
lines (an id matching both would be decided by list order alone), or whose `usage` names a
class that is not a ladder or an empty usage id.

### Current Values

| # | Line | Bow E R V X C | Crossbow E R V X C |
|---|---|---|---|
| 1 | mirkwood | 92 96 100 104 108 | |
| 2 | rivendell | 90 94 98 102 106 | |
| 3 | gondor_special | 88 92 96 100 104 | |
| 4 | mordor_num | 86 90 94 98 102 | |
| 5 | dale | 84 88 92 96 100 | 84 88 92 96 100 |
| 6 | isengard | 82 86 90 94 98 | 82 86 90 94 98 |
| 7 | harad | 80 84 88 92 96 | |
| 8 | mordor_uruk | 78 82 86 90 94 | 78 82 86 90 94 |
| 9 | rhun_new | 76 80 84 88 92 | 76 80 84 88 92 |
| 10 | umbar | 74 78 82 86 90 | 74 78 82 86 90 |
| 11 | gondor | 72 76 80 84 88 | 72 76 80 84 88 |
| 12 | goblin | 70 74 78 82 86 | |
| 13 | erebor | 68 72 76 80 84 | 68 72 76 80 84 |
| 14 | mordor | 66 70 74 78 82 | |
| 15 | gundabad | 64 68 72 76 80 | |
| 16 | dolguldur | 62 66 70 74 78 | |
| 17 | rohan | 60 64 68 72 76 | |
| 18 | dunland | 58 62 66 70 74 | 58 62 66 70 74 |

Consequences of the ranking, recorded so nobody reads them as bugs: Dol Guldur fell from 85-92 to
62-78, Rohan from 70-85 to 60-72, Dunland's Dragon crossbows from 97 to 66; Isengard's uruk
crossbows rose from 60 to 82-90 (their 97 damage unchanged), Dale's royal crossbowman from 65 to
92, the Moon Guard from 85 to 104. A faster arrow also flies flatter, so hits at range get slightly
easier for the same skill.

## Key Files

| File | Role |
|---|---|
| `tools/ranged_ladders.json` | The spec |
| `tools/ranged_ladder.py` | Library shared by the tools and the validator (no CLI) |
| `tools/generate_ranged_ladder_items.py` | Items and English loc rows into the live Armory and the mirror (`--apply`, `--verify`, `--revert`) |
| `tools/rebalance_ranged_ladders.py` | Report to `tools/reports/ranged-ladders/` (md, html, json); `--apply` rewrites the rosters |
| `docs/reference/ranged-troops.html` | The tracked HTML: every ranged troop per kingdom, skills beside weapon speed, reach, accuracy, spread (bow factor 0.0009 per skill level, crossbow 0.0005), cadence, damage (launcher plus ammo, `Mission.OnAgentShootMissile`); a full document with its own charset, no clock in it so a regenerate is byte-identical (`.gitattributes` pins it LF); never hand-edited |
| `tools/taom_schema.py` | `Registries.launchers` and `.mount_barred_usages`, `build_launchers`, `build_mount_barred_usages`, `Validator._ranged_ladder_inversions` (emits `RANGED_LADDER_INVERSION` and `RANGED_MOUNT_USAGE`), `_RANGED_LADDER_EXEMPT`; an install with no launchers is a suspect registry |
| `tools/tests/test_ranged_ladder.py` | 68 tests over all of the above, synthetic data only |
| `Main/_Module/ModuleData/troops/troops_*.xml` | The 227 rosters, edited through the tool only |
| `<game>/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/<folder>/ranged_ladder.xml` | 13 generated item files (unversioned; `--verify` is the reversion gate) |
| `<game>/Modules/LOTRLOME_Armory/ModuleData/Languages/loc_<folder>.xml` | The marker block of English names |

## Dependencies

`generate_starter_kit.py` (byte-faithful I/O, `index_items`, `registered_item_folders`,
`_serialize`), `fix_upgrade_armour_regressions.write_changes` (the roster writer),
`rebalance_troops.DEFAULT_GAME_MODULES` (install resolution through `_gamedir`). No C#.

## Tests

`python -m unittest tools.tests.test_ranged_ladder`: spec monotonicity and completeness, tier
function pinned to the validator's, prefix-before-file line precedence, launcher index (bows and
crossbows only, `*.xml` only, parse failures reported), troop loader (weapon slots, civilian sets
skipped, villagers excluded), the two rules on fixtures (tier, rank, ties, cross-class, unassigned),
grouping, planned items and edits (the usage override, the mounted refusal), drag-model
monotonicity, the higher troop judged by its slowest set; the mount-barred set read from a
fixture `item_usage_sets.xml` and None without one; the roster tool end to end on a fake
install (dry run touches nothing, apply refuses until the items exist, byte-faithful rewrite, ammo
untouched, idempotent, missing install reported); the generator end to end (both trees, loc rows
with sidecar, idempotent, `--verify` drift on a speed, a usage, an id and a missing mirror file,
`--revert` exact, unregistered folder and missing donor refused); the validator gate on real-spec
fixtures, including a troop file that does not parse (a `(file)` finding, never a clean pass) and
a mounted troop on a barred usage.

## How to change the ranking or the spread

1. Edit `tools/ranged_ladders.json` (reorder `lines`, or change `band_base` / `rank_step`).
2. `python tools/rebalance_ranged_ladders.py --stdout`: read the grid and the movers.
3. `python tools/generate_ranged_ladder_items.py --apply`, then `--verify`.
4. `python tools/rebalance_ranged_ladders.py --apply`; `python tools/validate_moduledata.py`.
5. Restart Bannerlord fully (a new item file loads only at process launch) and check a troop from
   each end of the grid in a Custom Battle.
6. `python tools/translate_with_claude.py --lang <L> --module Armory --sync-ids --apply` per
   language (needs `ANTHROPIC_API_KEY`).

## Performance

Nothing at runtime: the items are ordinary `<Item>` rows and the rosters ordinary references. The
validator pass is one pairwise sweep over about 230 ranged troops (13 ms), and the launcher
index parses only item files whose bytes mention a bow or crossbow (0.18 s over the eight module
roots; parsing every file cost 1.2 s).

## Changelog

- 2026-09-12: created (#582). First run 1,741 inverted pairs to 0; 130 items, 258 slot edits.
  Deep review: index pre-filter (validator cost 1.2 s to 0.18 s), `band_base` parse reported not
  raised, `files` tokens and prefix overlaps validated, `MaxCharacterTier` attributed to TAOM's
  override. `docs/reviews/rca-ranged-ladders-2026-09-12.md`.
- 2026-09-13: the tracked HTML report (`docs/reference/ranged-troops.html`) and the militia
  veteran step (#588). Codex review (gpt-6-astra, ultra): seven horse archers rostered into
  `long_bow` cells (`usage` override, `RANGED_MOUNT_USAGE`, planner refusal); the loot claim above
  corrected; the higher troop of a pair judged by its slowest set; an unparseable troop file is a
  finding; a valid prefix-before-file order accepted; an install with no launchers is a suspect
  registry. `docs/reviews/rca-ranged-ladders-codex-2026-09-13.md`.

## GitHub Issue

[#582](https://github.com/haterade22/TAOM/issues/582)
