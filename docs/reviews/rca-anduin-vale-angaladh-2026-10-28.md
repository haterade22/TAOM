# RCA: Anduin Vale and Angáladh deep review, 2026-10-28

## Top line

`/deep-review` at tier 1 (live `TAOM_Map` and Armory data, C# lists, generators) ran one wave of four
lenses: 5 Data flow, 2 Engine compatibility, 7 XML and ModuleData, and Tooling. The XML lens found
one HIGH, confirmed by Data flow and Engine compatibility, so a second wave ran Completeness and
Design on the files involved. Completeness found a second HIGH. A convergence pass checked the fixes.
Codex then ran at `ultra` once Mike gave the word for paid dispatch (2026-10-28), after every fix
above, and found one LOW the lenses missed (row 33). Of 34 confirmed findings, 29 are fixed in this
change and 5 are follow-ups with an issue or a stated reason. No finding was refuted.

The work is the two Neutral kingdoms of Anduin Vale and Angáladh, cut from Dale
([anduin-vale-angaladh.md](../features/anduin-vale-angaladh.md), #1053), uncommitted on
`feat/anduin-vale-angaladh` over `6589bdd11` during the review.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The live `TAOM_Map` named `Culture.anduinvale`, `Culture.angaladh` and seven clans that trunk and the deployed TAOM define nowhere: the base checkout's validator gave 52 errors (`UNKNOWN_CULTURE` 41, `BROKEN_FACTION_REF` 11), so every other checkout's ModuleData commit hook blocked, and a campaign started with another build would load 41 settlements on placeholder objects (lens 7 finding 1, lens 5 G2, lens 2 F1) | Two-module coupling | The recipe writes the live map mid-build from the feature checkout. **Repeat** of the Ered Luin RCA finding 2 (MEDIUM, recorded with no issue), and of Cardolan before it | The 13 live files restored from the pre-change backups after proving the change insert-only; base validator 0 errors again. The rows return in the merge sitting ("Merging this branch" in the feature doc). Recipe step 9 now says to apply map rows at the merge. #1055 for the gate. Lesson in `data-content-cultures.md` |
| 2 | HIGH | The merge sequence that the hold-back made necessary lived only in a private memory card; the feature doc and #1053 still described the rows as live (lens 4 F1) | Procedure not where the reader is | The fix was made under time pressure and recorded where the session keeps resume state | "Merging this branch" in the feature doc, a #1053 comment, and the "both map `--check`s exit 0" precondition in "How to verify in game" |
| 3 | MED | `test_generator_regions_in_sync.py` did not run the kingdom generator's or the promoter's `--check` for the two new realms (Tooling T1, lens 5 G3, lens 7 finding 2) | Gate coverage | The troop agent added its two rows; the kingdom and promoter steps were done by other hands, and no one owned the gate list | The four rows, with an lxml skip for the Dale promoter rows; 18 subtests pass |
| 4 | MED | `translate_with_claude.py --sync-ids` with its default `--module all` wrote 48 live Armory language files without a backup, and two docs printed that exact command (Tooling T2) | Live write by default | The seeding step was run as the docs showed it | The `/localize` skill and `TRANSLATOR_GUIDE.md` spell `--lang <L> --module TAOM`; the 48 files are recorded (insert-only, earlier Armory ids). The tool change is #1056 |
| 5 | MED | The Angáladh faction-map card keeps the placeholder's region east of Erebor, while its fiefs are in western Mirkwood (lens 5 G1) | Placeholder geometry inherited | The card was bound by id; its region was never compared with the fiefs | Mike chose a follow-up: #1057. The card stays parked |
| 6 | MED | `release-process.md` had no channel paragraph for these realms, though the feature doc pointed there (lens 4 F3) | Per-realm hand list | The paragraph is written by hand per realm | Paragraph added. The generic gate is part of #1055's problem class |
| 7 | MED | No #763 entry for this change's English keys and the 41 map rows (lens 4 F4) | Backlog entry missed | The seeding step was run, the backlog comment was not written | #763 comment |
| 8 | MED | The recipe still writes live rows mid-build, so the class recurs for every realm and for every worktree older than a realm's merge (lens 4 F5, lens 6 proposal 1) | Recipe order | Prose warnings existed (Ered Luin Known gaps, release-process.md); no gate | Recipe line; #1055 (stage rows until trunk defines their cultures) |
| 9 | LOW | The first restore note said "copy the built backups back", which would undo any live-map edit made after 18:18 (lens 4 F2) | Unguarded restore | Written for this session's state only | The merge sequence re-runs the tools and only compares with the backups; a hash guard first |
| 10 | LOW | The doc said the new Armory starter file loads only after a game restart; its folder is registered, so it loads at the next campaign start (lens 2 F2) | Wrong engine claim | Copied from the new-file rule without checking the folder registration | Corrected |
| 11 | LOW | `dale/starter_kit.xml` was called new with fresh backups; it existed since 2026-09-12 and its sidecars date from 2026-10-23 (lens 5 I3, lens 7, Tooling) | Claim not checked | Reported by the builder from the tool's output | Corrected, with the undo path |
| 12 | LOW | Angáladh flew 17006 and 17007, which Mirkwood's Nos Galengrim flies, and the docs said no clan flew them (lens 7 finding 3). The orchestrator's first replacement, 17008, also collided: its banner-key parser stepped in fixed ten-field layers and missed Nos Galengrim's | Identity reuse; a parser trusted as proof | The icons were checked with a layer parser that does not match the key format | Mike chose icons flown by no other clan or kingdom: 17000, 17001 and 20001, each counted by a plain token search over every banner key. 17000 and 17001 are also the `keystone_icon` of Mirkwood's Silvan Archer and Elk Rider careers (`career_system/taom_careers.xml` lines 824 and 845), a career screen, not a banner |
| 13 | LOW | The six careers name Dale's portrait sprites, which are not registered: a blank portrait, as for 42 of 85 careers (lens 7 finding 4, lens 5 I2) | Art binding by id | Pre-existing | #1058 |
| 14 | LOW | The Ironwood Youth's upgrade into the Woodman Bowman line was undocumented (lens 7 finding 5) | Doc gap | The fact sheet listed one branch | Documented |
| 15 | LOW | Three wanderer surnames kept Dale's: Bainling (Bain's line), Ravenhild, Fellrider (lens 7 finding 6) | Clone leak | The promoter's `subs` covered the surnames the builder noticed | Bainling and Ravenhild remapped per realm; Fellrider kept (no Dale person or place); the 4 language rows reset |
| 16 | LOW | The 12 translation caches lost their final newline, and two `voice_accents.json` lines were LF in a CRLF file (lens 7 finding 7, Tooling T9) | Byte fidelity | Hand edits by a builder agent | Fixed |
| 17 | LOW | `test_promote_xslt_culture.py` failed rather than skipped without lxml, and its refusal test passed for the wrong reason (Tooling T3) | Test asserts the wrong cause | The test checked only that an exit happened | `skipUnless(lxml)`; the refusal test asserts "found 0" |
| 18 | LOW | `test_promote_block_src.py` called `unittest.main()` above its last class (Tooling T4) | Script-mode skip | A class appended after the guard | Guard moved to the end |
| 19 | LOW | The new `great_axe` alternative in the two-hander self-check had no test (Tooling T5) | Untested gate branch | Added with the troop step | A probe test, shown to fail without `great_axe` |
| 20 | LOW | `add_lord_templates` hard-coded Dale's face for a generic key (Tooling T8) | Hidden default | Written for the one source in hand | `lord_face` key |
| 21 | LOW | The docs did not say that the Dale culture blocks follow the installed game (Tooling T10) | Doc gap | | Sentence in the generator comment, the README row and the feature doc |
| 22 | LOW | `kingdoms.md` and `recipe-add-a-kingdom.md` still counted six Neutral realms and 32 `alignment.json` keys (lens 5 I1) | Counts in prose | Not in the change's doc list | Corrected |
| 23 | LOW | The card test hard-coded the two feat values (lens 4) | Test restates data | Written from the card text | Reads them through `ProductionEffectBonus`, as its sibling does |
| 24 | LOW | `DALE_BOW_LEVELS` restated the ladder spec (lens 4) | Test restates data | | Reads `rl.load_spec()` |
| 25 | LOW | The dedicated-server re-stage owed by the scene edit was recorded nowhere (lens 4) | Downstream step | | In the merge sequence |
| 26 | LOW | Design: `src_culture` duplicated `id_overrides` (lens 6 proposal 3) | Two mechanisms | Added as a new key before the existing one was considered | Folded into `id_overrides`; all six promoter targets byte-identical |
| 27 | LOW | Design: land the rows in one merge sitting, compare with the built copies (lens 6 proposal 2) | Procedure | | Applied (finding 2) |
| 28 | LOW, deferred | Gondor-sourced blocks are scanned for Dale's words, not Gondor's (Tooling T6) | Speculative guard | | FOLLOW-UP no issue: those blocks hold only `{=!}stage_2_page_0_branch_N` names today, and `simplicity-criterion.md` asks Mike before a guard for a case that cannot occur yet |
| 29 | LOW, pre-existing | The `Culture.%s_raiders` protection in `transform` never matches (Tooling T7) | Latent regex | | FOLLOW-UP no issue: no ModuleData file has such a token, and every promoter `--check` is unaffected |
| 30 | LOW, pre-existing | The cloned companion rosters carry `dale_great_axe_a` beside a shield, inherited from Dale's own roster (Tooling cross-lens note) | Clone of a gap | | Known gap; `audit_polearm_shield_parity.py` passes |
| 31 | LOW | Angáladh's banner sigil was 2004 white while its `color2` is #F2F4F7 (palette 2022), so the data said white and the game shows off-white (convergence pass) | Data and engine disagree | The sigil colour was picked from the palette name, not from the colour the engine paints | Fixed to 2022 |
| 32 | LOW | The branch validator fails while the rows are held back, and no merge doc said so (convergence pass) | Missing procedure note | The merge sequence assumed the rows were applied | Fixed in the feature doc: 53 `LANDLESS_CULTURE` errors until merge step 1, commit only after the step 3 gates |
| 33 | LOW | `ClonedCultureFlavorTests` did not list `anduinvale` or `angaladh`; checking it showed that for every culture it skipped text over 200 characters (every culture description) and never read `taom_culture_text_strings.xml` or `taom_wanderer_strings.xml` (Codex) | Hand-kept allowlist; capped regex | The recipe does not name the test, and the cap hid the descriptions from every earlier run | Both cultures added with Dale's identity words, chosen from the lore and not from the promoter's table; the cap removed and the two string files scanned; a Dale word injected into Angáladh's description and into a wanderer backstory now fails the test, and the old test passed both |
| 34 | LOW | The 12 new Dale starter twins live only in the unversioned Armory, and the merge proof did not check them (lens 4, late note) | Live dependency unchecked at merge | | `python tools/generate_starter_kit.py --verify` added to the merge gates |

## Root-cause pattern

Findings 1, 2, 8 and 9 are one pattern: the kingdom recipe writes a shared, unversioned module from a
feature checkout, and the repo has no gate that sees the two modules disagree from the other side.
The Ered Luin RCA recorded the same coupling a day earlier as a MEDIUM with "no issue", and it came
back as a HIGH. A prose warning in a feature doc's Known gaps does not reach the next builder, who
starts from the recipe. The preventive action is therefore in the recipe (step 9), in an issue for the
gate (#1055) and in a lesson.

Findings 12 and 13 share a smaller pattern: a reused id (an icon, a portrait) was checked for
existence, not for who else uses it.

## Why each lens or step missed these

- **The build:** the map step ran first in parallel with the troop and culture steps, as the recipe
  orders it, so the live map named the cultures for several hours before any review.
- **Lens 7 (XML):** found finding 1 by running the validator in the base checkout, the only place the
  failure shows. The branch's own gates were green.
- **Lens 5 (Data flow) and lens 2 (Engine):** confirmed finding 1 from the deployed module; lens 2
  rated it MEDIUM because old saves are unaffected.
- **Codex:** not run.
- **Completeness and Design:** ran only in the second wave, which is where findings 2, 8 and the
  design proposals came from.

## Feedback memories to codify

None new. The lesson below carries the rule; the orchestrator rule "act only on findings that change
runtime behaviour while lenses run" was applied (the rollback ran after the last first-wave lens
reported).

## Final verification

`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: 28,129 passed, 0 failed, 7 skipped.
`python -m pytest tools/tests`: 6,225 passed, 11 skipped. Both ran after the first fix pass; the
commit-time run is quoted in the commit body.
