# RCA: nullable ratchet, plan 019 (deep review + Codex adversarial, 2026-09-24)

## Top-line

Plan 019 moved the seven nullable warning ids out of both production `<NoWarn>` lists into the
root `.editorconfig` and graduated `Main/Features/Siege` to `error` (branch
`improve/019-nullable-ratchet`, `7f02fc8d..19ca72d3`). Six deep-review lenses (standards, engine,
efficiency, completeness, data flow, design) and Codex (gpt-6-astra, ultra) found **no runtime
defect**: the build output is unchanged (Main `2 Warning(s)`, `0 Error(s)`, 0 CS86xx; the
test project's 2,256 CS86xx unchanged) and every engine member the change touches was verified
against the installed v1.5.3 DLLs.

**12 confirmed findings, 1 false positive, 0 HIGH.** Two MEDIUM: `/build-fix` still recommended
`!` for the errors the change turns on (fixed), and no GitHub issue exists (needs Mike). The rest
are LOW or NIT: comments and docs that described the no-settlement defer by its control flow, a
DTO comment promising a fallback one field does not get, a CHANGELOG pointer to a procedure that
lived only in the plan, stale and misattributed doc lines, a test oracle that checked lengths, and
tests for two of the prefix's five paths. Eleven are fixed in the follow-up commit; the issue is
left to Mike.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | `.claude/skills/build-fix/SKILL.md:91` said "CS8602: Add null check or `!` operator", and CLAUDE.md routes every `error CS####` there. The row was dead for `Main` before (ids in `<NoWarn>`); the ratchet made it the first advice for a Siege nullable error, and `!` silences the error the ratchet exists to raise | Harness advice contradicts a newly enforced rule | The plan's scope was the compiler config and the docs describing it; nobody grepped the ids across skills | Fixed: one row for all seven ids pointing at code-quality.md, plus a DON'T line for `<NoWarn>` and lowering a folder's `.editorconfig`. Lesson in `lessons/build-tooling-workflow.md` |
| 2 | LOW | The patch comment and `siege.md` item 3 called the no-settlement defer "the same outcome as before ... now without throwing"; vanilla then indexes the empty array and throws (`BesiegerCamp.cs:312-315`), and neither said the path is unreachable from vanilla (both callers dereference `SiegeEvent.BesiegedSettlement` first, `MobileParty.cs:3248`, `BesiegerCamp.cs:566`) | Control flow described as an outcome (**repeat**) | The plan supplied the wording, and `lessons/harmony-il.md:373` named Patch8 as a safe defer, so it read as safe | Fixed: comment, `siege.md` items 3 and 4 and the diagram; the Patch8 registry entry (was "Target: Various") now records both throwing defers, as the lesson's Prevent line requires. The lesson's wrong Patch8 example is corrected and a **Recurred** line added |
| 3 | LOW | `KingdomSiegeMessages.cs:5` "consumers fall back to \"\"" is false for `AcceptButton`, which reaches `InquiryData` unchanged (`SiegeDefenseService.cs:148`) | Comment overstates behaviour | Plan-supplied text (plan `:723-724` contradicts its own `:742-744`) | Fixed: the DTO comment now describes only the data |
| 4 | LOW | The CHANGELOG promised the next-folder procedure in `code-quality.md`; the steps, fix rules and hotfix escape lived only in `plans/019-nullable-ratchet.md`, and both `.editorconfig` comments cited the plan | Pointer to a backlog, not the knowledge base | The plan's docs step added only the mechanism | Fixed: procedure, fix rules and hotfix escape moved into `code-quality.md`; the hand-kept graduated list became a `git grep`; both comments point there. Lesson in `lessons/misc.md` |
| 5 | LOW | `siege.md:21,35` said `__result` is `settlement.GatePosition`; the code places each party on a ring around it (patch `:61-68`) | Stale doc carried into rewritten lines | Plan Step 8 told the executor to keep the wording (plan `:812-818`); stale since `0d15c44b` | Fixed. Covered by the plan-text lesson |
| 6 | LOW | `siege.md:56` said `SiegeEngineAvailabilityServiceTests` covers "the siege-defense service"; it tests `SiegeEngineAvailabilityService` | Misattributed doc line | Written from the file names | Fixed: each test class named with its service |
| 7 | LOW | The camp-2 test asserted only lengths; a copy that dropped the frames' transforms would pass (Codex P3) | Weak oracle | Plan oracle `:648-657` prescribed lengths | Fixed: `Assert.AreSame(originalCamp2, camp1)`. Lesson in `lessons/testing-qa.md` |
| 8 | LOW | "Never add these ids back to `<NoWarn>`" was prose only; one id back switches off every graduated folder with a green build, and `bannerlord-1.4.5` still carries the old lists | A machine-checkable rule without a gate | The plan deferred only the downgrade gate; CLAUDE.md tier 1 ("a machine can check it: write the gate") was not applied | Fixed: `NullableRatchetGateTests` (2 rows), shown failing on a temporary `CS8602` in the Dependencies `<NoWarn>` |
| 9 | LOW | Tests covered 2 of the prefix's 5 paths; the settlement path (the patch's purpose, and the proceeds side of the new guard) was called "structurally untestable" | Coverage gap from an untried claim | Plan `:899-900` and the old feature doc | Fixed: 3 more tests (camp-1 present, settlement ring, catch), the ring built on uninitialized engine objects. Lesson in `lessons/testing-qa.md` |
| 10 | NIT | `<NoWarn>$(NoWarn)</NoWarn>` in `Main/TAOM.csproj` assigned the property to itself | No-op code | Plan `:513` kept it "so other projects' NoWarn composition is untouched", which MSBuild does not do | Fixed: deleted; `dotnet msbuild -getProperty:NoWarn` prints `1701;1702` before and after |
| 11 | NIT | `SiegeCampGuardPatchTests` had no Arrange/Act/Assert comments (`tests.md:23-37`) | Convention | Style | Fixed while rewriting the class |
| 12 | MED | No GitHub issue for the change; `plans/README.md:5` requires one before a plan lands on a trunk branch | Process | The improve sprint executes plans without filing issues; filing is public and needs Mike | **Not fixed: needs Mike.** File it, then add `(#N)` to the CHANGELOG heading and `siege.md` "GitHub Issue", and `Refs #N` on the merge |

