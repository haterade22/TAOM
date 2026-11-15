# RCA: troop skills sit far above the vanilla range (#343, 2026-11-15)

## Top-line

TAOM's troop skills ran well above vanilla's. On the troop files of trunk `3849b1e01`, the median
main weapon skill at tier 10 was 390, where vanilla's whole range is 20 to 260 over six tiers. The
level came from four group tables (Infantry, Ranged, Cavalry, HorseArcher) keyed by level, with a
per-culture point modifier on top, tuned culture by culture over many changes. Nothing compared the
result to vanilla, so the curve drifted up with each culture that was tuned against its neighbours.
Recommendation B of the [roster analysis](troop-roster-analysis-2026-11-15.md) proposed spreading
the vanilla range over TAOM's ten tiers; the maintainer chose candidate C of the design.

The fix replaces the group tables with one curve and a role table read from the kit
([troop-skill-balance.md](../features/troop-skill-balance.md)). `TIER_CURVE` is 20, 40, 70, 100,
130, 156, 182, 208, 234, 260 over engine tiers 1 to 10. The main weapon takes the curve, a carried
sidearm 0.9 of it, an uncarried melee class 0.5 and an uncarried launcher or Throwing 0.4; Athletics
and Riding follow whether the troop rides, and Riding never sits under the troop's hardest horse.
The culture's row in `CULTURE_PCT` then raises or lowers each skill by a percent. Militia take the
tier 4 row and the elite slots +10 (was 15). The ranged ladder's skill spec anchors on the same
curve (Mirkwood T10 Bow 410 to 320). `python tools/rebalance_troops.py --apply` restated the roster.

Measured on the troop files, trunk against the result (`report_stats.py` in the session scratchpad,
read-only): 1120 troops in the files, 1116 changed, 3755 values rose and 4768 fell. The median main
weapon skill by tier, before and after:

| Tier | T0 | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8 | T9 | T10 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Before | 25 | 40 | 70 | 100 | 135 | 185 | 240 | 286 | 345 | 375 | 390 |
| After | 20 | 20 | 40 | 73 | 104 | 135 | 160 | 190 | 225 | 255 | 310 |

