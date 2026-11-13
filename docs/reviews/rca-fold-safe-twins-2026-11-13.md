# RCA: fold three behaviour-identical twins (plan 047, deep review 2026-11-13)

## Top-line

Two deep-review lenses (2 Engine compatibility, 5 Data flow) reviewed `664f19c5b..911373c9e` on
`improve/047-fold-safe-twins`. Codex was not run. The review lead re-checked every finding in the worktree:
**3 confirmed (0 HIGH, 0 MEDIUM, 3 LOW), 0 false positives.** None is a runtime defect: all three folds
return the same result as their twins for every input that can reach them, and the save hierarchy is
untouched. Two findings are new comment text that states a build or gate fact the code contradicts, both
copied verbatim from the plan; the third is a compile-time guard the fold removed with no test to replace
it. All three are fixed in the review commit, and the two new tests were RED under mutants of the call
sites they guard.

Review record: [deep-review-047-fold-safe-twins-2026-11-13.md](deep-review-047-fold-safe-twins-2026-11-13.md).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `CultureStageViewType.cs:7-9`: "SandBox.GauntletUI or SandBox.View, which TAOM cannot reference at compile time". `Main/TAOM.csproj:47-50` references every SandBox module DLL, other patches compile against `SandBox.GauntletUI`, and v1.5.7 ships the view only there | Doc claim against the build | A REPEAT of "A moved comment's engine claims are re-verified at the move" (`lessons/adapters-taleworlds-api.md`, #737). The plan (`plans/047-fold-safe-twins.md:786`) restated the three patches' old comment, and the executor copied it after proving the code byte-identical | Comment corrected; new lesson in `lessons/adapters-taleworlds-api.md` |
| 2 | LOW | `CultureStageViewTypeTests.cs:8-9`: the binding gate "proves that the names resolve against the installed engine". It fails when `TargetMethod()` returns null or a method with no body, or throws (`HarmonyPatchBindingTests.cs:76-79`, `:160-162`); nothing probes each name, and `Find()` stops at the first one | Doc claim against a gate | Same source: `plans/047-fold-safe-twins.md:752-753` supplied the sentence; nobody read the gate's failing branch against it | Comment corrected; same lesson |
| 3 | LOW | Both supply lists became `MBBindingList<SupplyRowVM>`, so building a list with the other factory compiles, and no screen test read a row's label | Test coverage of a removed guard | The fold's tests pinned each factory in isolation (`SupplyRowVMTests`); the screen tests asserted `ItemId` only, and the type split that used to make a swap a compile error was not listed as a property the fold gave up | Two `SupplyOrderScreenVMTests` cases, RED under two mutants of `SupplyOrderScreenVM.cs:660`, `:664`, `:753`; one-off, no lesson |

## Root-cause pattern

Findings 1 and 2 share a cause: the plan supplied finished comment text, and the executor treated its
sentences as part of the contract rather than as a draft to verify (standing rule 12 says plan text is a
draft). Byte parity was proved for the code; nothing proved the prose. This is the second time the
"moved comment" lesson has recurred on a refactor that was proved by byte parity, so the preventive action
is stronger than the first: an executor reports each new factual sentence with the line that proves it.

## Why each agent missed these

- **The executor:** ran the full suite, the binding gate and the RefAsm unit step (per the orchestrator's
  spec check); none of them reads comments. It took the plan's comments as given.
- **The plan review (`plans/_audit/2026-11-12-fable/plan-review-047.md`):** checked the plan's gates,
  its "Current state" excerpts against the code and the v1.5.7 engine, and its checklist (TDD order, ADRs,
  STOP conditions); it did not check the claims in the comment text the plan drafted.
- **Lens 2:** caught findings 1 and 2.
- **Lens 5:** caught finding 3, recorded as a LOW note because no current caller produces the wrong row.
- **Lenses 1, 3, 4, 6:** no report was handed to this lead, so their coverage is unknown.

## Follow-ups

- FOLLOW-UP (no issue: the review lead files no issues in an /improve run; listed for the orchestrator):
  the three unchanged patch comments (`CultureStageView_Constructor_Patch.cs:25-27`,
  `CultureStageView_Finalize_Patch.cs:23-24`, `CultureStageView_Tick_Patch.cs:23-24`) keep the claim of
  finding 1.
- FOLLOW-UP (no issue: plan 047 lists it as a follow-up candidate, `plans/047-fold-safe-twins.md:1101`): `docs/features/faction-map.md:22`
  says "Postfix on `Finalize`"; the code is a prefix on `OnFinalize`.

## Feedback memories to codify

None beyond the lesson: the rule belongs in the repo, not in a memory file.
