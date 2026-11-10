# RCA: Hero Defence Slices 1 and 2 review, 2026-11-09

**Summary:** the review covered the pure core of Hero Defence (#1106): the run rules, the wave generator, the three config
readers, the shipped JSON and `tools/generate_hero_defence_rosters.py`. Nothing in it is registered in IoC or `SubModule.xml`. The
review was tier 2 (gates, tests and a generator; no patch, model, save or engine call). Lens 5 (data flow) and the tooling lens ran
at `effort: "high"`, and Codex ran at `xhigh` on Mike's word for the night. No reviewer reported a CRITICAL or HIGH finding, so no
second wave ran. Lenses 1, 3, 4 and 6 were skipped by tier, and lens 2 and lens 7 were not in scope (no engine call, no XML).

Eight MEDIUM and a run of LOW findings were confirmed by reading the code. All but one are fixed here, each with a test that
failed first except where the table says otherwise. The unknown-id finding (R6) needs the loaded game's character list, so it is
owed by Slices 3 and 9. Nothing here runs in the game yet, so no finding could crash it or touch a save.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | MED | The infantry gap bonus was a wave-wide factor: in-band cavalry got it too (Codex: seed 2, wave 21, Gundabad, +0.15 health), and it was paid when infantry was in the band below its high level. Lens 5 Q1, Codex F1. | rule shape | The plan sentence says "for infantry", but a wave brief had only wave-wide factors, so I put the bonus there. No test mixed classes. | `CreepPack.HealthBonus` and `DamageBonus`, set only on infantry packs that fell below the band. `Brief_InfantryTopsOutBelowTheBand_OnlyItsPacksCarryAGapBonus`, `Brief_InfantryInTheBandBelowItsHigh_CarriesNoGapBonus`, RED first. |
| R2 | MED | A pinned opening culture skipped the entry rule. A pinned culture with no infantry at or below the band gave a wave with no creep, which cleared at once and paid the bonus. Lens 5 Q2, Codex F2. | sibling path | The drawn path checked `HasEntered`; the pinned path was written later and did not. Tests covered only eligible pins. | The plan uses a pin only when it has entered, and the catalog drops such an id with a warning. `WaveCatalog.BandOf` and `HasEntered` are shared by both. Two tests, RED first. |
| R3 | MED | Small accepted counts broke the count: a total that rounds to 0 gave an empty wave, and a leader wave with no infantry pack added a creep past the cap. Lens 5 Q3, Codex F3. | arithmetic invariant | The invariant "a wave has at least one creep, and a leader replaces a soldier" was in a comment, not in a test. | `TotalOf` computes in double and returns at least 1; with no infantry pack the leader takes one creep from the largest pack. A count row and a total assert, RED first. |
| R4 | MED | Famous villains led as captain or lieutenant (Isengard: `lord_I1_1`, `lord_I4_1`; Mordor: `lord_1_48_1`, `lord_1_15`; Goblin: `lord_GB1_2`). The plan allows them only at general rank. Codex F4. | data curation | I curated the leaders by hand and checked only "no buyable hero". No test encoded the general-rank rule, and I did not read each id's character. | New captains and lieutenants for the three cultures. `AFamousVillain_LeadsOnlyAsAGeneral`, RED first. |
| R5 | MED | The tick that ended the pause charged its whole `Dt` to the wave too, so `Tick(90)` then `Tick(96)` expired a 186 s wave limit. Codex F5. | state transition | Each phase method was tested alone; no test crossed the boundary inside one tick. | `TickPause` returns the rest of `Dt`, and only that reaches the wave. Two tests, the first RED. |
| R6 | MED | Unknown troop, leader and power ids pass the readers. Codex F6. | reference check | The readers have no game data, by design. | Not fixed here: FOLLOW-UP below, #1106. |
| R7 | MED | The gold purse could overflow `int` with accepted settings (Codex: `waveBonusPerWave` 100000 at wave 207). Codex F7. | arithmetic | The defaults stay small, and no test used the top of an accepted range. | `AddGold` saturates, and the clear bonus is computed in double. Two tests, RED first. |
| R8 | LOW | A repeated JSON key overwrote the first silently, and a repeated roster id gave two rows. Codex F8, lens 5 Q5. | reader | `JToken.Parse` keeps the last value by default. | `OpenRoot` sets `DuplicatePropertyNameHandling.Error`, so the file reverts with a warning; a repeated id is skipped with a warning. Two tests, RED first. |
| R9 | LOW | A missing or bad `opening` gave no opening, while the warning said "using the shipped list". Codex F9, lens 5 Q5. | default | The compiled default list was empty. | The default is mordor, erebor in `DefaultCatalog` and in the fallback. Catalog and shipped-data tests, RED first. |
| R10 | LOW | A cultures file with no valid culture loaded with no warning that the mode cannot start. Lens 5 Q5. | reader | Each culture warns alone; the empty total did not. | One warning when no culture survives. Test, RED first. |
| R11 | LOW | The director contract was only partly written, and a repeated `CreepSpawned` key counted its creep twice. Lens 5 Q4, Codex S4. | contract | The test driver kept the contract, so no test broke it. | "Director contract" in the feature doc; `CreepSpawned` ignores a key that is alive. Test, RED first. |
| R12 | LOW | At the wave limit, unreleased creeps dropped for free entered the leak share, so a wave whose only released creep leaked still paid 89% of the bonus. Lens 5 Q6. | accounting | The share counted every creep of the brief. | Unreleased creeps count only when they leak. Test, RED first. Whether a timed-out wave pays a bonus at all is Mike's decision. |
| R13 | LOW | The rotation preferred another signature only inside the bag, so the same signature could follow itself while another had entered (Codex: seed 0, waves 24 and 25, Goblin then Rhudaur, both horde). Codex W2, S5. | rule scope | The rule was written as "prefer", and the test catalog had two cultures per signature. | When the bag holds only the last signature and another has entered, the bag refills early. A 40-seed test, RED first. |
| R14 | LOW | Test gaps: float and range tests did not compare the catalog values, several `All()` asserts could pass on an empty list, the pack-size test allowed 21, the order test compared culture ids only, NaN rows lacked `countFactor` and rank health, no zero-byte file test, and the 30-wave scenario allowed health to fall. Codex section 5. | test strength | Each test asserted its warning or its main value, and no one asked whether it fails with the rule deleted. | Each gap closed in this change. |
| R15 | MED | `rosters.json` had no data gate, so a troop edit could drift it with no warning before the full suite. Tooling T1. | gate | The C# drift test runs only in the full suite. | The `hero-defence-rosters` row in `tools/data_gates.py`. |
| R16 | LOW | Generator exit codes: an empty folder wrote `{}` with exit 0, a crash and a drift both gave exit 1, a BOM counted as drift, the docstring named a test class that does not exist, and some tests were missing or claimed more than they checked. Tooling T2 to T8. | tooling | Only the happy path and the drift path had tests. | Exit 3 and nothing written for a bad or empty tree, `utf-8-sig`, docstring exit codes, the militia comment, nine more tests. Written after the fix, see the process section. |
| R17 | LOW | Plan deviations, now in the feature doc: health and damage growth stop at `maxFactor` 20, the gap bonus has no cap, and Shaghana merges into Harad. Codex W1, W7, E2 (bounty rounding order). | plan | The plan did not state these. | Doc lines; Mike decides each (below). |
| R18 | LOW | A NaN map entry or exit would stop every leak and every road-end check. Lens 5, Codex section 2. | contract | The map is not read yet. | A doc line for the Slice 3 tag reader. |

## Root-cause pattern

R2, R3, R5, R9 and R13 share one cause: a rule was enforced on the main path and not on its sibling path. The drawn culture
checked entry and the pinned one did not; a leader with an infantry pack replaced a soldier and a leader without one did not; a
normal tick charged time once and the tick that ended the pause charged it twice; the file's opening was right and the default
opening was empty; the rotation varied the signature inside the bag and not across the catalog. The tests followed the same main
paths, so they passed. R4 is the data form of the same gap: the plan's rule ("famous villains only at general rank") existed only
as prose, so nothing checked the second and first leaders against it.

## Why each reviewer caught or missed these

- **Lens 5 (data flow):** caught R1, R2, R3, R10, R11, R12 and R18 by tracing each brief value to its reader. It did not run the
  arithmetic at the top of the accepted ranges (R7), did not cross the pause boundary (R5), and did not read the leaders' characters
  (R4).
- **Codex (`xhigh`):** caught R1 to R9 and R13 with seeded transcriptions of the rotation and with the lords' identities from
  `lords.xml`. It did not report R10 or R12. It also confirmed that the shipped opening is safe and that all 969 roster ids and 75
  leader ids exist.
- **Tooling lens:** caught R15 and R16 and found the generator's filters sound against the XML.

## What went wrong in the process

1. **Main-path tests only.** Every rule had a test, but each test drove the path the rule was written for. A sibling path written
   later (pins, the leader fallback, the default opening) got no test of its own.
2. **A plan rule kept as prose.** The leaders were curated by hand against a rule that no test encoded.
3. **Code before tests in the tooling fix.** In this fix pass I edited the generator before I wrote its new tests (R16). The tests
   then passed; they were not seen failing first. The C# fixes kept the RED step.

What worked: a tier 2 review with Codex in parallel found every defect before the first commit, and each claim was checked against
the code before a fix went in.

## Verification

The full `/verify` ran in the worktree after the fixes (2026-11-09, 20:50 to 21:01). Build: exit 0, 0 errors; the 10 warnings are
Harmony analyzer warnings in other features. Restore with `--force-evaluate`: exit 0, no NU1900, NU1903 or NU1904. C# suite:
38,221 tests, 38,212 passed, 0 failed, 9 skipped; the Hero Defence filter alone ran 360 of 360 after the fixes, against 27 failed
before them. Tool tests: 8,407 ran, 12 skipped, 13 failed in `test_feedback_memory_hook`, `test_githooks_pre_commit` and
`test_statusline`. Those three modules start bash hooks, and the PowerShell host of the verify script found WSL's `bash.exe`
(`execvpe(/bin/bash) failed`); run again from Git Bash, the same 52 tests pass. The canary tripped, the API snapshot reproduces,
the data gates report `hero-defence-rosters` and `generator-refs` PASS with none failed or not run, and provenance has 0 misses.
`lint_docs.py --fail-on-drift` first failed on this RCA's four follow-up lines, which named #1106 only on a wrapped line; with the
number on each follow-up line it exits 0. Nothing ran in the game: no part of this change is registered.

## Follow-ups

- FOLLOW-UP (#1106): unknown ids (R6). The Slice 3 launch must resolve every roster troop and every leader against the loaded characters,
  and refuse a catalog with no culture (`CreepWavePlan.Pick` indexes `Cultures[0]`). Slice 9 closes `powers.pool` against its
  numbers table.
- FOLLOW-UP (#1106): decisions for Mike. Does a wave that reaches its time limit count as cleared, and does it pay a bonus? Is a mounted
  bounty rounded before or after the +50%? Keep the growth cap at factor 20? Keep Shaghana inside Harad? Approve the new captains
  and lieutenants of Isengard, Mordor and Goblin?
- FOLLOW-UP (#1106): a fallen hero's return at full health is engine behaviour (Codex test gap 9); the Slice 5 `respawn` check
  proves it.

## Lessons recorded

`docs/reviews/lessons/testing-qa.md`: "Every rule gets a test on each path that reaches it" (2026-11-09).
`docs/reviews/lessons/data-content-cultures.md`: "A plan rule about named characters needs a data test" (2026-11-09).
