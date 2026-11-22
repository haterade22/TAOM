# Ranged Ladders

> **The melee side of the same question** lives in [melee-damage-model.md](melee-damage-model.md). It reuses `engine_tier()` and the trailing-numeral regex from `tools/ranged_ladder.py`, but melee weapons are assembled from crafting pieces rather than carrying explicit stats, so their damage is simulated rather than authored.


> Every archer's reach, damage, accuracy and skill come from one ranked, per-tier ladder. Tier
> climbs every stat inside a kingdom; rank orders the kingdoms at each tier; one generated item per
> tier a kingdom fields. Issues #582 (2026-09-12, reach) and #617 (2026-09-18, the rest).

## Overview

Every bow and crossbow a troop carries is a generated `ladder_<line>_<bow|xbow>_t<tier>` item. Its
`missile_speed`, `thrust_damage` and `accuracy`, and the troop's own Bow or Crossbow skill, come
from `tools/ranged_ladders.json`: a curve per stat along the tiers, shifted by the kingdom's rank on
that stat. Five tools apply it (`generate_ranged_ladder_items.py`, `rebalance_ranged_ladders.py`,
`restat_ranged_donors.py`, `sync_ranged_ladder_translations.py`, and `rebalance_troops.py`, which
agrees on the skill), and the validator's `RANGED_LADDER_INVERSION` and `RANGED_DAMAGE_CEILING` keep
it true. Inside a kingdom a crossbow out-hits and out-aims every bow at the same tier.

The items live in the unversioned `LOTRLOME_Armory`, so an Armory reinstall drops all of them and
archers spawn bowless with no error. `python tools/generate_ranged_ladder_items.py --verify` is the
gate that catches it.

## Why This Exists

### What decides an arrow (Bannerlord 1.5.3, `SandBox.dll` and `TaleWorlds.MountAndBlade.dll`)

- **Reach** is the launcher's `missile_speed` and nothing else. `Mission.OnAgentShootMissile`
  launches at the wielded bow's `GetModifiedMissileSpeedForCurrentUsage()`;
  `SandboxAgentStatCalculateModel` sets `MissileSpeedMultiplier` to 1, and only two Throwing perks
  and wet weather move it. Range is native ballistics of that speed, gravity 9.806,
  `AirFrictionArrow` 0.003. The arrow's own `missile_speed` sets neither the launch speed nor its
  tier or price (`DefaultItemValueModel.CalculateAmmoTier` reads damage and stack size only).
  Managed code reads it for the inventory tooltip and for `ItemObject.Effectiveness` in a
  tournament auto-resolve; native receives it inside the ammo's `WeaponStatsData` on every shot,
  and whether native uses it is unverified.
- **Per-spawn modifier.** In a campaign mission every troop slot rolls a random `ItemModifier` from
  the item's `modifier_group` (`Mission.DecideAgentSpawnEquipment`, heroes excepted), and the
  modified damage and speed are what `OnAgentShootMissile` uses. Vanilla's `bow` group: none 45 of
  102, splintered 25 (damage -15), cracked 15 (-8, speed -6), balanced 10 (+1, +2), masterwork 5
  (+5, +3), legendary 2 (+7, +4). The 27 ladder crossbows carry the `crossbow` group: none 45 of
  102, cracked 25 (-10, -6), bent 15 (-5, -1), tuned 10 (0, +3), masterwork 5 (+3, +9), legendary 2
  (+4, +15). Accuracy has no modifier. That swing is larger than one tier step, so in one battle
  adjacent tiers can invert by engine design; the gates compare base values. A Custom Battle rolls
  no modifier (`CustomGame.IsCoreOnlyGameMode`).
- **Damage**: `CalculateStrikeMagnitudeForMissile` returns `(v_hit / v_launch)^2 x MissileTotalDamage`,
  where the total is the bow's `thrust_damage` plus the ammo's (`OnAgentShootMissile` passes the bow's
  as `damageBonus`). `ComputeBlowMagnitudeMissile` multiplies by `GetWeaponDamageMultiplier`, which
  for a bow is `1 + 0.0011 x Bow` (`DefaultSkillEffects.BowDamage`) and for a crossbow is 1. Armour:
  `scaled = magnitude x 50 / (50 + armour)`, Pierce `0.25 x scaled + 0.75 x max(0, scaled - 0.33 x armour)`
  (`ComputeRawDamage`); head or neck x2.0 for a Pierce missile (`GetDamageMultiplierForBodyPart`); a
  troop has 100 HP. So the launcher's number is almost all of the hit: Bow 200 adds 22%.
- **Accuracy**: `GetWeaponInaccuracy` is `(100 - accuracy) x (1 - 0.0009 x Bow) x 0.001` for a bow and
  `(1 - 0.0005 x Crossbow)` for a crossbow. Accuracy 100 is no spread at any skill; Bow 300 removes
  only 27% of the rest. Mounted, `SetMountedPenaltiesOnAgent` multiplies it by
  `1 + max(0, 5 - 0.05 x Riding)` (x6 at Riding 0, x1 from Riding 100). Moving and holding a
  drawn bow cost a bow more than a crossbow (x6 against x2.5 for movement), and an AI bowman's
  unsteady timer starts four times later. A Custom Battle uses none of this: its model defers to
  the base `AgentStatCalculateModel.GetWeaponInaccuracy`, `(100 - accuracy) x (1 - 0.002 x skill)`
  for both classes, so only a campaign battle shows the crossbow's accuracy edge as the gates
  compute it.
- **Skill** also drives the AI: `SetAiRelatedProperties` scales the lead, vertical and horizontal
  (up to 2 degrees) aim errors by `1 - aiLevel` and fire rate by `0.3 + 0.7 x aiLevel`, with
  `aiLevel = min(1, skill / 300 x 0.96)` on Realistic combat AI. `AiShooterError` is a flat 0.008 plus
  up to 30% in rain or fog and 10% at night.
