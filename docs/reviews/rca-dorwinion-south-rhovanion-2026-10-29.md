# RCA: Dorwinion and South Rhovanion deep review, 2026-10-29

## Top line

`/deep-review` at tier 1 (live `TAOM_Map` and Armory data, C# lists, generators) ran one wave of four
lenses: 5 Data flow, 2 Engine compatibility, 7 XML and ModuleData, and Tooling. Codex did not run:
#1062 had no word for paid dispatch. The first wave found two HIGHs (Engine F1, Tooling T1), so a
second wave ran Completeness and Design on the files involved; Completeness found a third HIGH. Mike
decided the three behaviour-changing items in one question (2026-10-29). Of 23 confirmed findings, 19
are fixed in this change and 4 are follow-ups with an issue or a stated reason. One convergence
pass over the fixes (Tooling lens) found four more: three fixed, one follow-up. One claim was refuted:
the XML lens read a stale report file and called the distance cache unrebuilt; the cache file itself
is dated 14:31:53 and names `town_SRH1`.

The work is the two Neutral kingdoms of Dorwinion and South Rhovanion
([dorwinion-south-rhovanion.md](../features/dorwinion-south-rhovanion.md), #1062), uncommitted on
`feat/dorwinion-south-rhovanion` over `0799af376` during the review.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | At 14:19:59 another session deployed a trunk build (`cd06da904`) over this branch's build, while the live map named `Culture.dorwinion`, `Culture.southrhovanion` and their clans on 61 rows; a new campaign on that deploy throws at notable spawn (placeholder cultures with null feats and notable templates), and a 14:20 session stopped there (Engine F1, XML 1) | Two-module coupling | Mike chose to retag live with the branch deployed; that is safe only while no other checkout deploys. **Repeat** of the #1053 class (its RCA row 1; #1055), now through a deploy, which no validator sees | The 61 rows put back on their interim Dale/Rohan values at 14:55:49; the final retag only in the merge sitting, right after deploying the build that defines them; Mike tells the other session to merge trunk before its next deploy. Lesson amended in `lessons/build-tooling-workflow.md`; #1055 comment |
| 2 | HIGH | `test_generator_regions_in_sync.py` had no kingdom-generator or promoter rows for the two realms (Tooling T1) | Hand-kept gate list | **Repeat** of #1053 RCA row 3: the fix then added rows by hand, so the next realm was missed again | The test derives its lists from `REGENERATABLE` and `TARGETS` (D1, Mike approved) |
| 3 | HIGH | After the merge, the other session's checkout (14 uncommitted files overlapping this diff) cannot fast-forward and would deploy without the cultures again; nothing recorded it (Completeness 1) | Cross-session hazard | The reviewers of one branch see one checkout | Recorded in the feature doc's merge section and Known gaps; Mike coordinates |
| 4 | MED | The map `--check` never compared a live row's culture or owner with the tables, so both live flips passed it (Tooling T2, Design D2) | Gate blind spot | Culture and owner were written once and never changed before | Both map tools compare culture and owner (D2, Mike approved), with tests |
| 5 | MED | Upgrading the villager into the Plains Levy lowered armour 29 to 27 (`UPGRADE_ARMOUR_REGRESSION`, the one new warning) | Borrowed kit | The levy was dressed from Rohan's lowest kit without the villager's | The levy carries Rohan's L6 militia pauldron |
| 6 | MED | The merge sequence and its retag script lived only in the session scratchpad and a memory card (Completeness 2) | Procedure outside the repo | Repeat of #1053 RCA row 2 | "Merging this branch" in the feature doc; `tools/oneoff/retag_do_srh_live.py`, reading owners from the map tables |
| 7 | MED | No #763 entry, no dedicated-server re-stage and no backup cleanup recorded (Completeness 3) | Owed items | Same shape as #1053 row 7 | #763 comment; the merge section lists the re-stage and the backups |
| 8 | MED | The planned T1 fix (derive from `TARGETS`) would turn the test red: Lindon's and Blue Craig's promoter `--check` refuse today (Completeness 4) | Fix not checked | | `REGENERATABLE` drives the rows, with Lindon and Blue Craig named as refusing |
| 9 | LOW | South Rhovanion was not on Rohan's Culvert list for the Postern Gate (Data flow 1) | Variant list | Dale has no variant, so the Dale precedent never showed it | Added, with a test |
| 10 | LOW | South Rhovanion's culture listed 9 of its 10 wanderers, copied from Rohan's list (#762) (Data flow 2) | Clone of a gap | | The promoter lists every cloned wanderer |
| 11 | LOW | `FORBIDDEN_FOR["vlandia"]` in the CC menu tool was narrower than the C# Rohan list (Tooling T4) | Two word lists | | Widened |
| 12 | LOW | `process_faction_map.py` and `assemble_faction_map.py` still map the two cards to the old placeholder values; a re-run would revert `factions.json` (Tooling T5) | Stale generator table | Every realm since Cardolan has the same shape | Tables updated |
| 13 | LOW | The doc said the cache rebuild was owed; it ran at 14:31:53 (Engine F2) | Doc rot | The rebuild happened mid-review | Doc corrected; old saves on the #1041 path |
| 14 | LOW | The doc said the Rohan ladder bow lives in the rhun folder; its starter twin does, because the donor carries `Culture.khuzait` (Engine F3) | Wrong cause | Written from the tool's output | Corrected |
| 15 | LOW | The doc did not say trunk's fort `--check` reports this change's rows in other checkouts until they merge (Tooling T6) | Doc gap | | Added |
| 16 | LOW | The map-tool realm constants described a one-line flip that cannot exist (Design D3) | Misleading code | Left from the interim step | Inlined as literals |
| 17 | LOW | The promoter kept a Dale default for the XSLT keys that Rohan's keys break (Design D4) | Hidden default | | Keys read from `NORTHMAN_SCAFFOLDS` only |
| 18 | LOW | The doc's career-portrait line said all six were blank; South Rhovanion's use Rohan's, which exist (XML note) | Doc error | | Corrected; #1058 note for Dorwinion's |
| 19 | LOW | Trunk's `0799af376` shipped `tools/translation_name_tags.json` as invalid JSON, failing 6 grammar-tag tests (found before the review) | Bad merge on trunk | Another session's commit | Trunk repaired the file in `1ef7d1af6`; the merge takes trunk's file and regenerates this change's tags on top |
| 20 | LOW, deferred | The union merge dropped one blank separator line in the 12 module-strings files (XML 3) | Byte hygiene | | FOLLOW-UP no issue: no parse or runtime effect; the next generator write normalizes it |
| 21 | LOW, deferred | `test_the_horse_archers_ride_and_shoot_from_the_ladder` asserts riding only; the bow is covered by another test (Tooling T3) | Test name | | FOLLOW-UP no issue: coverage is complete in `test_every_ranged_troop_carries_its_ladder_cell` |
| 22 | LOW, deferred | `town_SRH1` keeps four children named `template_empire_town_*`; that names are cosmetic is UNVERIFIED (Engine F4) | Scene names | Mike's editor work | FOLLOW-UP no issue: an editor rename for Mike, recorded in Known gaps |
| 23 | LOW, deferred | No test checks the new keys of the compiled `RaceAbilityDefaults` twin (Completeness 5) | Twin coverage | Same gap as #1053 | FOLLOW-UP no issue: the shipped-JSON test covers the data; a twin test is cheap but not this change's |
| 24 | MED | Convergence pass: `retag_do_srh_live.py` checked only the live rows, so an `--apply` before the deploy would recreate row 1 | Missing precondition | The fix pass copied the scratch script's guards, which predate row 1 | `--apply` refuses unless the deployed ModuleData defines both cultures and all 8 owner clans; proved against the deployed module (refuses) and a scratch copy |
| 25 | LOW | Convergence pass: a stale comment in `add_map_fortifications.py` still said the rows were retagged live | Comment rot | Left by the D3 inline | Deleted |
| 26 | LOW | Convergence pass: the map tools' docstrings, help and OK lines did not name the new culture, owner and bound comparisons | Doc gap | | Updated |
| 27 | LOW, deferred | Convergence pass: `process_faction_map.py` and `assemble_faction_map.py` still disagree with `factions.json` on 13 and 14 other rows and drop the `{=taom_faction_*_name}` keys, so a re-run still reverts it | Stale generator table | Row 12 fixed only this change's two rows | Both docstrings now say `factions.json` is kept by hand. FOLLOW-UP no issue: the tools have not run since the cards were hand-edited; an issue is Mike's call |

## Root-cause pattern

Rows 1, 3, 6 and 7 are one pattern, the fourth time: a shared, unversioned module (the live map) and
the deployed module must change together, and a session can make them disagree without any gate
noticing. #1053 met it through other checkouts' validators; this change met it through another
checkout's deploy. #1055 proposes gating map rows on trunk; this review adds that the dangerous state
is "live map ahead of the deployed build", which a check against the deployed `ModuleData` could see
(the Design lens ran `validate_moduledata.py --moduledata <game>/Modules/TAOM/ModuleData` and it reports
the mismatch). The cheapest prevention remains the recipe rule: map rows name only cultures every
deploying build defines until the merge sitting.

Rows 2 and 8 are a second pattern: a gate's list kept by hand. The derived list removes it.

## Why each lens missed or found these

- **Engine** found row 1 by comparing the deployed DLL and data with the live map, the only place the
  state shows; the scope file still described the earlier, consistent state.
- **XML** found the same state from the validator and the live file; it misread the cache's report
  file (refuted above).
- **Tooling** found rows 2 and 4 by probing the gates with wrong inputs.
- **Data flow** traced 27 chains; rows 9 and 10 were in code the Dale precedent never exercised.
- **Completeness and Design** (second wave) found rows 3, 6, 7, 8 and the four design proposals.
- **Codex** did not run.

## Final verification

Quoted in the commit body after the merge sitting.
