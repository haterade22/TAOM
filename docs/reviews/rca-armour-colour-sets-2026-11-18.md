# RCA: armour band clone upgrades mixed silver with bronze (2026-11-18)

Issue: #1450. Change under review: worktree `taom-armour-colours`, branch `fix/armour-colour-families`.

## Top-line

The maintainer saw a Mirkwood troop in a silver chest with bronze pauldrons. The band clone change
(#1440, merged 2026-11-17) swapped the level 36+ wearers of a shared piece to a stronger piece of the same
line and slot, judged on armour values only. Lines such as Mirkwood's and Rivendell's ship one piece in
several metals under one line prefix, so the pick put silver pieces into gold and bronze sets. Clashing battle
sets rose from 206 before #1440 to 313 on trunk 686947589 (recounted 2026-11-18 with the table after the review fixes).

## Findings

| # | Finding | Root cause | Fix |
|---|---|---|---|
| C1 | 107 battle sets more clash on trunk than before #1440 | `pick_upgrade` judged values, bands and the upgrade gate, never the look | `colour_clash` refuses a candidate whose family clashes with a top wearer's set; such a piece falls back to a band clone |
| C2 | No tool knew an armour piece's colour | no data source | `tools/scan_armour_colours.py` builds a family table from names and textures, with a reviewed override file |
| C3 | Nothing reports a clashing set | no gate | validator warning `ARMOUR_COLOUR_CLASH`; data gate `armour-colours` keeps the table current |
| C4 | The career kit check failed after the new fallbacks | `generate_career_kits.py` did not know band clones: the clone tool gives a career kit the light clone of a source, the career tool picked the source | the career tool reads a clone as its source and gives a source's light clone (`career_armour`) |

Result on 2026-11-18, after the review round: 200 clashing sets, none that did not clash before #1440 (6 that
did no longer do). `python tools/validate_moduledata.py` reports `ARMOUR_COLOUR_CLASH` 41 and
`ARMOUR_CLASS_TABLE_DRIFT` 0.

## Review round (tier 1, 2026-11-18)