T10 sits over 260 because 12 of the 13 tier 10 troops are Lindon, Rivendell or Mirkwood, whose
rows are above the curve (Mirkwood's main weapon median 350 to 240). Harad went up (median 90 to 104, +15.6%); every other
culture went down, from -12.0% (southrhovanion, 100 to 88) to -38.5% (feral_orcs, 65 to 40), with
Gondor at -27.0% (185 to 135) and Mirkwood at -31.4%. `python tools/validate_moduledata.py`: 0 errors before and after;
`TROOP_SKILL_KIT_MISMATCH` 97 to 1, `UPGRADE_SKILL_REGRESSION` 0 to 0, `TROOP_ITEM_ZERO_NOT_MAIN` 0
to 0. No equipment line and no Armory value changed.

### The review round (2026-11-15)

The first review asked for these changes, all in this change:

- **The culture order.** The maintainer set the order of the peoples: the Elves best, the Dwarves
  next, then Men, the orcs under Men, each people keeping its strength (Rohan the best riders,
  Erebor and Iron Hills the heaviest two-handers). `CULTURE_PCT` was reset to it and
  `CultureTableTests` pins the order. The literal rule "no plain orc row over any mannish row" is
  not satisfiable with Harad's own TwoHanded -5 and Polearm -3 under every orc row, so the test
  compares the plain orcs with the mannish mean.
- **The Black Numenoreans.** `mordor_num_*` takes a mannish row of its own, `mordor_numenorean`
  (Athletics and Riding +3, the melee classes +5). `mordor_num_knight` (T8, horse difficulty 90) went
  from Athletics 180, Riding 310, OneHanded 350, Polearm 320 to 135, 215, 220, 195.
- **The villagers.** The 20 `villager_*` characters with a `<skills>` block take the tier 1 row and
  `--apply` writes only their skill values into `characters/npcs_*.xml` (147 lines). The clamp
  then lifts 17 recruit values from them, not 50: a farm tool or a stone the recruit does not carry.
- **A missing culture row** stops only `--apply` and the troops `--restat` names; the clamp-only
  modes report the troop and go on (`ClampOnlyRunTests`).
- **The crush-through skill window** scales with the curve: `skillDeadZone` 30 to 20,
  `skillTargetDelta` 200 to 135 (the JSON, the C# defaults, the tests, `combat-mechanics.md`). The
  exponent `k` and the race bonuses (elf +20 attack, dwarf +15 defence, in skill points) were not
  scaled.
- **The shipped roster is a fixed point of the writer**: every value on disk is what `--apply`
  plans (`test_the_shipped_skills_are_a_fixed_point_of_the_writer`). Five mutants of the role table
  each failed it (7 to 1378 values off).
- **Militia archers.** The elite step is +10 on every skill but one: a spare launcher the troop
  does not use is capped at its launcher cell, so on Ered Luin, whose basic militia cell is 38, the
  elite gets 38, not 50. The realm tests pin the exact step with that cap.

`--apply` in the review round changed 193 troop and villager records; a second `--apply --dry-run`
plans nothing.

## Engine facts (v1.5.7)

The reviewer confirmed these on the installed v1.5.7 build; each was re-read this round in the
decompiled DLLs (`pwsh tools/taom-src.ps1 path <Type>`):

- **The mount rule gates only mounting.** `Agent.CheckSkillForMounting` (`Agent.cs` 3645 to 3653)
  compares effective Riding with the mount's `MountDifficulty`. Its managed callers are the mount
  action (4826) and the use check (2703); the spawn path does not call it, so a rider under its
  horse's difficulty spawns mounted but cannot mount again once off it.
- **Combat speed does not read Athletics.** `CombatMaxSpeedMultiplier`
  (`SandboxAgentStatCalculateModel.cs` 801) is a lerp of the managed parameters
  `BipedalCombatSpeedMinMultiplier` and `BipedalCombatSpeedMaxMultiplier` by carried weight.
  Athletics sets the free top speed (`MaxSpeedMultiplier`, 790 to 797), the time to reach it, and
  the knockback and knockdown resistance (311, 321).
- **Simulated tournaments read troop skills.** `DefaultTournamentModel.GetTournamentSimulationScore`
  (line 56) is `(hero ? 1 : 0.4) x (max(OneHanded, TwoHanded, Polearm) + Athletics + Riding) x 0.01`,
  so a troop's simulated tournament score falls with the curve.
- **Wage, power and upgrade cost read tier or level, not skills.** `DefaultPartyWageModel`:
  `GetCharacterWage` switches on `Tier` (line 25), the recruitment cost on `Level` (225).
  `DefaultMilitaryPowerModel.GetDefaultTroopPower` reads `Tier` (246).
  `DefaultPartyTroopUpgradeModel`: the XP cost reads the tiers and the target's level (34 to 65),
  the gold cost is the difference of the two recruitment costs (76 to 81). No TAOM file under
  `Main/` whose name holds Wage, Power, Upgrade or Tournament calls `GetSkillValue`.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | The skill level was about one and a half times vanilla's at the top tiers (main weapon median 390 at T10). Weapon damage and armour were fitted on these skills (#1271), so a high tier troop hit harder than the weapon tables meant. | Curve with no outside reference | Every check compared troops to the curve or to each other; no gate held the curve to the vanilla range. | `TIER_CURVE` ends at vanilla's 260; `CurveTests` in `tools/tests/test_troop_skill_curve.py` pin it. |
| 2 | MEDIUM | Harad sat at about half of every other culture from tier 3 up, and nine culture files had no modifier row at all, so they took the bare group table. | Missing data read as a default | A missing `CULTURAL_MODS` key fell back to no modifier without a message. | `CULTURE_PCT` has a row for each culture; a culture with no row raises `MissingCultureRow` and stops the run (`CultureTableTests`). |
| 3 | LOW | Riding could sit under the troop's horse: `rohan_wold_recruit` had Riding 70 on a horse of difficulty 80. Such a rider spawns mounted but cannot mount again once off its horse (`Agent.CheckSkillForMounting`, v1.5.7, "Engine facts" above). | Rule keyed on the wrong input | The Riding curve was keyed by level and group, and no gate read the horse. | `troop_mount` reads the hardest Horse item's `difficulty` over the battle sets and loose equipment (`taom_schema.build_mount_difficulty_registry`); Riding is at least that. `test_no_mounted_troop_rides_under_its_horse` checks every shipped troop. |
| 4 | MEDIUM | Some lines were tuned by hand away from the formula: `gondor_loss_noble*`, `mordor_num_*`, the dwarf ram riders, `*_bolgs_ironfang`. A full rebaseline would have swept them, so none could run, and the formula and the files drifted apart (407 troops off it before this change). | Hand edits outside the source of truth | The writer reported the residuals and each run left them, so nothing forced the decision. | The restat put them on the curve. A second `--apply --dry-run` plans nothing, so any later hand edit shows as a change. |
| 5 | LOW | The elite militia bonus of 15 was sized for the old base of about 135; on the new tier 4 base of 100 it would be 15 percent. | Constant sized for a table that moved | The constant lives apart from the table it scales. | `MILITIA_ELITE_BONUS` is 10; `test_militia_elite_bonus.py` holds it between 10 and 20 and checks the step on disk. |
| 6 | LOW | Two writers held their own skill tables: `rebalance_troops.py` and the realm generators (`realm_troops.py`'s ATH, PRIMARY, SECONDARY, LOW, RIDING). A change to one left the realm files on the other. | Duplicated source of truth | Each generator's `--check` compares to its own table. | `realm_troops.role_skills` calls `rebalance_troops.calculate_skills`; the realm tables are deleted. |

## Where the code and the design differ

The design (candidate C, in the session scratchpad) was built in its order. Where it was silent or
the code disagreed, these readings were taken:

- **Tier 0.** The design keys N by tier 1 to 10. A tier 0 troop (level 5 or under) takes the tier 1
  row (`max(1, tier)`).
- **Rounding.** The design's preview rounded with Python's `round`, which rounds half to even. The
  code rounds to the nearest 5 with halves up, in exact fractions (`round5`), so a value at a half
  never depends on float error.
- **Name shifts.** The design let the shift become 0.1 N or be dropped. It is dropped: the role table
  already ranks the main weapon over the sidearm.
- **The archer's sidearm.** The design gives an archer's other weapons 0.9 of its launcher cell,
  rounded down to 5, and 0.9 N for the one archer with no ladder line. The code reads the troop's own
  launcher value in both cases, so the launcher stays on top for that troop too. An uncarried melee
  class is the lower of 0.5 N and that value, and a spare launcher is at most the main one.
- **Militia archers.** The design does not say how the archer rule meets the militia row. The code
  caps a militia archer's other weapons the same way, from the cell at its own tier, and does not
  add the elite +10 to the ladder cell or to the values read from it.
- **Throwing on an archer.** A javelin carried by an archer counts as a sidearm, not as a second
  main weapon.
- **The main weapon across kinds.** After the clamp, the main weapon is raised to the top melee or
  launcher value of the troop, unless it is a ladder cell, which the clamp may not lift.
- **The skip list.** The design lists `SKIP_TROOP_IDS` as not restated. The formula leaves them, but
  the upgrade clamp only raises, and eight of them sit under a restated parent: `goblin_spider_rider`
  and `goblin_spider_lord` Crossbow 10 to 30 and Throwing 25 to 30, `goblin_wolf_runner`,
  `goblin_dire_wolf_rider` and `goblin_wolf_lord` Crossbow 10 to 15, the three `gundabad_*wolf*`
  riders Crossbow 15 to 30. All of these are weapons the troops do not carry. The trolls and the
  elephant and mumakil riders did not move.
- **Counts.** The design's preview restated 1103 troops and skipped 17; `--apply` changed 1108 and
  skipped 12 (the skip list; no troop is skipped for tier 0 any more). The preview did not model the
  militia or `settle_purpose`.
- **The clamp after the restat.** The clamp raises 315 values on 263 troops (OneHanded 126,
  Athletics 77, Polearm 43, Throwing 32, Bow 18, TwoHanded 16, Riding 3), 298 of them from a troop
  parent and 17 from a `villager_*` parent; the purpose step raises 2 more after it. The design
  counted 141 edges with a drop before the clamp. (Before the villagers went on the curve: 368 on
  268, 50 from villagers.)
- **Uncarried values over their role value.** After the clamp, 230 values on 203 troops sit over
  the role table's value for a weapon the troop does not carry (OneHanded 121, Polearm 43,
  Throwing 32, Bow 18, TwoHanded 16); the review measured 261 on 208 before the villagers went on
  the curve. They are the clamp's lifts from a parent that did carry it. One troop's order still
  does not fit its kit, `dg_uruk_skirmisher` (follow-up below).
- **Bow and Crossbow percents.** They change a written value only on a carried launcher with no
  ladder line; one troop reads them, `feral_orcs_archer`.
- **Flat edges.** Seven upgrade edges outside the militia have equal skills on both ends
  ([troop-skill-balance.md](../features/troop-skill-balance.md), "Upgrade monotonicity"); the gate
  allows flat.

## Not verified

- No game run. The skills reach troops at agent spawn from the XML, so the change shows in new and
  existing saves; the battle feel at the new level is the maintainer's call in game.
- The damage tables in [troop-skill-balance.md](../features/troop-skill-balance.md) come from the
  design harness (`curve_sim.py`, `render.py`), which is not in the repo. The harness leaves out
  attack speed, blocking, the AI and thrown weapons.
- The mapping from the horse item's `difficulty` attribute to the agent's `MountDifficulty` was
  not traced, and no native caller of the mount check was searched.
- The war rams' difficulty reads 0 (the goats are not in the mount difficulty registry), so the
  Riding floor does not apply to the ram riders.
- The crush-through window's player effect ([combat-mechanics.md](../features/combat-mechanics.md))
  is computed from the formula, not seen in a battle.
- `generate_enlistment_rosters.py --repair-ranks` plans 0 rank repairs; its "weaker kits left" and
  "dropped categories" counts were not compared with trunk's.
- `fords_troops.xml` (Fords of War, Long Defeat) and `spider_creature.xml` are outside
  `troops_*.xml` and were not restated (the review round said not to restat the Fords file).

## Follow-ups

FOLLOW-UP #343: `dg_uruk_skirmisher` holds Bow 92 (its ladder cell) under OneHanded 105, raised by the clamp from its parent `dg_uruk_veteran_warrior`. It is the last `TROOP_SKILL_KIT_MISMATCH` (reason "melee skill above the launcher's ladder cell"; the other reasons count 0), so the code stays a warning. Decide the edge (an exempt respecialising edge, or a lower parent value), then flip `TROOP_SKILL_KIT_MISMATCH_SEVERITY` to error.
FOLLOW-UP #343: an in-game check at the new level: a tier 10 Mirkwood archer against a tier 8 Gondor line, a Rohan rider on its horse, and a militia defence of a castle.
FOLLOW-UP #343: the design harness that produced the damage tables (`curve_sim.py`, `render.py`) is only in a session scratchpad. Bring it into `tools/` with a test, or accept that the tables cannot be reproduced from the repo.
FOLLOW-UP #343: Fords of War copies. `fords_of_war/fords_troops.xml` holds `fow_*` copies of campaign troops at the old skill level, and the Long Defeat shop upgrades `fow_*` copies into campaign troops, so a restated campaign troop can sit under the copy that upgrades into it. Restat the copies on the curve and add `fords_troops.xml` to the upgrade graph the clamp reads; nothing in `rebalance_troops.py` may assume `troops/` is the only troop root.
FOLLOW-UP #343: `spider_creature.xml` carries its own troop skills at the old level. Decide whether it follows the curve.
FOLLOW-UP #343: stale skill tables in tools that are not run any more but would undo the restat if run: `tools/generate_gondor_troops.py`, `generate_rhun_troops.py`, `generate_dale_troops.py`, the `apply_*_troop_revamp.py` scripts and `apply_black_numenorean_troops.py`. Make `rebalance_troops.py` their source, or retire them.
FOLLOW-UP #343: the ranged ladder's #1271 bow damage rows were fitted at the old Bow skills; the top bow's hit fell about 7% ([ranged-ladders.md](../features/ranged-ladders.md)). Decide whether to refit them.
