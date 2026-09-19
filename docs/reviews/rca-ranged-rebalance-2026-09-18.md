# RCA: deep review of the per-tier ranged rebalance (#617, 2026-09-18)

> **Corrected by the second review** (`docs/reviews/rca-ranged-rebalance-second-review-2026-09-18.md`,
> the same evening). Its audit of this file found one fabricated "why missed" (item 6), one engine
> premise carried over from 1.4.8 (item 7), an incomplete refutation (item 10), a miscount in the top
> line and the root-cause pattern, and two sentences the v2.0.30 release made stale. Each is marked
> **[corrected]** below; the original claim is kept where it matters for the record.

**Top line.** Six review agents covered commits `b37fc22d`, `697cbdb6` and `c3592f62` plus the
unversioned writes into the live Armory and the `lotraom-assets` v1.5 mirror (`391f69b7`). Three
finished (live ladder XML, data flow, tooling); three hit the session rate limit (troop roster diff,
weapons restat, engine compatibility) and their checks were run by script afterwards, below. One
finding was live and player-visible: every non-English language showed English names for all 123
ladder bows, because the translation sync only ever adds ids and #617 renamed them. Everything else
was latent: correct on today's data, one edit away from a wrong write, except the handbook drift
(item 9), which was live in the docs **[corrected]**. One finding was refuted with engine evidence
(item 10, and that refutation proved incomplete **[corrected]**; the original said two) and one was
dropped as code that would check nothing. The generated items, the troop XML and the restat
themselves came back clean on every check.

## Findings