- R1 (high): the scanner wrote an all-unknown table, and exited 0, on an install with no texture image. Root cause: the image index was built inside `build()` and an empty index was a valid input. Fix: `image_index()`; `main` exits 2 with "no texture image" and writes nothing; the summary line counts the images.
- R2: exit codes did not separate a verdict from a crash. Root cause: SKIPPED exited 0, a bad override exited 2, a crash had no handler. Fix: SKIPPED and a missing numpy or Pillow exit 2, a bad override 1, a crash 3 through `entry()`; an explicit `--game-modules` without the Armory is an ERROR; the default install honours `BANNERLORD_GAME_MODULES` (`_gamedir`).
- R3: an override file saved with a byte order mark could not be read. Root cause: `read_text(encoding="utf-8")`. Fix: `utf-8-sig`.
- R4: `iron` made the Iron Hills Dwarf pieces silver by name (130 rows). Root cause: `iron` was in `WORDS` as a metal; it names the line. Fix: removed; the measure decides.
- R5: an override did not reach a band clone or a lord variant of its source. Root cause: the lookup used the item id only. Fix: `override_chain` (id, then `band_clone_source`, then `lord_variant_source`).
- R6: the unused-override guard was inline and untested. Fix: the pure `unused_overrides(ids, prefixes, item_ids)`, tested both ways.
- R7: the diffuse texture was chosen by a name suffix. Root cause: the slot was dropped on the way from the material (`material_assets`). Fix: `material_texture_slots` keeps the slot; slot 0 (`MBTextureType.DiffuseMap`) is the diffuse, the name rule is the fallback when no material decodes.
- R8: the unit tests failed on a machine without numpy or Pillow. Fix: a guarded import and `skipUnless` on the pixel tests.
- R9: the upgrade pick and the validator looked a family up by two different rules. Root cause: two copies of `table.get(...)`. Fix: one `scan_armour_colours.family_of` (own row, else the clone source's row, else unknown).
- R10: the clone tool planned on a table that lacked a piece of the Armory. Root cause: only an empty table was refused. Fix: `colour_table` raises `CloneError` for an empty table or for a piece of `read_sources` without a row (0 on 2026-11-18).
- R11: the validator passed silently on a missing or empty table. Fix: one `(table)` warning that the pass checked nothing.
- R12: the colour branch of `pick_upgrade` was untested. Fix: tests for a clashing candidate skipped, the fallback to A, a pick judged against an earlier swap, and the empty-table stop of `build()`; `ARMOUR_COLOUR_CLASH` tested in both directions.
- R13 (medium): the plan rewrote six slots that held an upgrade target natively (battle sets 5 and 7 of `imladris_marksman`, `lindon_imladris_marksman` and `fow_imladris_marksman`: `rivendell_torso_infantry_tier2_silver` and `_tier3_silver` became `_silvergold`). Root cause: the read-back turned every copy of a replacement back into its source by id, so a native copy was moved with the written ones. Fix: each upgrade row records in `wrote` the slots it wrote, the next plan reads back those slots only, and `write_changes` rewrites the listed battle sets only; the six slots are restored and `--check` is clean. The latent pairs the reviewer named hold their native slots, unchanged from trunk (`rohan_edoras_golden_hall_kings_own_rider` with `cts_rohan_captain_boots1`, `erebor_noble_veteran_axe_guard` with `sk_dwarf_erebor_chest_plate_elite_a`, `ironpass_mountain_guard` with `sk_dwarf_iron_boots_elite_a1`, and battle sets 6 and 8 of the three marksmen).
- R14: the Ranger chest read gold (`sk_ll_rg_chest_heavy_a`). Root cause: the shared Galadhrim atlas `t_ll_gl_chest_a_d`; its own texture `t_ll_rg_chest_a_d` is 94% brown leather. Fix: a `sk_ll_rg_chest_` prefix row, neutral.
- R15: data gates missed inputs. Root cause: the colour gate watched neither the art, the clone file, the lord variants nor SubModule.xml, and the validator and career-kit gates did not watch the table, the scanner or the clone tool. Fix: the globs, the exit-2 note and the rescan step in the `armour-band-clones` note.
- R16: the C# mirror of the career kit rule compared exact ids. Root cause: `StarterKitCoverageTests.TroopBattleItemsByCulture` did not read a band clone as its source, as `generate_career_kits.index_troop_gear` does. Fix: `BandCloneSource` on both sides.
- R17: documentation claimed more than the code did. Fix: the mesh chain (male non-slim pick, slot 0), what stops the scanner, the trailing colour suffix of the variant rule, the career tool reading a clone as its source, and the vanilla pieces with no row.

## Review round 2: green militia and the gold and bronze split (tier 2, 2026-11-18)

The maintainer saw a Mirkwood Militia Spearman in a gold helmet over the green Palace Guard chest. Decisions: the four militia troops (and their Fords copies) wear only the green Palace Guard set, and gold and bronze are separate families (a set is all gold, all silver or all bronze). Lenses 5 Data flow, 7 XML and Tooling at high; Codex skipped (out of usage).

- R18 (HIGH): moving the militia off the bronze pieces retired their clones, and the clone tool sent the villager, wanderer, character creation and Fords kits that wore those clones back to the elite sources (body 48). Root cause: the scope counts battle troops only, so a source with only kit wearers left was dropped. Fix: `kept_sources` keeps such a source cloned at its kits' bands, with tests.
- R19 (HIGH): the validator test `test_a_pair_that_does_not_clash_is_clean` still held gold beside bronze clean. Fix: the pair moved to a new test that it warns.
- R20 (MEDIUM): two helmet upgrades (T1 gold and T2 silver to T3) dropped with the militia, though they stay in one colour; the recruits and sentinels would wear T1 and T2 helmets and the two helmets would become elite shop items. Root cause: an upgrade lived only while its source was shared down. Fix: `held_upgrades` keeps a shipped upgrade while no low battle troop wears its source and the replacement does not clash; only the vambraces upgrade drops (bronze beside gold).
- R21 (MEDIUM): `kept_sources` kept a source cloned that low battle troops still wore. Fix: it requires no battle wearer of level 30 or lower (`low_anchor`), with a test.
- R22 (MEDIUM): the validator message said "silver with gold or bronze". Fix: "two metals (gold, bronze, silver)".
- R23 (LOW): the summary line counted kept rows as shared down; the spec description did not name kept or held rows; kept rows sorted after the scope. Fix: the line, the description and one sorted order.
- R24 (LOW): the Mirkwood enlistment recruit kits kept the gold T1 helmet over the green chest. Fix: the green T1 helmet, as the militia wear.

## Deferred

- FOLLOW-UP: #1452, step 2: the 200 sets that clashed before #1440, culture by culture with the maintainer's eye on each measured piece.
- FOLLOW-UP: #1452, the measured error still in the table: the `sk_ll_ml_` line; fix it with an override row in step 2. The Ranger chest (`sk_ll_rg_chest_heavy_a`) is fixed by a prefix row.
- FOLLOW-UP: #1452, vanilla `Armor` items that TAOM sets name are not scanned (29 vanilla ids in `troops_dunland.xml`, `troops_harad.xml` and `troops_umbar.xml` read `unknown`).
- FOLLOW-UP: #1452, two images that share a name are picked by path order; pick by the texture's source path.
- FOLLOW-UP: #1452, a cloth or leather texture can make a piece read warm (34 rows); each needs a Kit check, in step 2.
- FOLLOW-UP: #1452, 10 `fow_` copies in `fords_troops.xml` have clashing sets that nothing reports; the validator reads `troops_*.xml` only.
- FOLLOW-UP: #1452, lord template rosters, NPC rosters and equipment set rosters have no colour check (`ARMOUR_COLOUR_CLASH` reads `troops_*.xml` only).
- FOLLOW-UP: #1452, the gold and bronze split reads a warm piece by the larger share, with no margin: 16 warm textures have the smaller share at half the larger or more, and one texture gives `sk_rh_loke_chest_light_b` bronze but `sk_rh_loke_pauldron_lam_light_a` gold. The 15 troops the split made visible (Mirkwood guards and swordsman, eleven Rhûn wain and Sagarun troops, two South Rhovanion troops) need a Kit check first; an ambiguity band (a `warm` family that clashes with silver only) is one option.
- FOLLOW-UP: #1452, gold beside bronze in kits no gate checks: 21 character creation sets (16 Umbar), 9 wanderer sets and 1 NPC set (Mirkwood).
- FOLLOW-UP: #1452, elite pieces never in the clone spec sit raw in light kits (`mkwd_inf3_pauldrons` 20 sets, `mkwd_inf3_vambraces_bronze` 12); `BAND_CLONE_SOURCE_WORN_LOW` sees spec sources only.
- FOLLOW-UP: #1423, the restat apply on the live Armory for the three new clone sources (`mkwd_inf3_chest` 8 to 48, `mkwd_inf3_chest_bronze` 17 to 48, `mkwd_inf3_greaves` 3 to 22), then `generate_armour_classes.py --apply`, after the merge to trunk.

## Root-cause pattern

A swap tool judged what it could measure (armour values) and nothing told it what the player sees. The
same gap can hide in any tool that swaps a visible item: a weapon of another culture's look, a helmet that
hides a beard.

## Feedback memories to codify

None: the lesson goes in `docs/reviews/lessons/data-content-cultures.md`.