False positive: Agent 4 F7's naming point (file named after the patch category rather than the
patched method). Both styles are in use: `Patch62MovieReleaseAvGuardTests.cs`,
`RecruitGatePatchTests.cs` and `Clan_UpdateBannerColorsAccordingToKingdom_PatchTests.cs` all exist.

## Root-cause pattern: the plan's own text

Findings 2, 3, 5, 7, 9 and 10 were written into plan 019 and executed faithfully: supplied comment
text, a doc wording the plan said to keep, a test oracle, an untestability claim and a
justification for keeping a no-op line. The executor treated the plan as the specification, and the
plan's author wrote those lines without re-reading the code, engine or MSBuild behaviour they
describe. Plan review looked at the steps, not the prose. Lesson (`lessons/build-tooling-workflow.md`):
text an `/improve` plan supplies is a draft the executor verifies, and the plan's done criteria
say so.

Finding 2 is a repeat. `lessons/harmony-il.md` already said "falls through to vanilla describes
control flow, not an outcome", but the same lesson named Patch8 as the example of a safe defer, so
anyone reading it before editing Patch8 (as `harmony-patches.md` requires) was told the opposite
for this patch. A lesson's example is as load-bearing as its rule: the example is now corrected.

## Why each agent missed these

- **Implementation (executor):** followed the plan's text; see the pattern above. It did run the
  RED/GREEN cycle and counted every build, which is why no runtime defect exists.
- **Agent 1, standards:** found 3 to 6, 8, 10 and 12. It missed 1 because its checks cover the
  changed hunks, and the skill row is outside the diff; it checked test isolation and naming, not
  oracle strength (7, 9).
- **Agent 2, engine:** found 2 and 6. The rest are outside an engine lens.
- **Agent 3, efficiency:** found 10 and routed 4 and the unreachability of 2 as cross-lens notes.
- **Agent 4, completeness:** found 1 to 6, 9, 11 and 12. It listed the untested paths but accepted
  the camp-2 assertion (7), and filed the missing gate (8) as follow-up rather than a finding.
- **Agent 5, data flow:** found 1 to 4, 6 and 10. It traced the test's `DebugManager` swap but not
  what the camp-2 assertion proves (7), and did not ask about path coverage (9).
- **Agent 6, design:** found 2 to 4 and 10, plus the `StringAssert` improvement. It judged the
  harness out of its design scope (1) and the test harness "already optimal" without checking the
  oracle (7).
- **Codex:** found 3 and 7, and noted 2 and 5 as observations. Its prompt pointed it at git refs of
  the change, so the unchanged skill file (1) was outside what it read; it had no reason to look
  for an issue (12), and it did not open the CHANGELOG pointer's target (4).

## Feedback memories to codify

None beyond the four lesson entries: the plan-text lesson belongs with `/improve`'s plan template,
which is the orchestrator's to change once all the improve branches are reviewed.