- TAOM's registered `TaomAgentStatCalculateModel` and `TaomCombatMechanicsModel` leave every formula
  above to the Sandbox base. What they add is hero career passives on the final damage
  (`ApplyDamageAmplifications` / `ApplyDamageReductions`, ranged hits included), the refuge defender
  reduction, and the Culture Doctrine aggression pass over the AI error terms, which ships off. A
  troop-versus-troop arrow is pure engine outside a career hero's battle: there the leader's
  `TroopDamage` and `TroopResistance` passives multiply the hits their party's troops deal and take
  (`TaomAgentApplyDamageModel`), and the Infantry ability's ally buff adds `DamageMultiplierBonus`
  and draw speed to every agent in range (`CareerAgentStatService.ApplyAllyBuff`). The shield
  penetration flags `TaomCombatMechanicsModel` sets on missiles ship off, with empty lists.

### What was wrong

- **#582 (2026-09-12)**: the 227 troops carrying a bow or crossbow had no order of reach: six trees
  handed a higher tier a slower launcher, seven kingdoms' militia carried vanilla `noble_long_bow`
  over their own regulars, and Dol Guldur outranged every Gondor regular. 1,741 inverted pairs.
- **#617 (2026-09-18)**: archers nearly or fully one-shot troops. The #582 generator cloned each line's
  donor and changed only `missile_speed`, so every tier of a line carried the donor's damage and
  accuracy. A Rivendell T2 militia archer held a 105-damage, accuracy-100 Noldor longbow and landed
  about 48 on a T5 chest (armour 48), where vanilla T2 bows are 40 to 61 at a median accuracy of 85.
  Mordor's Black Numenoreans carried 107, the Iron Hills crossbows 120 to 130 with +8 bolts. Militia
  archers also carried Bow 120 to 180 at T1 and T2 against 45 to 70 for regular T2 archers, so garrison
  militia out-aimed their own kingdom's T4s. The ranked, per-tier model below was approved by the
  project lead on 2026-09-18 from an editable roster (the scratch generator is not in the repo).

## Architecture

### The two rules

Per launcher class (Bow and Crossbow are never compared with each other) and per stat (speed,
damage, accuracy, skill):

1. Inside a **line**, a lower tier never beats a higher tier.
2. At the same **tier**, a better-ranked line is never worse than a worse-ranked one, on that stat's
   own rank list. Equal ranks are not compared.

`validate_spec` refuses a spec in which any line's cells fail to rise with tier, so the spec itself
cannot express a rule 1 inversion. Rule 2 across ties and across stats is a property of the formulas.

