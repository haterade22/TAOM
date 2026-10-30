# RCA: merging the Shadowfax screen speed and career forks branches into trunk (deep review, 2026-10-30)

## Top-line

Two long-lived branches were merged into `bannerlord-1.5.x`: `feat/ui-speed` (Shadowfax Screens #851 to #854, 373
commits behind trunk) and `feature/career-forks` (#769, about 500 commits behind). Each branch had been reviewed on
its own, so the `/deep-review` covered only the resolution and the collisions with trunk's later code. Tier 1: Codex
at `ultra` (Mike's word, 2026-10-30), two Data flow lenses, Engine compatibility and XML. The engine lens verified 47
items against the installed v1.5.6 DLLs and found none incompatible.

One HIGH, four MEDIUM and about twelve LOW findings were confirmed and fixed in the same session. Every one was the
merge's own work, not either feature's: a step that trunk's tooling already does better, a gate that arrived on trunk
after the branch was cut, or a trunk feature that computes the same quantity on a parallel path.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | The career generator printed `translate_with_claude.py ... --sync-ids --seed-only`, which trunk's translator rejects ("Specify either --dry-run or --apply"), and the branch's `tools/tests/test_translate_seed_only.py` (15 failures) still tested the branch's own `--seed-only` mode, which the merge dropped for trunk's. | Tooling, two implementations of one flag | The merge kept trunk's tool and changed the printed command to add `--sync-ids`, then ran two tool test files instead of the whole tools suite. The printed-text test pins only the text. | Fixed: `--sync-ids --seed-only --apply` (trunk's documented form, `TRANSLATOR_GUIDE.md`), dead test deleted. Lesson: run the full `/verify` (both suites) on a merged tree before the review. |
| 2 | MEDIUM | The 12 `std_taom_career_data_strings_*` language files were rewritten in full (`\r\r\n` became `\r\n\r\n`, about 48k changed lines for a 63-row change). | Line endings (repeat) | The id merge script read with `str.splitlines()`, which splits `\r\r\n` into two breaks. `TRANSLATOR_GUIDE.md` already records the same damage for merge `85cbd20c8`. | Fixed: trunk's bytes plus the branch's 63 rows. Lesson: never port localization rows with a custom line script; regenerate or seed with the repo's own tools. |
| 3 | MEDIUM | The English `taom_career_data_strings.xml` no longer matched its generator (63 inserted blank lines, 12 rows out of the generator's order). | Generated file hand-merged | The file conflicted and was merged by id like the hand-kept files. | Fixed: `generate_name_localization_strings.py --apply --category career_data`. |
| 4 | MEDIUM | 111 old Gondor strings that nothing reads failed trunk's orphan-row gate (`check_orphan_loc_rows.py`, added on trunk after the branch was cut). | Gate newer than the branch | The merge ran the C# suite but not the `.ai/verification.md` gates. | Fixed on Mike's word: retired with their 24 language copies, 12 cache entries each and 18 `career_group_names.json` entries. |
| 5 | MEDIUM | Three Gondor picks give "+50% troop knockdown resistance", but trunk's Hammerhand (#895, on by default) computes bash and kick knockdowns without asking `GetKnockDownResistance`, so the pick did nothing there. | Semantic collision, parallel path | No conflict marks it: the branch hooks the model method, trunk added a second consumer of the same quantity. | Fixed on Mike's word: `BuildShieldBashContext` passes the counter's `1 + m` as the victim resistance factor (`HooksFacade_VictimResistanceFactor_PassesThroughTheCareerKnockdownCounter`). |
| 6 | LOW | On a dedicated server, `ShadowfaxScreensModule.OnPhase` warned "expected exactly 1 RefreshPartiesList call..., swapped 0" and that the quest adapter is absent, every boot. | ADR-013 port, log noise | Marking the hooks `[ClientOnly]` keeps the patches off a server, but the install reports still ran there. | Fixed: early return on `IDedicatedServerProvider.IsDedicatedServer` (`OnPhase_GameInit_OnADedicatedServer_BindsNothingAndWritesNoInstallLine`). |
| 7 | LOW | The quest seam had no null-then-replace test and `NullQuestJournalAdapter` no unit test. | Test gap | The port copied the registration pattern, not its tests. | Fixed: `TaomClientCompositionTests` (15 seams), `NullQuestJournalAdapterTests`. |
| 8 | LOW | The training pin did not hold the Seasoned Hosts factor before the career flat term, which decides the result. | Test gap | The pin was moved, not re-derived for the combined model. | Fixed: `WithBonus(...)` is the pin's first fragment. |
| 9 | LOW | Stale docs: the Patch23 (#993) interplay still described in-place colour writes (three places); `ClanFastOpenCalls` said to carry `[ClientOnly]`; "category number not assigned yet"; the Patch130 removal checklist named the old adapter path; an uncomputed "109 classes"; `seasoned-hosts.md` missing the career term; a dated 2026-10-15 career entry rewritten with a 2026-10-30 fact; the generator doc describing the dropped `--seed-only`; two comment lines dropped from `taom_module_strings.xml`; the prisoner comment missing Promises. | Docs | Docs were resolved by text, not re-checked against the resolved code. | All fixed. |

## Fix pass

The fixes changed runtime behaviour in a tier 1 area (Hammerhand) and fixed a HIGH, so they were reviewed again: Codex at
`high`, the Data flow lens and the Tooling lens. No CRITICAL, HIGH or runtime break. Confirmed and fixed:

| # | Sev | Bug | Fix |
|---|-----|-----|-----|
| 10 | LOW | The career knockdown counter was evaluated while the bash context was built, before Hammerhand's toggle, weapon and immunity gates, so the mission-end `[CareerPerks]` tally counted bashes Hammerhand ignored. | The context carries the career scale as a `Func<float, float>` that `ShieldBashKnockdownService` applies after its gates (`Decide_ToggleOff_NeverAsksTheCareerScale`, `Decide_ImmuneVictim_NeverAsksTheCareerScale`, `Decide_CareerScale_DividesTheChanceOfAnEligibleRoll`). |
| 11 | MEDIUM | Keeping trunk's translator dropped two guards the branch had added after its own review: `--seed-only --module TAOM_Map` exited 0 having done nothing, and an `Armory` or `all` sync with no install wrote the TAOM rows before it failed. | Ported to trunk's `main()` on Mike's word, with the branch's two tests in trunk's form. |
| 12 | LOW | The printed seeding command was pinned only as text. | A test runs the printed line through the translator. |
| 13 | LOW | Stale texts after the Hammerhand fix: the numbered Hammerhand calculation, two consumer registries, the "campaign only" notes (Hammerhand asks the counter in Custom Battle too, where it falls through), the over-count list; the Patch130 removal checklist missed its snapshot rows; `career_group_names.json` still said 294; the marker-region rule needed its named exception; a lesson held raw carriage returns where it meant the text `\r\r\n`. | All fixed. |

The tally finding (10) came from the Hammerhand fix itself; the Tooling lens found 11 by comparing the deleted test with
what trunk's translator still refuses, which the first pass had not asked.

## Root-cause pattern

Findings 1 to 4 share one cause: the merge redid by hand what trunk's own tooling does (seeding, generation, gates),
and checked the result with the C# suite only. Trunk had added a translator mode, an orphan gate and a stricter
generator check after both branches were cut, so a branch-era habit missed all three. Finding 5 is the other class
of long-lived merges: a conflict-free collision, where trunk added a second consumer of a value the branch adjusts.

## Why each lens missed nothing here

The lenses found these; the merge itself produced them. Codex and the career Data flow lens both found finding 1
independently. The XML lens found 2 to 4 by running the gates. The career Data flow lens found 5 by listing every
consumer of the knockdown quantity.

## Feedback memories to codify

None beyond the two lessons in `docs/reviews/lessons/build-tooling-workflow.md`.

`FOLLOW-UP`: no issue: the 114 new career strings are English in all 12 languages; the owed translation goes to #763.
