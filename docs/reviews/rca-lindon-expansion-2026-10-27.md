# RCA: Lindon expansion (#1039), deep review 2026-10-27

## Top line

Mike placed two towns, three castles and 18 villages for Lindon on the live map and asked for their rows, owners
and names. The change wrote the live `TAOM_Map` rows with the map tools, added four clans and 13 lords with a
one-off, gave `clan_lindon_2` Harlond, and renamed three settlements so no name repeats. `/deep-review` ran at tier
1 (live `TAOM_Map` data): wave 1 was 5 Data flow, 2 Engine compatibility, 7 XML and Tooling; a HIGH sent wave 2
(3 Efficiency, 4 Completeness, 6 Design); one convergence pass checked the fixes. Codex did not run: no paid
dispatch was authorised for this change. The review confirmed 1 HIGH (older than this change), 6 MEDIUM and 15 LOW
findings; 16 are fixed here, the HIGH is documented and filed (#1041), and 5 are follow-ups.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Once the distance cache is rebuilt for a batch, a save made before it is expected not to load: `NavigationCache.Deserialize` gets null from `Settlement.Find` for a dropped id, `SettlementPositionScript.OnInit` registers no cache, and `Campaign.CalculateCachedValues` dereferences it (v1.5.6 decompile, not reproduced). The docs and tools said such saves load. Live since the 2026-10-23 rebuild for #978 | Engine claim checked one layer deep (pre-existing) | The save-order rule was derived from `Settlement.Deserialize` alone; nobody followed the dropped ids into the cache the same recipe rebuilds at step 7 | Claims corrected in `settlements.md` "Add", `arthedain.md`, `cardolan-rhudaur.md`, both map tools; FOLLOW-UP #1041 (guard needs C#, after `/engine-bump`) |
| 2 | MED | The four new clans reused `clan_lindon_2`'s party template, whose 14 stacks are Rivendell troops, so their lords would lead "[Rivendell]" units; clans 1 and 2 already did | Reused data without reading it (pre-existing in clans 1 and 2) | The plan copied the Dale precedent "reuse an existing template" and checked only that the template id exists | Mike chose to re-point both Lindon templates' 27 stacks at their Lindon twins; `rivendell.json` synced; lesson in `data-content-cultures.md` |
| 3 | MED | `external-loc-coverage-baseline.json` was refreshed before the last live loc write (the Ninglas rename), so the gate failed again | Gate recorded before the last write | The baseline step sat in the middle of the run, and the later rename was a new request | Refreshed after the last write; lesson in `build-tooling-workflow.md` |
| 4 | MED | `author_lindon_clans.py` lacked the `--rerun-retired` refusal its Dale model carries; a re-run after an engine bump would pick new faces | Copied pattern without its guard | The guard was dropped on purpose while the one-off was still being run, and not restored | Refusal added; `tools/README.md` row |
| 5 | MED | This change's first warning called the 421 to 438 differing rows per language in `loc_settlements.xml` "translations" `Apply-MapVillageNames.py` would overwrite; they are pre-rename text (DE "Dorf E2_1" for Linenhold) that the tool's missing cache eviction let the translator write back | Took a differing row for a translation | The count was measured, the text was not read | Warnings, steps and lesson corrected in four docs and the tool docstring |
| 6 | MED | The live writers never check that a scene name has a `scene.xscene`; four villages were written onto the empty `battania_village_c` and `_f` folders and caught by hand | Gate covers rows, not their content (pre-existing tool gap) | `--check` proves rows, loc rows and positions, not scene folders; `audit_scene_names.py` exits 0 even with suspects | Data fixed here; Follow-up closed 2026-11-04 (c30c4e618, ad8f13d00) no issue: needs Mike's word to file (a writer guard reusing `audit_scene_names.all_scene_folders`) |
| 7 | LOW | No repo gate held the three renames; a reinstall plus `add_map_*.py --apply` would revive Harlond, Forlond and Edhellond twice each | Unversioned module without a gate | The renames were written by scratch one-offs | `test_renamed_settlements_keep_their_new_names_in_every_language` |
| 8 | LOW | The face test accepted any `lords.xslt` template as replacing a donor's face; the one-off required one that writes `<BodyProperties>` | Test weaker than the rule it guards | Copied from the Dale test | Test uses the one-off's rule and fails when no donor is found |
| 9 | LOW | The hero test passed with a lord moved between two new clans | Test asserted a subset | Written against the owner rows only | `LORD_CLANS` pinned |
| 10 | LOW | The one-off said archers stay Infantry and lords show visual age 22.01; heroes field from equipment (every roster carries `Item.charger`, so Cavalry) and render at `age=` | Engine claim not checked | Written from the XML, not `CharacterObject.GetFormationClass` / `Hero.Age` | Docstring corrected; feature doc says Cavalry |
| 11 | LOW | The bard doc said five lone towns; `town_RH1` has been one since #978 | Count not re-measured | Only Lindon's town was checked | Six towns listed, measured from the live file |
| 12 | LOW | The feature doc said every Lindon clan has two or more married ladies, that Lindon reuses Rivendell party templates wholesale, and that its culture is `rivendell` | Prose not swept | The doc pass edited only the lines naming fiefs | Corrected, plus a #1039 section with saves and an in-game checklist |
| 13 | LOW | No feature-map row for the Lindon factions or this batch | Documentation duty | Same | `LindonExpansion` row |
| 14 | LOW | `recipe-add-a-kingdom.md` named Rhudaur the smallest realm on all three counts | Claim beyond the measurement | Two of three counts were checked | States each minimum |
| 15 | LOW | The face test uppercased every vanilla file once per key (2.5 s) | Quadratic scan in a test | Copied from the Dale test | Each file scanned once (0.9 s) |
| 16 | LOW | Issue #1039's body described the plan, not what ships | Records not swept | Same as 12 | Body rewritten |
| 17 | LOW | The map writers do not check a new name against names already on the live map | Pre-existing tool gap | The duplicate-name check is table-internal | Fixed in #1042: `add_map_villages.name_collisions` in both writers' `--check` and `--apply` |
| 18 | LOW | `add_map_fortifications.py`, `add_map_villages.py` and `rename_map_settlements.py` parse each output as they write it, not all before any | Pre-existing (#979 rule) | Not in this diff | FOLLOW-UP no issue: needs Mike's word to file |
| 19 | LOW | `generate_new_faction_settlements.py --apply` would re-append Mithlond's rows as `Culture.rivendell` at old prosperity, after the Lindon batch | Pre-existing live writer hazard | Not in this diff | Fixed in #1042: the generator only appends missing ids, with today's cultures |
| 20 | MED | Outside this change: the renamer's missing cache eviction left about 421 renamed villages per language on pre-rename text in 11 languages | Pre-existing | Found by the Design lens while checking finding 5 | Fixed in #1042: 4,633 rows reset to English and evicted for #763; the renamer now evicts its renames |
| 21 | LOW | The dedicated-server stage holds the old TAOM_Map and TAOM data | Copy outside the deploy path | Not part of the build | Owed: re-stage after the deploying build (also open from the Tharbad RCA) |
| 22 | LOW | The distance cache predates the 23 new rows | Owed step | Needs the game | Owed in #1039's checklist (and see finding 1 before rebuilding) |

Not applied, with reasons: registering the renames in `Apply-MapVillageNames.py` and giving it a cache-evicting main
(Design P1, P2: a rewrite of a tool outside this change; follows finding 20); merging the Dale and Lindon face tests
(P6: the Dale test is outside this change); `generate_new_faction_kingdoms.py:69` (its own comment calls the Lindon
entry history, it cannot regenerate Lindon, and `recipe-add-a-kingdom.md:164` quotes it); the attribute-order
assumption in the test regexes (one writer, fixed order).

## Root-cause pattern

**A check that measures one layer or one file and is then trusted for all of them (1, 2, 5, 6, 8, 14).** The save
rule was read in `Settlement.Deserialize` but not in the cache; the party template was checked for existence, not
content; the loc rows were counted, not read; the scene name was checked for a folder, not a scene; the face rule
was copied without the clause that made it strict; the realm floor was checked on two of three counts. The fix for
the class is to name, before trusting a check, every place the checked thing flows to, and read one sample of each.

**Steps out of order (3).** A gate that records a baseline must run after the last write it covers.

## Why each lens found what it found

- **Engine compatibility** found 1 by following the dropped settlement ids from `Settlement.Deserialize` into the
  cache reader, and 10 by reading `GetFormationClass` and `Hero.Age`.
- **Data flow** found 2 by following `default_party_template` to its troops, and 3 by re-running the gate.
- **XML** found 3 and 7 by running every gate and asking what a reinstall would revert.
- **Tooling** found 3, 4, 6, 9, 17, 18 and 19 by running the writers' edges and the two-edit test.
- **Completeness** found 11 to 14 and 16 by sweeping prose against data.
- **Design** found 5 and 20 by reading what the differing rows actually say.
- **Efficiency** found 15.
- **Codex** did not run. **Standards** was not in scope (no C# or harness files).

## Feedback memories to codify

None beyond the lessons entries: `lessons/data-content-cultures.md` (read a reused template's contents) and
`lessons/build-tooling-workflow.md` (record a baseline after the last write).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/data-content-cultures.md](lessons/data-content-cultures.md)

<!-- backlinks-end -->