**Crossbows against bows (Mike, 2026-09-18).** A crossbow reloads slower, so inside a kingdom (the
lines of one Armory folder: Gondor is `gondor`, `ithilien` and `blackroot`) a crossbow out-hits and
out-aims every bow at the same tier, compared as the engine applies them: a bow's hit carries
`1 + 0.0011 x Bow` and a crossbow's none, and spread is `(100 - accuracy) x (1 - 0.0009 x Bow)` for a
bow against `x (1 - 0.0005 x Crossbow)` for a crossbow, ammo at the restat caps (arrows +4, bolts +5).
Two spec values carry it: `crossbow_bonus` 9 (the most the 99 cap and the strict tier rise allow;
every crossbow has at least 42% less spread than its kingdom's best bow at the same tier) and a
crossbow damage curve that steps up at T7 and T8 (77 and 82), so Gondor's crossbowmen out-hit the
Ithilien and Blackroot rangers (82 against 80 at T7, 87 against 85 at T8, bolt and arrow included). The rule is not
across kingdoms: a Mirkwood bow may out-hit a Dunland crossbow. Pinned by
`RepoSpecTests.test_a_crossbow_out_hits_and_out_aims_its_kingdoms_bows`.

### Lines, ranks, cells

| Term | Definition |
|---|---|
| Engine tier | `clamp(ceil((level - 5) / 5), 0, 10)`, `ranged_ladder.engine_tier`, pinned to `taom_schema.Validator._troop_tier` by a test. The cap of 10 is TAOM's `TaomCharacterStatsModel.MaxCharacterTier` override (vanilla caps at 6) |
| Line | A troop file (`files`, the `troops_<culture>.xml` token) or an id prefix inside one (`prefixes`, matched first). Militia, bosses and horse archers ride their file's line |
| Ranks | Three lists per line, 1 = best, ties allowed: `overall` (drives speed and the troop's skill), `damage`, `accuracy` |
| Tiers | The tiers a line fields, per class (`tiers`). Each is a cell and a generated item; a troop at an unlisted tier is a finding |
| Band | E = T0-2, R = T3-4, V = T5-6, X = T7-8, C = T9-10. Bands now only pick the donor MESH (`donor_by_band`), so tiers in a band share a look |
| Speed | `speed.tier_base[t] + 3 x (worst overall rank - overall rank)` |
| Damage | `round(damage[cls][t] x (1 + damage.step[t] x (7 - damage rank)))` |
| Accuracy | `min(99, accuracy.top[t] - (accuracy rank - 1) (+9 for a crossbow))` |
| Skill | `max(10, skill.anchor[t] + skill.step[t] x (7 - overall rank))`, the troop's Bow or Crossbow |

The anchor rank 7 is the Gondor group, standing in for vanilla's typical culture. Vanilla's own
pattern (1.5.3 regulars) set the scale: its best archer culture, Battania, sits about 23% above the
typical cultures at T4 to T6. The damage step was 4% per rank at T1 to T6 and grew to 8% at T10. On
2026-11-14 (#1271) it became 3% at every tier and the rows were lowered (Bow 38 to 55, Crossbow 50
to 84), so Mirkwood sits 18% over Gondor at every tier and the top bow cell is 65 (Mirkwood T10), the
top crossbow cell 82 (Gondor T8). The old rows let Mirkwood T10 hit for about 90 on a T5 chest.
Accuracy cannot widen under its ceiling without a lower rank's T7 dropping under its T6, so accuracy
steps 1 per rank at every tier and the high-tier gap comes from damage and skill.

The skill anchor is the troop skill curve, `rebalance_troops.TIER_CURVE` (20 to 260), and the
skill step is 4% of it, rounded (1 at T1 to 10 at T10), so an archer of the anchor rank holds the
curve's number at its tier (#343; `LadderSpecTests` in `test_troop_skill_curve.py` pins both).

The #1271 damage rows (2026-11-14) were fitted at the old Bow skills, before #343 lowered the
curve, and were not refitted. A bow's hit carries `1 + 0.0011 x Bow` and its spread
`1 - 0.0009 x Bow` (`tools/ranged_ladder.py`), so on the top bow cell (Mirkwood T10, Bow 410
before, 320 now) the skill multiplier falls from 1.451 to 1.352, about 7% less hit, and the spread
factor rises from 0.631 to 0.712, about 13% wider. A crossbow's hit does not read Crossbow. The #343
design harness (body hits on 100 HP, the anchor rank 7 bow into same-tier armour) gives, T1 to T10,
35, 37, 31, 21, 24, 15, 18, 22, 23, 25 before and 35, 37, 30, 20, 22, 14, 16, 17, 19, 21 after.
Every bow cell hits less, so the R4 one-shot guideline below still holds; whether the bow rows
should rise back is a #1271 question this change does not decide.

The ranking, position 1 best: Mirkwood 1; Rivendell and Lindon 2; Ithilien 3 (`gondor_ith_*`,
`gondor_ithilien_*`); Blackroot Vale (`gondor_brv_*`), Arthedain and Rhudaur 4; Black Numenoreans 5; Harad, Dale (whose line also claims `troops_anduinvale.xml`, `troops_angaladh.xml` and `troops_dorwinion.xml`) and Cardolan 6; Gondor,
Rhun, Isengard and Umbar 7; Erebor (whose line also claims `troops_eredluin.xml`, so Ered Luin carries
Erebor's ladder bows and crossbows) and the Black Uruks 8; Gundabad, Dol Guldur, Mordor's orcs and the
goblins 9; Rohan (whose line also claims `troops_southrhovanion.xml`) 10; Dunland 11. Harad ranks 3rd on accuracy (after the elves). Erebor's bows and
crossbows rank 2nd on damage (crafting) and 8th on everything else. Crossbows rank among crossbows.
Khand has no troop file, so no archers.

### Items

Every tier each line lists, per class, is a generated item, 138 in all, a pure function of the spec.
Each is a verbatim clone of the line's donor for the tier's band (elf Longbow I to IV, Ithilien I to
III, Numenorean I/II, Erebor I/II, Dale recurve to longbow, Rhun steppe to Dragon longbow) with `id`,
`name`, `missile_speed`, `thrust_damage` and `accuracy` replaced (and `item_usage` where the line
overrides it, below) and `is_merchandise="false"`. That flag is `ItemObject.NotMerchandise`: it keeps
the clones out of the shops, and vanilla `DefaultBattleRewardModel.GetRandomItem` skips every
`NotMerchandise` slot when it rolls casualty loot, so a ladder bow never drops from a fallen archer.
The player's own bows are the donors, which stay in the shops and the loot pool. Mesh, difficulty and
flags are the donor's; a vanilla donor keeps its vanilla `culture=` (harmless while
`is_merchandise="false"` keeps the clones out of `DefaultTournamentModel.GetRegularRewardItems`).
One path does hand a ladder bow to a hero: Field Commission (`HeroCommissionAdapter`) copies the
troop's first battle set onto the new companion, so a commissioned archer keeps its ladder bow as
saved hero equipment. Before the 2026-11-14 rows twelve cells sat above `hero_ceiling`; none does now (the top bow
cell is 65, the top crossbow cell 82), but the Armory's own donors still reach 90 and 104. Mike's
decision (2026-09-18): swap it for the line's donor at commission (#625). The kept `difficulty` then gates only the player's
inventory screen (`CharacterHelper.CanUseItem`); no managed spawn, AI or loot code reads it, and
`MissionWeapon.GetWeaponData` hands it to native, where its use is unverified.

**A clone inherits the donor's art too, so fix the donor and regenerate.** `clone_launcher` is a
`copy.deepcopy` of the donor, and `--verify` checks ids, speed, damage, accuracy, usage and the
localization row but never `mesh` or `body_name`. In #633 three donors had no `bo_` twin of their
own and carried the elven bow's; six ladder clones inherited it, and the borrow was invisible to
every gate because the name resolves. Never hand-edit a `ranged_ladder.xml` to repair art: repoint
the donor in `LOTRAOM_weapons.xml`, re-run the generator, and let `COLLISION_BODY_BORROWED` confirm
it.

Names are `{=<id>}<donor name, its own numeral and "- Starting" / "- Horse" suffix stripped>
<tier numeral I..X>`, registered as English rows in the Armory's `Languages/loc_<folder>.xml`
between `<!-- TAOM-RANGED-LADDER:START/END -->` markers so `translate_with_claude.py --module Armory
--sync-ids` can seed the twelve translated languages. That flag only ADDS ids a language lacks and never
removes one, so when #617 retired the band ids every language kept 130 dead rows and had none of the
123 new ones (non-English players saw English bow names). `sync_ranged_ladder_translations.py` closes
that: a tier item's translated base is the translated base of the retired band item of the same line,
class and band (the donor is chosen by band), so it copies that base, appends the tier numeral, and
replaces the retired rows in place, keeping the files' doubled-CR line endings. No machine
translation; a row it cannot derive, or a `ladder_*` row it cannot parse, stops the run, and each live
file gets a `.bak-rangedsync` sidecar once. Applied 2026-09-18 to 156 files per tree. It also keeps
`tools/translation_cache/<lang>.json` in step: `rebuild_translation_files.py` (the recovery after an
Armory reinstall) resolves override, then cache, then English, so a cache still keyed on the retired
ids would rebuild every carried name in English. `--apply` sets each current ladder id in each cache
to the live row's text and drops the retired ids; `--verify` exits 1 when a language file or a cache
holds a retired id or lacks a current one.

**A rider cannot draw a `long_bow`.** Native's `long_bow` usage set is `base_set="bow"` plus
`requires_no_mount` and `requires_no_shield`; a mounted AI archer holding one never fires. The spec
overrides the usage per line with `"usage": {"Bow": "bow"}` (Harad and Dunland), the Armory's own
`wm_mirkwood_bow_a02` "LongBow II - Horse" pattern; `planned_edits(barred=)` refuses a mounted troop
in a cell whose effective usage is in the install's `requires_no_mount` set, and `RANGED_MOUNT_USAGE`
names any that exists.

**A line may name its own items.** A clone takes its donor's display name unless the line states
`"names": {"Bow": "..."}`; Arthedain's rangers carry Gondor's Ithilien bows under
"[Arnor] Ranger's Bow II" to "VII" (to VI on 2026-10-07, VII added 2026-11-06 for the Dunedain Steel
Bowman). An empty name is a spec problem.

### The Armory's own bows and ammo

The ladder items carry the troops; the Armory's own launchers are what lords, wanderers, named
companions and the shops hand out. `donor_stats` in the spec restats all 38 of them (hero-grade, top
`highelf_longbowd` 90, the Iron Hills Heavy Crossbow II 104, accuracy capped at 98) and `ammo_stats`
caps the Armory's arrows at +4 and its bolts at +5, vanilla's ceilings (27 items: the elven and
Mirkwood arrows from +5, the Isengard and Iron Hills bolts from +8). `restat_ranged_donors.py` edits
only those attribute values inside the matching `<Weapon>` tag, in the live Armory and the v1.5
mirror, and refuses an id that vanilla defines, that the Armory defines twice, or that it does not
define. It finds items in a comment-masked copy of the text (same length, so offsets carry over)
and edits the original, so a retired item kept as a comment is neither a second definition nor the
thing edited. `hero_ceiling` (Bow 90, Crossbow 105) backs `RANGED_DAMAGE_CEILING`: a launcher any
Lord, Wanderer or `is_hero` character can carry (its own equipment, its battle `EquipmentSet` rosters,
or a template `lords.xslt` hands to a retagged vanilla lord) above the ceiling is a finding, and so is
one in a `player_char_creation_*`, `player_career_*` or `enlist_*` (the enlistment quartermaster)
roster. No `NPCCharacter` names those; the game applies them to the player at runtime, the blind spot
`.claude/rules/moduledata-validation.md` describes for `MOUNTED_DWARF`. Civilian sets are skipped
whether the roster or an inner `<EquipmentSet>` carries the flag. Today the start and career rosters
hold 158 launcher slots, all `starter_*` twins at 40 to 45 damage, and the 84 enlistment rosters that carry a launcher (of 268) top
out at `crossbow_d` 93. The restat takes a `.bak-rangeddonor` sidecar for the live file only (the
mirror is a git repo) and refuses a `donor_stats` row with no recognised stat.

### Rosters and skills

`rebalance_ranged_ladders.py --apply` moves every `Item0..Item3` slot holding a bow or crossbow, in
every battle set, onto `ladder_<line>_<class>_t<tier>`, and sets the troop's Bow or Crossbow to its
cell through `rebalance_troops.apply_skills_via_regex` (the troop's full declared skill set with only
that value replaced). Ammo slots are never touched and a class never changes. Two guards:

- **Retired ids.** Regenerating the items removes ids the spec no longer lists (the #582 band ids
  `ladder_<line>_<cls>_<e|r|v|x|c>`, or a tier a spec change dropped) before the rosters are repointed.
  `retired_ladder_launchers` recognises a `ladder_*` id no loaded file defines by its shape and plans
  it as a launcher of the right class, so the slot is repointed rather than skipped.
- **The skill clamp.** `UPGRADE_SKILL_REGRESSION` is an error, so a child below its upgrade source on
  the written skill is raised to it (non-ladder children only, the way `rebalance_troops.py`'s clamp
  works); a ladder troop that would need raising off its cell is refused. Militia-to-militia edges and
  `RESPECIALIZATION_EXEMPT_EDGES` are skipped. The tool refuses to run when the militia bindings
  cannot be read (`rebalance_troops.militia_troop_ids` fails closed).

- **Templated characters.** On 1.5.3 a `skill_template` supplies a character's base skills and an
  inline `<skills>` row overrides that skill (installed `BasicCharacterObject.Deserialize` copies the
  template into a fresh `MBCharacterSkills`, then applies the inline rows; on 1.4.8 the inline block
  was ignored whenever the template resolved). An upgrade edge with a templated side is judged on
  the SkillSet with the inline rows laid over it (`sync_lord_inline_skills.effective_skills`, #626),
  as `UPGRADE_SKILL_REGRESSION` judges it. A templated troop is never written: a cell or a clamp it
  needs raises `LadderError` naming its SkillSet, because an inline row that differs from the
  SkillSet is `SKILL_TEMPLATE_MISMATCH`. The readers do the same: `troop_values` and `inversions` take
  `skill_sets` and read a templated troop's Bow or Crossbow through its SkillSet (the validator and
  `rebalance_ranged_ladders.py` pass them; #1490), and `analyze_troop_balance.py` loads them for
  `rebalance_troops.load_roster`. No troop file carries one today. The 22
  `characters/npcs_*.xml` files do, with no inline rows; all 1,164 templated lords in
  `characters/lords.xml` carry 18 inline rows each, which 1.4.8 ignored and 1.5.3 applies over the
  template. They equal their templates (`sync_lord_inline_skills.py`, gated by
  `SKILL_TEMPLATE_MISMATCH` since #626), so the bump changed no lord.
- **Cells nothing defines.** A roster naming a CURRENT tier id that no item file defines (the generator
  never ran, or an Armory reinstall removed the items) stops the run, report mode included: planned as
  a retired placeholder it would have passed the item check and read clean.

`rebalance_troops.py` takes a ladder troop's Bow or Crossbow from the same `skill_cell`
(`ladder_cells`), so a later full rebaseline never undoes the ranking. `troop_weapon_classes` reads
battle sets only, as the ladder does (`imladris_recruit` carries `highelf_longbowd` in a civilian
roster and gets no cell or Bow emphasis for it). Its monotonicity clamp refuses to raise a ladder cell
(a `RuntimeError` naming the troop), the same edge `planned_skill_edits` refuses, and it refuses to run
at all while the rosters name a ladder id no loaded file defines (`undefined_ladder_ids`), since those
troops cannot be classed. The Iron Hills noble crossbow line (`iron_hills_noble_scout` /
`_sharpshooter` / `_veteran_sharpshooter`) is on the Erebor cells, 120 / 160 / 195: Mike, 2026-09-18,
kept the ranking over the #366 hand-tune (175 / 225 / 275), since the line's edge is its crossbows (the
hardest-hitting at T4 to T6), and it is no longer in `SKIP_TROOP_IDS`. A full rebaseline is still not
part of this pass: its dry run on 2026-09-18 changed 77 troops for reasons unrelated to archery, and
it raises no ladder cell.

First #617 run: 709 inverted pairs to 0; 227 troops re-pointed (559 launcher slots) and 186 skill values (2 clamps:
`sagarun_marine` and `sagarun_storm_forged_marine` Bow 160 to 170, under the Rhun T5 skirmisher) over
16 troop files; 123 items; 65 Armory items restatted per tree.

### Component Diagram

```
tools/ranged_ladders.json  (stats curves, lines with ranks/tiers/donors, donor_stats, ammo_stats, hero_ceiling)
        |
        v
tools/ranged_ladder.py     cell, skill_cell, rank, tiers_for, ladder_id, line_of/line_for, index_launchers,
        |                  load_ranged_troops, load_upgrade_sources, inversions, unlisted, planned_items,
        |                  planned_edits, planned_skill_edits, retired_ladder_launchers, hero_launchers
        |
        +--> tools/generate_ranged_ladder_items.py   LOTRLOME_items/<folder>/ranged_ladder.xml + loc rows
        +--> tools/restat_ranged_donors.py           LOTRAOM_weapons.xml attribute values
        |                                             (ladder items: live Armory, the v1.5 mirror only with
        |                                              --asset-repo; restat: live Armory + v1.5 mirror)
        +--> tools/rebalance_ranged_ladders.py       reports; --apply: troops/troops_*.xml slots + skills
        +--> tools/sync_ranged_ladder_translations.py  the 12 languages' loc files (both trees)
        +--> tools/rebalance_troops.py               ladder_cells (the same skill cell; the clamp keeps it)
        +--> tools/taom_schema.py                    RANGED_LADDER_INVERSION, RANGED_MOUNT_USAGE,
                                                      RANGED_DAMAGE_CEILING (warnings)
```

## Configuration

### Config File: `tools/ranged_ladders.json`

```json
{"bands": {"E": [0, 2], "R": [3, 4], "V": [5, 6], "X": [7, 8], "C": [9, 10]},
 "stats": {"anchor_rank": 7,
   "damage": {"Bow": {"1": 38, "...": 0, "10": 55}, "Crossbow": {"1": 50, "...": 0, "10": 84},
              "step": {"1": 0.03, "...": 0, "10": 0.03}},
   "accuracy": {"top": {"1": 85, "...": 0, "10": 99}, "rank_step": 1, "crossbow_bonus": 9, "max": 99},
   "speed": {"tier_base": {"0": 54, "...": 0, "10": 78}, "rank_step": 3},
   "skill": {"anchor": {"1": 20, "...": 0, "10": 260}, "step": {"1": 1, "...": 0, "10": 10}, "min": 10}},
 "hero_ceiling": {"Bow": 90, "Crossbow": 105},
 "lines": [
   {"id": "harad", "folder": "harad", "files": ["harad"],
    "ranks": {"overall": 6, "damage": 6, "accuracy": 3}, "tiers": {"Bow": [2, 3, 4, 5, 6]},
    "donor": {"Bow": "wm_harad_bow_a01"}, "usage": {"Bow": "bow"},
    "donor_by_band": {"Bow": {"X": "wm_harad_bow_a02", "C": "wm_harad_bow_a02"}}},
   ...],
 "donor_stats": {"highelf_longbowd": {"damage": 90, "accuracy": 98}, ...},
 "ammo_stats": {"sm_dwarf_iron_bolt_a": 5, ...}}
```

`validate_spec` refuses: a line without all three ranks as positive integers; `tiers` that are empty,
out of 0 to 10, repeated, or for a class without a donor; a listed tier some curve does not cover; a
line whose speed, damage, accuracy or skill does not rise from each listed tier to the next; a
non-numeric curve value (reported, never raised); bands that leave a tier uncovered; a `folder` the
Armory does not register (generator); a missing or wrong-class donor; a `files` token with no
`troops_<token>.xml`; prefixes overlapping across lines; a `usage` for a non-ladder class or an empty
one; `donor_stats` / `ammo_stats` / `hero_ceiling` values that are not positive integers.

### The one-shot guideline (R4)

The maintainer set it on 2026-11-14 for the weapon and armour rebalance (#1271); it is a guideline
and the elite-into-recruit kill stays wanted. Against the model armour of all 25 kingdoms in
`rebalance_armor.KINGDOM_CAPS` (head `cap x 1.04`, body chest plus cape, at the band of tier `t`'s
level `5t + 1`), a cell's hit is `(damage + ammo) x (1 + 0.0011 x skill)` for a bow and `damage + 5`
for a crossbow:

- R4a: a bow cell is at most 95, a crossbow cell at most 105.
- R4b: no body one-shot when the attacker's tier is at most two above the defender's, or when the
  attacker's tier is 5 or lower.
- R4c: no same-tier one-shot of a helmeted head (missile pierce doubles a head hit).
- R4d: the spec's own rules (strict rise per line, a better damage rank never lower at a tier, a
  crossbow out-hits and out-aims its kingdom's bows, a clean `validate_spec`).

The one-shot rule is computed for a hit at launch speed against a still target; a target that charges
takes more.

Pinned by `RepoSpecTests.test_ranged_cells_respect_the_one_shot_guideline`, which holds the model
armour constants itself; `test_kingdom_caps.py` pins `rebalance_armor` to the same numbers. Real
troops are not covered: a troop under its armour band floor (`WORN_ARMOUR_FLOOR`) can still be
one-shot, and a bow's t3 hit can kill a helmet of 8.

### Current Values

These are the spec's cells. The live Armory carries them: on 2026-11-15
`python tools/generate_ranged_ladder_items.py --verify` printed "OK: all 147 ladder items present
with their speed, damage and accuracy in 1 tree(s)", so the 2026-11-14 damage rows (#1271) are in
its `ranged_ladder.xml` items.

| Line | Ranks (overall, damage, accuracy) | Class | Cells: tier speed / damage / accuracy / skill |
|---|---|---|---|
| mirkwood | 1, 1, 1 | Bow | T2 88/47/88/52; T3 90/50/91/88; T8 102/61/97/256; T9 105/64/98/288; T10 108/65/99/320 |
| rivendell | 2, 2, 2 | Bow | T2 85/46/87/50; T3 87/48/90/85; T5 91/53/93/155; T6 93/55/94/186; T7 96/57/95/217; T8 99/60/96/248; T9 102/62/97/279 |
| lothlorien | 2, 2, 1 | Bow | T2 85/46/88/50; T3 87/48/91/85; T4 89/51/93/120; T5 91/53/94/155; T6 93/55/95/186; T7 96/57/96/217; T8 99/60/97/248; T9 102/62/98/279 |
| ithilien | 3, 3, 4 | Bow | T7 93/56/93/210; T8 96/58/94/240; T9 99/60/95/270; T10 102/62/96/300 |
| blackroot | 4, 4, 5 | Bow | T3 81/46/87/79; T4 83/48/89/112; T5 85/50/90/145; T6 87/52/91/174; T7 90/55/92/203; T8 93/57/93/232 |
| arthedain | 4, 5, 4 | Bow | T2 79/42/85/46; T3 81/45/88/79; T4 83/47/90/112; T5 85/49/91/145; T6 87/51/92/174; T7 90/53/93/203 |
| rhudaur | 4, 5, 4 | Bow | T2 79/42/85/46; T3 81/45/88/79; T4 83/47/90/112; T5 85/49/91/145; T6 87/51/92/174 |
| mordor_num | 5, 5, 6 | Bow | T6 84/51/90/168; T7 87/53/91/196; T8 90/55/92/224; T9 93/57/93/252 |
| harad | 6, 6, 3 | Bow | T2 73/41/86/42; T3 75/43/89/73; T4 77/45/91/104; T5 79/47/92/135; T6 81/49/93/162; T7 84/52/94/189 |
| dale | 6, 6, 7 | Bow | T1 71/39/79/21; T3 75/43/85/73; T4 77/45/87/104; T5 79/47/88/135; T6 81/49/89/162 |
| dale | 6, 6, 7 | Crossbow | T3 75/55/94/73; T4 77/60/96/104; T5 79/64/97/135; T6 81/69/98/162 |
| cardolan | 6, 6, 7 | Bow | T2 73/41/82/42; T3 75/43/85/73; T4 77/45/87/104; T5 79/47/88/135 |
| gondor | 7, 7, 8 | Bow | T2 70/40/81/40; T3 72/42/84/70; T4 74/44/86/100; T5 76/46/87/130; T6 78/48/88/156; T7 81/50/89/182; T8 84/52/90/208 |
| gondor | 7, 7, 8 | Crossbow | T3 72/53/93/70; T4 74/58/95/100; T5 76/62/96/130; T6 78/67/97/156; T7 81/77/98/182; T8 84/82/99/208 |
| rhun_new | 7, 7, 8 | Bow | T2 70/40/81/40; T3 72/42/84/70; T4 74/44/86/100; T5 76/46/87/130; T6 78/48/88/156; T7 81/50/89/182; T8 84/52/90/208; T9 87/54/91/234 |
| rhun_new | 7, 7, 8 | Crossbow | T5 76/62/96/130; T6 78/67/97/156; T7 81/77/98/182 |
| isengard | 7, 7, 8 | Bow | T3 72/42/84/70; T4 74/44/86/100; T5 76/46/87/130 |
| isengard | 7, 7, 8 | Crossbow | T2 70/51/90/40; T3 72/53/93/70; T4 74/58/95/100; T5 76/62/96/130; T6 78/67/97/156 |
| umbar | 7, 7, 8 | Bow | T3 72/42/84/70; T4 74/44/86/100; T5 76/46/87/130 |
| umbar | 7, 7, 8 | Crossbow | T5 76/62/96/130 |
| erebor | 8, 2, 9 | Bow | T2 67/46/80/38; T3 69/48/83/67; T4 71/51/85/96; T5 73/53/86/125; T6 75/55/87/150 |
| erebor | 8, 2, 9 | Crossbow | T4 71/67/94/96; T5 73/71/95/125; T6 75/77/96/150 |
| mordor_uruk | 8, 8, 9 | Bow | T4 71/43/85/96; T5 73/45/86/125; T6 75/47/87/150 |
| mordor_uruk | 8, 8, 9 | Crossbow | T5 73/60/95/125; T6 75/65/96/150 |
| gundabad | 9, 9, 10 | Bow | T2 64/38/79/36; T3 66/39/82/64; T4 68/41/84/92; T5 70/43/85/120 |
| dolguldur | 9, 9, 10 | Bow | T2 64/38/79/36; T3 66/39/82/64; T4 68/41/84/92; T5 70/43/85/120; T6 72/45/86/144; T7 75/47/87/168; T8 78/49/88/192; T9 81/51/89/216 |
| mordor | 9, 9, 10 | Bow | T2 64/38/79/36; T3 66/39/82/64; T4 68/41/84/92 |
| goblin | 9, 9, 10 | Bow | T2 64/38/79/36; T3 66/39/82/64; T4 68/41/84/92; T5 70/43/85/120 |
| rohan | 10, 10, 11 | Bow | T1 59/35/75/17; T2 61/36/78/34; T3 63/38/81/61; T4 65/40/83/88; T5 67/42/84/115; T6 69/44/85/138; T7 72/46/86/161 |
| dunland | 11, 11, 12 | Bow | T2 58/35/77/32; T3 60/37/80/58; T4 62/39/82/84; T5 64/40/83/110; T6 66/42/84/132 |
| dunland | 11, 11, 12 | Crossbow | T4 62/51/91/84; T5 64/55/92/110; T6 66/59/93/132 |

## Key Files

| File | Role |
|---|---|
| `tools/ranged_ladders.json` | The spec |
| `tools/ranged_ladder.py` | Library shared by the tools and the validator (no CLI) |
| `tools/generate_ranged_ladder_items.py` | Items and English loc rows into the live Armory, the v1.5 mirror only with `--asset-repo` (`--apply`, `--verify` on speed, damage, accuracy and usage, `--revert`) |
| `tools/restat_ranged_donors.py` | `donor_stats` / `ammo_stats` into the Armory's own items (`--apply`, `--verify`) |
| `tools/rebalance_ranged_ladders.py` | Report to `tools/reports/ranged-ladders/` (md, html, json); `--apply` rewrites the launcher slots and the ranged skill |
| `tools/sync_ranged_ladder_translations.py` | The translated names move from retired ids to current ones in the 12 translated languages (`--apply`, `--verify`) |
| `tools/rebalance_troops.py` | `ladder_cells` + `troop_weapon_classes` (battle sets only): the ladder troops' Bow or Crossbow in a full rebaseline; the clamp refuses to lift a cell; `undefined_ladder_ids` refuses a run while the rosters name an undefined ladder id |
| `tools/translation_cache/<lang>.json` | The translator's cache; the sync tool keeps its ladder ids in step |
| `docs/reference/ranged-troops.html` | The tracked HTML, every ranged troop per kingdom; regenerated by the roster tool on `--apply` or `--docs-html <path>` (a dry run leaves it alone), never hand-edited |
| `tools/taom_schema.py` | `Validator._ranged_ladder_inversions` (emits `RANGED_LADDER_INVERSION`, `RANGED_MOUNT_USAGE`, `RANGED_DAMAGE_CEILING`), `_RANGED_LADDER_EXEMPT` |
| `tools/tests/test_ranged_ladder.py`, `tools/tests/test_restat_ranged_donors.py`, `tools/tests/test_sync_ranged_ladder_translations.py` | Synthetic-data tests over all of the above plus the shipped spec |
| `Main/_Module/ModuleData/troops/troops_*.xml` | The 285 rosters, edited through the tools only |
| `<game>/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/<folder>/ranged_ladder.xml` | 14 generated item files (unversioned; `--verify` is the reversion gate) |
| `<game>/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml` | The restatted donors and ammo (unversioned; `restat_ranged_donors.py --verify`) |

## Tests

`python -m pytest tools/tests/test_ranged_ladder.py tools/tests/test_restat_ranged_donors.py
tools/tests/test_sync_ranged_ladder_translations.py tools/tests/test_militia_elite_bonus.py
tools/tests/test_fix_upgrade_armour_regressions.py`: the
cell and skill formulas and their caps and floors on a fixture spec; spec validation (ranks, tier
lists, a falling curve, an uncovered tier, non-numeric values, structure, prefixes, files, donors);
the shipped spec (validates, 23 lines, the approved ranks, Mirkwood T10 108/65/99 and Bow 320, the skill anchor on the troop curve, no
better damage rank below a worse one at any tier, a crossbow out-hitting and out-aiming every bow of
its kingdom at the same tier, 147 items, the donor table under the ceiling, and the one-shot
guideline R4 on every cell);
the rules per stat (tier, same-tier rank, different tiers never compared, the worst set judged, ties,
classes); unassigned and unlisted troops; planned items, slot edits (retired ids repointed, unlisted
tier refused, mounted refusal, class refusal) and skill edits (cells, the clamp raising a non-ladder
child, refusing a ladder one, militia and exempt edges skipped, a templated source judged on its
SkillSet, a templated child or ladder troop never written, and a templated edge without SkillSets
refused; the tool refusing an install whose vanilla modules define no SkillSet, or an unreadable one);
`rebalance_troops` agreeing with the cell (the Iron Hills
nobles included), ignoring a civilian-roster bow in both readings, its clamp refusing to lift a cell,
refusing an undefined ladder id, and single-quoted militia bindings; hero launchers from inline kit,
rosters, `lords.xslt` and the player start, career and enlistment rosters, and the ceiling; the roster
tool end to end (dry run, BOM + CRLF kept, skills written and inserted, retired ids repointed, a
current cell no file defines refused, a deleted items file refused, idempotent, militia bindings
unreadable refused); the generator end to end (both trees, per-stat drift, loc rows, revert); the
restat tool (dry run, only the listed attributes change, BOM + CRLF kept on the mirror, no sidecar in
the mirror, idempotent, drift, unknown, vanilla, duplicate and attribute-less ids refused, an id
inside a comment ignored, a donor row with no known stat refused); the translation sync (dry run, rows
replaced in place with doubled CR kept, the split Gondor lines, idempotent, an underivable or
unparseable row refused, a bad spec refused, the cache kept in step, a sidecar once); the validator
gates. `tools/tests/test_fix_upgrade_armour_regressions.py` pins that the shared slot writer parses
every file before writing any.

## How to change the ranking or the curves

1. Edit `tools/ranged_ladders.json`: a line's `ranks`, a curve in `stats`, a line's `tiers`, or
   `donor_stats` / `ammo_stats`.
2. `python -m pytest tools/tests/test_ranged_ladder.py` (the shipped-spec tests pin the approved
   numbers; update them with the decision).
3. `python tools/rebalance_ranged_ladders.py --stdout`: read the cells and the inversions.
4. `python tools/generate_ranged_ladder_items.py --apply`, then `--verify`; for the donor and ammo
   tables `python tools/restat_ranged_donors.py --apply`, then `--verify`.
5. `python tools/rebalance_ranged_ladders.py --apply` (it repoints ids the generator just retired);
   `python tools/validate_moduledata.py`.
6. When item ids changed: `python tools/sync_ranged_ladder_translations.py --apply`, then `--verify`
   (it carries the existing translations and the translator cache across). Only for a name it cannot
   derive, run `python tools/translate_with_claude.py --lang <L> --module Armory --sync-ids --apply
   --allow-live-write` per language (needs `ANTHROPIC_API_KEY`), then the sync tool again to drop the retired rows. Then
   `python tools/check_external_loc_coverage.py`.
7. Commit the repo (spec, troop files, `tools/translation_cache`, the tracked HTML). Leave the
   `E:\repos\lotraom-assets` mirror alone: Mike syncs it himself in one pass (2026-09-22), so say in the
   commit body which live Armory files changed (`ranged_ladder.xml`, `LOTRAOM_weapons.xml`, `Languages`).
8. Restart Bannerlord fully (item XML loads at process launch) and check a troop from each end of the
   ladder in a Custom Battle. Custom Battle rolls no item modifier and gives bows and crossbows one
   spread formula, so the crossbow's accuracy edge over its kingdom's bows shows only in a campaign
   battle.

## How to verify in game

Not verified in game for the 2026-11-14 damage rows (#1271), which are not yet in the Armory. After
the apply (`generate_ranged_ladder_items.py --apply`, then `--verify`) and a full restart:

1. In a Custom Battle, check the tooltip damage of a Mirkwood tier 10 bow (65) and a Gondor tier 8
   crossbow (82).
2. Field same-tier elite archers on both sides (Gondor against Mordor). A body hit must not kill in
   one arrow. A helmeted head hit should not either, except on the real-troop cases the R4 section
   names (a troop under its armour floor, a tier 3 bow into a helmet of 8).
3. Field Mirkwood tier 10 archers against tier 1 to 3 recruits: a one-arrow kill there is wanted.
4. Check that a hero's bow (`donor_stats`, unchanged) still out-hits the troop ladder.

## Performance

Nothing at runtime: the items are ordinary `<Item>` rows and the rosters ordinary references. The
validator pass is a pairwise sweep over about 230 ranged troops for four stats plus one scan of the
character files for hero kit.

## Changelog

- 2026-09-12: created (#582). First run 1,741 inverted pairs to 0; 130 items, 258 slot edits.
  `docs/reviews/rca-ranged-ladders-2026-09-12.md`.
- 2026-09-13: the tracked HTML report and the militia veteran step (#588); Codex review fixes (the
  `long_bow` usage override, the loot claim, the worst-set rule). `docs/reviews/rca-ranged-ladders-codex-2026-09-13.md`.
- 2026-09-18: per-tier, ranked stats (#617). Damage, accuracy and the troop's skill join reach; each
  tier its own item (`_t<tier>`, 123 cells); ranks per stat; Ithilien and Blackroot Vale split; the
  Armory's own bows and ammo restatted (`restat_ranged_donors.py`); `RANGED_DAMAGE_CEILING`; the
  roster tool writes skills and repoints retired ids. 709 inverted pairs to 0.
- 2026-09-18: deep-review follow-ups (#617). Translations carried to the tier ids
  (`sync_ranged_ladder_translations.py`); comments masked in the restat tool; the slot writer parses
  all files before writing; `rebalance_troops` reads battle rosters only and its clamp refuses to lift a
  ladder cell; templated characters mirrored from the gate; player start and career rosters under the
  hero ceiling. `docs/reviews/rca-ranged-rebalance-2026-09-18.md`.
- 2026-09-18: second deep review (#617). Crossbows out-hit and out-aim their kingdom's bows
  (`crossbow_bonus` 9, crossbow damage T7 to T10 107 / 126 / 134 / 142; 27 crossbow items
  regenerated); the Iron Hills nobles take their Erebor cells; the roster tool refuses a cell no file
  defines; the sync tool keeps the translator cache in step, backs up and refuses an unparseable row;
  restat and sync validate the spec; `enlist_*` rosters under the hero ceiling; a templated ladder
  troop takes its cell (1.5.3 semantics); one militia regex and one class predicate across the
  writers and the gate. `docs/reviews/rca-ranged-rebalance-second-review-2026-09-18.md`.
- 2026-11-14: damage rows lowered (#1271). `stats.damage.Bow` is 38, 40, 42, 44, 46, 48, 50, 52, 54, 55,
  `Crossbow` is 50, 51, 53, 58, 62, 67, 77, 82, 83, 84 (T1 to T10) and `step` is 0.03 at every tier,
  so no cell is above `hero_ceiling` and no cell one-shots a same-tier helmeted head or a body
  within two tiers (rule R4, under Configuration). Skill, speed, accuracy, `hero_ceiling` and `donor_stats` did not
  move, so `rebalance_ranged_ladders.py` plans 0 slot and 0 skill edits; the live Armory items need
  `generate_ranged_ladder_items.py --apply`, and until then `--verify` reports the 142 items whose
  damage changed.
- 2026-10-15: KEYforce's Harad regular archers (lotraom-assets `db981663`) arrived carrying raw
  `hr_st_longbow_light/med/heavy` launchers; on their tiers a regular out-shot the tier-7 Serpent
  marksman. The port (#805) added tier 7 to the `harad` line (#803; `ladder_harad_bow_t7`, its
  translated names copied from tier 6), and `rebalance_ranged_ladders.py` put every Harad archer on its
  tier's cell: the six regulars on `_t3`/`_t4`/`_t5`, the Serpent and Red Serpent bowmen, archers and
  marksmen on `_t5`/`_t6`/`_t7`, with their Bow skill raised to the cells (12 values, Mike's choice).
  Inversions 398 to 0; no `RANGED_LADDER_INVERSION` remains in any culture. His three longbows ship as
  non-merchandise Armory items, waiting to become the line's `donor` art.
- 2026-11-15: the skill curve joins the troop curve (#343). `stats.skill.anchor` is 20, 40, 70, 100,
  130, 156, 182, 208, 234, 260 and `step` is 1 to 10 (T1 to T10), so Mirkwood T10 is Bow 320 (was
  410) and Rohan T4 is 88. `rebalance_troops.py --apply` wrote the cells with the rest of the
  restat; the roster tool plans 0 slot and 0 skill edits after it. The skill is not an item stat, so
  no Armory item changes (`generate_ranged_ladder_items.py --verify`: OK).

## GitHub Issue

[#582](https://github.com/haterade22/TAOM/issues/582), [#617](https://github.com/haterade22/TAOM/issues/617), [#1271](https://github.com/haterade22/TAOM/issues/1271), [#343](https://github.com/haterade22/TAOM/issues/343)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/orientation.md](../ai-includes/orientation.md)
- [docs/features/field-commission.md](./field-commission.md)
- [docs/features/troop-skill-balance.md](./troop-skill-balance.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/balance-levers.md](../modding/balance-levers.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
