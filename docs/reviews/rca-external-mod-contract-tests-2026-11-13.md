# RCA: the external mod contract tests (#1186), 2026-11-13

## Top line

`/deep-review` at risk tier 2 (two new test files, no production code) ran lens 5 Data flow at `high` effort, and
Mike chose to run the tier's Codex pass before the push (`gpt-6-astra` at `xhigh`, dispatched from the worktree;
prompt [codex-prereview-external-mod-contract-tests-2026-11-13.prompt.md](codex-prereview-external-mod-contract-tests-2026-11-13.prompt.md),
output under `raw/`). Neither found a defect in the four pins: each test fails when its value changes under the
documented test commands. Lens 5 confirmed one MEDIUM finding, a false reason in a test comment and message, and
three LOW ones. Codex reported one P2, in the doc the test messages point to, and no P1. No CRITICAL or HIGH, so
no second wave. Lenses 1, 2, 3, 4 and 6 were skipped by tier.

The MEDIUM finding had spread before the review caught it: the same false sentence was in the test, in issue #1186
and in `external-mod-couplings.md`. Checking it led the main session to a difference nobody had asked about, the
swapped property ids of `HoNPresetItemReference` (#1194).

Blast radius: `python tools/graphify_taom.py affected` found no consumer of the three test classes as first written
(the graph was refreshed for the worktree); the later rename adds none. Live sweep: `TAOM_Map/ModuleData/settlements.xml`
and `LOTRLOME_Armory/ModuleData/skins.xml` changed during the work, each with another session's own backup file;
NOT IN SCOPE, this change wrote nothing to the game folder.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The base id test's summary and failure message said that another base id "breaks the import of the original mod's saves". No import exists: TAOM reads the key `"TAOM_FormationPresets"` (`FormationPresetCampaignBehavior.cs:59`), the original mod wrote `"FormationPresets"` (lens 5; Codex suspect 4 agrees) | effect stated without its producing line | The sentence came from TAOM's own definer comment (`FormationPresetSaveableTypeDefiner.cs:9-10`) and from a reader's report. The builder checked the number and the original mod's guard, the two things the sentence names, and never opened the reader of the data | Fixed: the comment and message now say what the engine lines show (base id plus class id; no definition, no object). One line added to `.claude/rules/tests.md`; lesson appended |
| 2 | P2 | `external-mod-couplings.md`, which three failure messages name, still said no test pins the module id, the base id or the enum values, and repeated finding 1 (Codex F1; doc review F3-1) | doc contradicts the gate it documents | The doc rows were written by the knowledge batch before the tests existed, and the tests were added without re-reading the rows they point to | Fixed in the same commit as the docs: the rows name the tests and what they do not cover |
| 3 | LOW | "A reordered or inserted member compiles, passes every other test": no run proved the second half (lens 5) | overclaim | Written from expectation | Fixed: "can pass the other tests" |
| 4 | LOW | "With another id both copies run": for the two preset mods a duplicate save registration can stop the start instead (Codex suspect 4; v1.5.7 `DefinitionContext.cs:94-99` adds the id with a dictionary `Add`) | imprecise consequence | The guard's purpose was restated without the definers' own comment | Fixed: "both copies can activate, and two of those mods then register the same save ids" |
| 5 | LOW | Two test classes sat in one file named after neither, and the module id test sat in a CompanionTactics file (lens 5) | placement | The file was named for the issue, not for its classes | Fixed: `FormationPresetSaveableTypeDefinerTests.cs` beside its EquipPresets twin's pattern, and one `ExternalModContractTests` class in `TAOM.Tests/Infrastructure/` |
| 6 | LOW | The class ids, the two SyncData keys, the two behaviour class names, the saved field ids and the names other mods find by reflection stay unpinned, and a change to any of them is as silent as the four pinned values (lens 5 gaps; Codex suspect 6: "not a defect in this bounded change", provided #1186 records the scope) | coverage boundary | #1186 was scoped from the couplings the review had found, before anyone traced what a save identity consists of | Not applied here. `FOLLOW-UP` #1186: a small compatibility baseline (type ids, behaviour `StringId`s, keys, field id meanings, reflection names) |
| 7 | (found on the way) | `HoNPresetItemReference` saves `SlotIndex` as property 1 and `ItemStringId` as property 2 in TAOM, and the other way round in the original mod, under the same save id | pre-existing data contract | The port of 2026-05-07 wrote its own property order; the comment that claims import compatibility is on the formation preset, and nothing compares the two layouts | `FOLLOW-UP` #1194 (decision owed: keep TAOM's layout and state that the import is not supported) |

## Root-cause pattern

Findings 1, 2 and 7 are one pattern. A sentence about compatibility ("matches the original mod, so saves import") sat
in a production comment. It names two true facts (the same base id, the same class id) and draws a conclusion that
needs four more (the behaviour's `StringId`, the SyncData key, the field ids, the field types). Every later text
(the port's own test comments, a reader's report, an issue, a doc row, a new test message) repeated the conclusion
and checked only the two named facts. The first person to open the reader of the data found the conclusion false for
formation presets, and the next comparison found the layout difference for EquipPresets.

This class has shipped before. `lessons/testing-qa.md` holds "A comment a plan supplies beside a new test is a
draft" (plan 049, the same day), and `lessons/adapters-taleworlds-api.md` holds three entries about engine claims in
comments. Those are about engine facts. This one is about TAOM's own data flow, where no decompile is needed: both
ends are in reach and neither was read. So the preventive action is a rule line that loads with every test file, not
another lesson alone.

## Why each reviewer missed or found these

- **Lens 5 Data flow** found 1, 3, 5 and 6 by tracing each pinned value from its source to every reader, TAOM's
  behaviour included. It could not find 7: it compared the two behaviours' keys and stopped at "the import claim holds"
  for EquipPresets, one hop short of the saved classes.
- **Codex** found 2 because suspect 5 sent it to the doc, and sharpened 4 from the engine's definition code. It
  traced what a changed base id does on load (the preset list loads as null and TAOM substitutes an empty list; a
  source trace on v1.5.7, not a run). It had no access to the original mod's source, so 7 was out of its reach.
- **The main session** found 7 while checking lens 5's "holds" before writing it into the couplings doc.
- **The doc review** (four finders, one Opus verifier, in the record) found 2 from the doc side.

## Improvements (Step 4)

Lenses 3 and 6 did not run (tier 2), so there is no design proposal to apply.

APPLIED:
- `FormationPresetSaveableTypeDefinerTests.cs`: corrected summary and message (finding 1); own file (finding 5).
- `ExternalModContractTests.cs`: one class for the three game-free pins (finding 5); wording (findings 3 and 4).
- Proof: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~ExternalModContractTests|FullyQualifiedName~FormationPresetSaveableTypeDefinerTests"`, Failed 0, Passed 4, after the last edit.

NOT APPLIED:
- A run-time read of the enum values (`GetRawConstantValue`) in place of the folded `(int)` casts. Codex calls it
  optional hardening and lens 5 calls it not a finding: every documented test command rebuilds the tests, and
  `.ai/verification.md` already forbids `--no-build` after a partial build.
- A source-text twin of the base id pin for runs without the game: weaker than the reflection read, and it fails on
  a harmless refactor to a constant (lens 5).

Deferred, each with its issue:
- FOLLOW-UP #1186: the compatibility baseline of finding 6, and three stale comments that claim the formation
  preset import (`FormationPresetSaveableTypeDefiner.cs:9-10`, `HoNFormationPreset.cs:10`,
  `SaveDefinerCollisionDetectorTests.cs:15-17`); they are production and test code outside this change.
- FOLLOW-UP #1194: the EquipPresets property id difference.

## Lessons codified

- `.claude/rules/tests.md`: one line, so the rule loads with every test file.
- `docs/reviews/lessons/state-lifecycle-save.md`: "A save compatibility claim names every part of the identity, and
  is proven at the reader".