| # | Source | Sev | Bug | Category | Why missed | Fix and prevention |
|---|---|---|---|---|---|---|
| 1 | Live XML agent, data flow agent | HIGH | The 12 translated languages each still carried the 130 retired band rows (`ladder_<line>_<cls>_<e..c>`, the split `ladder_gondor_special_bow_*` among them) and none of the 123 tier ids, so `check_external_loc_coverage.py` failed at +132 to +137 per language and non-English players saw English bow names. | Rename treated as an addition | `translate_with_claude.py --sync-ids` only adds missing ids; the #617 plan filed "translator run owed" as a follow-up and nobody asked what happens to the rows being retired. The loc gate was not run before commit. | `tools/sync_ranged_ladder_translations.py`: a tier item's donor is chosen by band, so its translated base is the retired band row's base; copy it, append the tier numeral, replace the rows in place (doubled-CR endings kept), refuse an underivable row, `--verify`. Applied to 156 files per tree. Gate now +12 to +14 per language, none of it a ladder row except three inherited DE rows (item 13). Recipe step 7 in the feature doc runs it. |
| 2 | Tooling agent | MED | `restat_ranged_donors.py` searched raw text: a retired item kept as a comment beside the live one aborted the whole run ("defined 2 times"), and a comment-only copy would have been edited and reported as written. | Comment-blind text search | The sibling `rebalance_troops._strip_xml_comments` existed for exactly this; the new tool was written from `ranged_ladder`'s ElementTree readers, which never see comments, and the regex path had no comment fixture. | Items are found in a comment-masked copy of equal length and edited in the original at the same offsets (nothing is restored from the mask, per the offset rule in `.claude/rules/moduledata-validation.md`). Test `test_an_id_inside_a_comment_is_neither_a_definition_nor_edited`. Dead `current()` deleted. |
| 3 | Tooling agent | MED | `fix_upgrade_armour_regressions.write_changes` (the slot writer `rebalance_ranged_ladders.py --apply` uses) parsed and wrote each file in one loop, so a parse failure in file N left files 1 to N-1 written. | Partial multi-file write | The parse guard was added per file, inside the write loop; the restat tool written the same day did it right and nobody compared the two. | Every file is rewritten and parsed first, then all are written. Test `test_a_file_that_would_not_parse_leaves_every_file_unwritten`. |
| 4 | Data flow agent | MED | `rebalance_troops.clamp_upgrade_monotonicity` could lift a ladder troop's Bow or Crossbow off its cell (a full rebaseline would write it), while `ranged_ladder.planned_skill_edits` refuses the same edge. 0 of 227 troops today. | Two writers, two invariants | The ladder override was added to `rebalance_troops` as a value, not as a constraint the later clamp had to honour. | Records carry their `ladder` cells; the clamp raises a `RuntimeError` naming each lifted cell. Test `test_the_clamp_refuses_to_raise_a_ladder_cell`. Dry runs of both modes on the committed roster raise nothing. |
| 5 | Data flow agent | MED | `rebalance_troops.troop_weapon_classes` counted civilian rosters, the ladder does not: `imladris_recruit` and `lindon_imladris_recruit` hold `highelf_longbowd` only in a civilian set. Inert today because Rivendell lists no T4 bow cell. | Two definitions of "carries a bow" | The docstring asserted civilian sets are template refs with no weapons; two troops say otherwise. | New `battle_weapon_classes` (the ladder's reading) feeds `ladder_cells`; the old function is unchanged for the rest of the formula. Test `test_a_launcher_in_a_civilian_roster_is_not_a_ladder_cell`. |
| 6 | Data flow agent | LOW | `rebalance_troops.MILITIA_BINDING_RE` accepted double quotes only; `taom_schema`'s twin takes either. No single-quoted binding exists. | Two readers of one binding | **[corrected]** Both regexes were born in one commit (`c7392d5a`, 2026-08-31) with different quote handling (`rebalance_troops.py:351` double only, `taom_schema.py:822` either) and nobody diffed the twins. The original said the schema copy "was widened later", a history nobody had checked. The second review made them one object. | Either quote. Test `test_a_single_quoted_binding_is_read_like_the_validator_reads_it`. |
| 7 | Data flow agent | LOW-MED | Neither ladder writer knew `skill_template`: the gate skips an edge with a templated side, the ladder clamp judged it on the inline values alone. No troop file carries one today; `characters/lords.xml` and every `characters/npcs_*.xml` do **[corrected]**. | Two writers, one gate, one engine rule | The ladder planner was built against the troop files, where the attribute does not occur. | `RangedTroop.templated`; the clamp skips such edges. **[corrected]** The premise "the engine reads the template, not the inline block" is the 1.4.8 rule: on the installed 1.5.3 `BasicCharacterObject.Deserialize` copies the template into a fresh `MBCharacterSkills` and then applies the inline rows, so an inline row overrides. The refusal this fix added (a templated ladder troop a `LadderError`) rejected a configuration that works on 1.5.3; the second review removed it. |
| 8 | Data flow agent | LOW | `RANGED_DAMAGE_CEILING` never saw the `player_char_creation_*` / `player_career_*` rosters (applied to the player at runtime, named by no `NPCCharacter`). 158 launcher slots, all `starter_*` at 40 to 45 damage: no breach. | Runtime-applied data outside the sweep | The documented `MOUNTED_DWARF` blind spot, not carried to the new gate. | `hero_launchers` counts those rosters by prefix. While there: roster reads now skip an inner `<EquipmentSet equipmentType="Civilian">`, which the lord path had been counting. Test `test_hero_launchers_counts_the_player_start_and_career_rosters`. |
| 9 | Data flow agent | MED | `docs/modding/troops.md` worked example (marked as a copy of `gondor_ithilien_ranger`) still showed `ladder_gondor_special_bow_c` and Bow 320; the troop has `ladder_ithilien_bow_t10` and 380. A sweep found a second: the crafting chapter's `highelf_longbowa` example showed 105 damage and accuracy 100 (live: 72 and 98) and named a retired clone. | Doc example drift | `check_handbook_attributes.py` checks attribute NAMES against the dump and that the example id exists, never the values. | Both examples corrected; the clone sentence now names `ladder_rivendell_bow_t2` on `rivendell_militia_archer` (Bow 105), both read from disk. The checker gap is an open follow-up, below. |
| 10 | Live XML agent | MED | Refuted. `ladder_harad_bow_t2` and `ladder_rivendell_bow_t2` keep their donors' `difficulty="100"`, and the agent reasoned a player who loots one could not equip it. | | | No vanilla path hands a player a ladder item: `is_merchandise="false"` sets `NotMerchandise`, and v1.5.3 skips it in casualty loot (`DefaultBattleRewardModel.GetRandomItem` :109 and :137), hideout loot (`HideoutCampaignBehavior` :144) and tournament prizes (`DefaultTournamentModel` :101). **[corrected]** TAOM's own Field Commission does: `HeroCommissionAdapter` copies the troop's first battle set onto the new companion, ladder bow included, and the player can move it to the inventory, where `difficulty` then gates the player. `difficulty` has no MANAGED spawn, AI or loot reader (it is handed to native in `MissionWeapon.GetWeaponData`, unverified there). The original said "a ladder item cannot reach a player". Mike chose to swap a commissioned archer's ladder bow for the line's donor (#625). |
| 11 | Data flow agent | LOW | Dropped. `RANGED_DAMAGE_CEILING` does not scan the live `LOTRLOME_Armory` and `TAOM_Map` roots. | | | Neither module defines a Lord, Wanderer, `is_hero` character or `EquipmentRoster` (grep, 2026-09-18), so the added roots would check nothing (`simplicity-criterion.md`). The Armory holds items and monsters by rule. |
| 12 | Data flow agent | LOW | The 12 translated `loc_gondor.xml` files held 15 retired rows each, 180 in all, and no cleanup path existed. | | | Removed by item 1's tool: the only `ladder_*` band rows left under `Languages` were in the #582 `.bak-rangedladder` sidecars, an extension the engine never globs. **[corrected]** The v2.0.30 release's `sweep_module_backups.ps1` has since moved every live sidecar to `E:\Bannerlord_Backups\module_bak_sweep_2026-09-18\`. |
| 13 | Found while closing item 1 | INFO | DE shows "Arbalest V" to "VII" for the Rhun crossbows, identical to English, so the gate counts three rows. | | | Inherited: DE held "Arbalest I" to "V" on the retired rows too (mirror `HEAD`). Five identical rows became three. A translation choice, not a sync defect. |

## The three agents that hit the rate limit, done by script

- **Troop roster diff.** A parsed comparison of every troop in the 16 files at `697cbdb6^` and
  `697cbdb6`: 559 slot changes, all ladder id to ladder id in the same slot of the same set; 186
  Bow/Crossbow values; nothing else (attributes, upgrade targets, element counts, other skills, set
  counts). BOM state, line-ending style and line counts unchanged in all 16. On the current tree:
  0 ladder ids naming another line or tier than the troop's own, 0 ladder troops off their skill
  cell, 0 ladder-line troops still holding a non-ladder launcher, 0 battle sets with a launcher and
  no ammo of its class.
- **Weapons restat.** Live `LOTRAOM_weapons.xml` against its `.bak-rangeddonor`: 73 changed lines,
  every one inside one of the 65 targeted items and differing only in the digits of `thrust_damage`
  or `accuracy`; all 65 touched. Same bytes after line-ending normalisation as the mirror copy; all
  13 `ranged_ladder.xml` files likewise. **[corrected]** That sidecar was moved by the release sweep
  (above); the proof is reproducible from the mirror, where `391f69b7` changes
  `LOTRAOM_weapons.xml` by 146 lines, the 73 out and 73 in.
- **Engine compatibility.** Every claim in `tools/ranged_ladder.py`'s header and the feature doc
  read against the v1.5.3 dump: `Mission.OnAgentShootMissile` (launch speed and `damageBonus` from
  the wielded bow), `SandboxStrikeMagnitudeModel.CalculateStrikeMagnitudeForMissile`
  (`(speed / base)^2 x MissileTotalDamage`), `GetWeaponDamageMultiplier` (Bow `0.0011` add factor,
  no crossbow branch), `ComputeRawDamage` with the Pierce blunt factor `0.25` and `0.33 x armour`,
  `GetDamageMultiplierForBodyPart` (head and neck x2 for a human-hit missile), `GetWeaponInaccuracy`
  (`-0.0009` Bow, `-0.0005` Crossbow), `SetMountedPenaltiesOnAgent` (`5 - 0.05 x Riding` as a
  factor), `CalculateAILevel` (`skill / 300 x 0.96` on Realistic), `SetAiRelatedProperties` (error
  terms, `AiShootFreq = 0.3 + 0.7 x level`, `AiShooterError` 0.008), the weather factor, the two
  Throwing perks and wet weather on `MissileSpeedMultiplier`, `MBGlobals.Gravity` 9.806 and
  `AirFrictionArrow` 0.003 in the install's `managed_core_parameters.xml`. All confirmed.
- **C# shipped-data tests**, which the data-flow agent could not run: `dotnet test TAOM.Tests`
  (no deploy) 9,791 passed, 0 failed, 2 skipped, on a tree that also held another session's
  uncommitted war-ram work.

## Root-cause pattern

Items 4 to 7 **[corrected: the original said 4 to 8; item 8 is a coverage gap with one
implementation]** are one defect family: **each tool that writes or gates a value keeps its own
definition of the inputs it classifies on.** "Carries a bow", "is militia", "are these skills the
ones the engine reads" and "may the clamp raise this" each had two or three implementations
(`rebalance_troops`, `ranged_ladder`, `taom_schema`) that agreed on today's data by coincidence.
#617 added a second writer of the ranged skill and inherited the first writer's definitions without
comparing them. Item 1 is the same shape across a process boundary: the generator retires ids and
the translation tool, which only adds, was trusted to follow.

## Lessons codified

- `docs/reviews/lessons/localization-ui.md`: an add-only id sync reads a rename as an addition.
- `docs/reviews/lessons/data-content-cultures.md`: two writers of one value need one definition of
  every input they classify on.

## Open follow-ups (not fixed here)

- `check_handbook_attributes.py` could compare an example's attribute VALUES with the element it
  claims to copy. 152 markers across 40 docs; some examples are deliberately trimmed, so it needs a
  design pass for which attributes are pinned, not a one-line change.
- In-game: full restart, `/armory-audit`, Custom Battle smokes (owed since #617), plus one
  non-English client to see the carried names.
