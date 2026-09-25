# RCA: plan 002, NaN and Infinity in career mutation floats (2026-09-24)

## Summary

Six deep-review lenses and Codex gpt-6-astra reviewed `78889a85` on `improve/002-nan-guards`, the
port of `MutationParams.GetFloat`'s finiteness guard. The guard is correct and covers every
non-finite string net472's `Single.TryParse` accepts. The review confirmed seven LOW-to-MEDIUM
findings and no HIGH: three fixed here (two untested guard branches, a missing calculator-author
note, an overbroad CHANGELOG claim), two left for Mike (the missing GitHub issue, the silent
fallback against config rule 5) and two plan-text errors left for the orchestrator. Every lens also
flagged, as FOLLOW-UP, that a finite parameter can still overflow to Infinity inside a calculator,
past the accessor guard, because `MutationService.ApplyMutation` has no exit gate. That is
pre-existing and behaviour-changing, so it waits on Mike.

The bug the plan fixed is a config float, a category `csharp-architecture.md` already names in its
NaN-gate history (which counts six shipped instances), so no scope widening is owed. It escaped
issue #128's NaN sweep because the value is stored as a raw string at load and parsed only at use.
The plan closed the literal-string route; the overflow route is
the "gate the service's own EXIT" clause of the same rule, which the plan explicitly set aside as
redundant.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | LOW | `GetFloat`'s unparseable fallback and its negative finite pass-through had no test; narrowing the guard to `IsFiniteAtLeast(result, 0f)` passed all 5 tests | Test coverage | The June commit wrote one test per rejected value and none for the values the guard must still accept; the plan listed "negative values must pass" as a reviewer probe, not as a test | Two tests added, proved by a mutant; lesson in `lessons/testing-qa.md` |
| 2 | LOW | The rule "read calculator floats only through `GetFloat`" lived only in the plan's maintenance notes | Documentation | A plan's maintenance notes are not read by the next calculator author | One step added to `docs/features/career-system.md` "Add a new mutation calculator" |
| 3 | LOW | CHANGELOG claimed "the same `FiniteFloatValidator` the other float loaders use"; `CareerConfigProvider.cs:448,476,516` use hand-written checks | Documentation accuracy | The claim was written from the TroopWeight comparison, not from a grep of the feature's own loader | Reworded; one-off, no rule |
| 4 | MED (process) | No GitHub issue for the fix | Process | The plan assigned the issue to the orchestrator, and the port ran without it | Needs Mike (public action); CHANGELOG says so |
| 5 | LOW | A rejected non-finite param falls back with no warning, against config rule 5 | Convention inconsistency | The plan decided that `MutationParams` has no logger, so silence is the only option, without looking one layer up to `CareerConfigProvider.ParseChoice`, which holds the logger and the choice id | Needs Mike; lesson in `lessons/gamemodels-services.md` |
| 6 | LOW | Plan text still describes the TroopWeight half and eight tests (Codex P3-1) | Plan drift | Plan written against a June base; scope shrank at execution | Orchestrator refreshes the plan |
| 7 | LOW | Plan command recipes omit `-p:ModuleId=` (Codex P3-2) | Convention inconsistency | Plan template predates the requirement | Orchestrator uses `.ai/verification.md` commands |

## Root-cause pattern

Findings 1 and 5, and the calculator-overflow follow-up, share one shape: the fix was judged at the
accessor alone. The tests covered what the accessor rejects, not what it must keep; the warning was
ruled out because the accessor has no logger; and the plan called the accessor "the single
chokepoint", which holds for parsing but not for the value the service finally writes. The layer
above (`CareerConfigProvider` at load) and the layer below (`MutationService.ApplyMutation` at the
write) both hold a logger and the context a warning needs.

## Why each agent missed these

The implementing agent (the June `impl-002` builder, then this port) is the one that missed them;
the review lenses caught all of them.

- **Builder (June and port):** followed the plan's test list exactly; the plan's reviewer probe 2
  ("negative values must still pass") was framed as a review question, so no test was written for
  it. The port was byte-identical to `cfc47206` by design, so it inherited the gap.
- **Agent 2 (Engine) and Agent 3 (Efficiency):** their rule sets do not cover test completeness or
  documentation; both still reported the overflow follow-up.
- **Codex:** traced the fallback of every calculator and noted that the accessor "does not ... prevent
  subsequent arithmetic overflow", then scoped it out as outside the literal-parsing contract
  instead of flagging it. It did not check the test set for pass-through cases.

## Feedback memories to codify

None as memory files. Two lessons appended:
- `docs/reviews/lessons/testing-qa.md`: test the values a validation guard must still accept.
- `docs/reviews/lessons/gamemodels-services.md`: a guard on a lazily parsed parameter is not the
  service-exit gate.
