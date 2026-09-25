# Deep review and Codex: plan 002, NaN and Infinity in career mutation floats (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 002, MutationParams.GetFloat rejects NaN and Infinity (branch improve/002-nan-guards)
Date: 2026-09-24
Diff: a39a9c86..78889a85 (MutationParams.cs +2, MutationParamsTests.cs new, CHANGELOG.md +12)

Scope:   C# (one accessor, one test file), CHANGELOG
Waves:   one wave: Agent 1 Standards, 2 Engine compatibility, 3 Efficiency, 4 Completeness,
         5 Data flow, 6 Design; Codex gpt-6-astra (ultra) in parallel

STANDARDS:     PASS, 0 blocking; 2 LOW (silent fallback vs rule 5, no issue), 1 NIT (untested branch)
COMPATIBILITY: PASS, 0 incompatible, 0 unverified (no TaleWorlds API in the diff)
EFFICIENCY:    PASS, 0 issues
COMPLETENESS:  INCOMPLETE, GitHub issue missing; 4 LOW (2 untested branches, doc note, silent fallback)
DATA FLOW:     PASS in the changed code, 0 gaps, 1 LOW inconsistency (silent fallback)
DESIGN:        2 KEEP proposals (0 apply, 2 follow-up); the guard itself ALREADY OPTIMAL
XML:           NOT IN SCOPE (no XML changed)
TOOLING:       NOT IN SCOPE
```

Every lens finding and both Codex observations were re-checked against the worktree at `78889a85`
before classification. The guard at `Main/Features/CareerSystem/Mutations/MutationParams.cs:19-21`
reads `(TryParse && IsFinite) ? result : defaultValue`, and three lenses independently decompiled the
installed net472 `mscorlib` to show that `"NaN"`, `"Infinity"` and `"-Infinity"` (trimmed,
case-sensitive) are the only strings `Single.TryParse` turns into a non-finite float; `"1e39"` fails
the parse. The three RED tests therefore cover every non-finite input the accessor can see.

## Details

**Agent 1, Standards.** Checks 1 to 10 pass. Findings: (1) LOW, the new fallback is silent against
`csharp-architecture.md` "Config Providers MUST Validate" rules 5 and 6; (2) LOW, no GitHub issue;
(3) NIT, the unparseable fallback and the negative finite pass-through are untested. Follow-ups:
`CareerConfigProvider.cs:448,476,516` check NaN by hand instead of `FiniteFloatValidator`, which also
made the CHANGELOG's "the same `FiniteFloatValidator` the other float loaders use" overbroad; the
calculator result reaches `prop.SetValue` unchecked (`MutationService.cs:89-97`).

**Agent 2, Engine compatibility.** No TaleWorlds API in scope. Verified `Single.TryParse`,
`Single.IsNaN`, `Single.IsInfinity` and `FiniteFloatValidator.IsFinite(float)` on net472; confirmed
net472 has no `float.IsFinite`, so the house helper is required. The path runs only on a campaign
ability activation (`AbilityEffectExecutor.cs:108-114`). Follow-up: calculator overflow, as Agent 1.

**Agent 3, Efficiency.** No issues. Two masked integer compares after a successful parse, run once
per matching mutation per V-press. Rejected (simplicity): parse-once caching and a compiled-delegate
cache for the reflection setter.

**Agent 4, Completeness.** Tests PASS, CHANGELOG PASS, IoC and SubModule.xml unaffected. GitHub issue
MISSING. LOW: two untested guard branches; the "read floats only through `GetFloat`" maintenance
note lives only in the plan; silent fallback. Follow-ups: `career-system.md:314` shows
`<Mutation target=...>` where the loader reads `target_id` (`CareerConfigProvider.cs:279`, verified);
unguarded float parses in `PolygonPointParser.cs:26-27` and `SiegeDefenseService.cs:253` (impact
UNVERIFIED); the Codex prompt file was untracked (committed with this report).

**Agent 5, Data flow.** 10 flows traced. `MutationService.cs:91` is the only constructor of
`MutationParams`, and all five built-in calculators read floats only through `GetFloat`, so the one
guard covers every calculator. Before the fix a `value="NaN"` Duration mutation made an area buff
last the whole mission (`MissionAbilityExecutionContext.cs:189,201`), and a NaN Radius reached the
native `GetNearbyAgentsAux` unguarded (native behaviour UNVERIFIED). Shipped data: 410 mutations,
all `calculator="flat"`, values 2 to 9, so shipped play is unchanged. In-scope LOW: the silent
fallback (T5). Follow-ups: F1 calculator results unchecked, F2 `operation` parsed and never read,
F3 the scaling calculators' 0.01 default for a bad `factor`, F4 the stale plan index row.

**Agent 6, Design.** The one-line guard is already optimal (`MBMath.IsValidValue` exists but the
house rule and ADR-007 prefer the TAOM helper). KEEP proposal 1 (FOLLOW-UP, behaviour-CHANGING): a
finiteness gate on the calculator result in `MutationService.ApplyMutation`, with a RED test
`MutateAbility_MultiplyOverflowsToInfinity_KeepsCurrentValueAndWarns`. KEEP proposal 2 (FOLLOW-UP,
behaviour-PRESERVING): delete the unread `MutationDefinition.Operation` and `OperationType`.

## Finding classification

| # | Source | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| 1 | A1 NIT, A4 LOW x2, A5, A6 | LOW | Unparseable fallback and negative finite pass-through untested; narrowing the guard to `IsFiniteAtLeast(result, 0f)` would pass all 5 tests | CONFIRMED | FIXED: `GetFloat_UnparseableValue_ReturnsDefault`, `GetFloat_NegativeFiniteValue_ReturnsParsed` |
| 2 | A4 LOW | LOW | Calculator authors are told nowhere to read floats only through `GetFloat` | CONFIRMED | FIXED: `docs/features/career-system.md` "Add a new mutation calculator" step 1 |
| 3 | A1 follow-up | LOW | CHANGELOG said "the same `FiniteFloatValidator` the other float loaders use"; `CareerConfigProvider.cs:448,476,516` use hand-written checks (re-read) | CONFIRMED | FIXED: CHANGELOG entry reworded |
| 4 | A1 LOW, A4 MEDIUM | MED (process) | No GitHub issue for plan 002; `gh issue list --state all --search MutationParams` returns nothing (re-run) | CONFIRMED | NEEDS MIKE: filing is public. CHANGELOG now says "No issue filed yet" |
| 5 | A1 LOW, A4 LOW, A5 T5 | LOW | A non-finite param falls back with no warning, against rule 5; the plan chose silence because `MutationParams` has no logger (plan line 284) | CONFIRMED conflict, plan-sanctioned | NEEDS MIKE: warn at load in `CareerConfigProvider.ParseChoice` (outside the plan's files), or a shipped-data gate asserting every mutation `value`/`factor` is finite |
| 6 | Codex P3-1 | LOW | Plan text still describes the TroopWeight half and 8 tests; the port is mutation-only | CONFIRMED (plan doc) | Not fixed here: plan text and `plans/README.md:38` belong to the orchestrator |
| 7 | Codex P3-2 | LOW | Plan command recipes (`plans/002-...md:105-110`) omit `-p:ModuleId=` (re-read) | CONFIRMED (plan doc) | Not fixed here: orchestrator; this review ran with both properties |

No finding was a false positive. No HIGH or CRITICAL finding; no Codex P1 or P2.

## Action items

1. Mike: file the GitHub issue for plan 002 (links #128) and cite it in the CHANGELOG heading.
2. Mike: decide finding 5 (load-time warning or shipped-data gate) and the calculator exit gate
   (Agent 6 proposal 1), ideally together, since the exit gate would also carry the rule 5 warning.
3. Orchestrator: refresh the plan 002 text and its `plans/README.md` row (Codex P3-1, P3-2).

## Improvements (Step 4)

APPLIED: none. Agent 3 had no APPLY-scoped fix, and both Agent 6 KEEP proposals are FOLLOW-UP
(pre-existing code outside the diff). The defect fixes above are listed in the table, not here.

NOT APPLIED:
- `Main/Features/CareerSystem/Mutations/MutationService.cs:89-97`, Agent 6 proposal 1 (exit gate on
  the calculated value): FOLLOW-UP and behaviour-CHANGING, needs Mike. No issue filed (public action).
- `Main/Features/CareerSystem/Domain/MutationDefinition.cs:10,17,23`, `OperationType.cs`,
  `CareerConfigProvider.cs:282`, Agent 6 proposal 2 (delete the unread `Operation`): FOLLOW-UP,
  pre-existing and unrelated to plan 002. Verified: nothing in `Main` reads `.Operation`.

FOLLOW-UP (pre-existing; no issues filed, since filing is public and needs Mike's word):
- `CareerConfigProvider.cs:448,476,516`: swap the hand-written NaN checks for `FiniteFloatValidator`
  (no runtime change); `ParseFloat` (`:505-519`) also falls back silently.
- `BuiltInCalculators.cs:11,14`: a bad `factor` on `skill_scaling`/`level_scaling` falls back to a
  real 0.01 bonus, unlike the no-op defaults of the other three calculators (design question).
- `docs/features/career-system.md:314`: the example uses `target=`; the loader reads `target_id`.
- `FactionMap/Widgets/PolygonPointParser.cs:26-27`, `Siege/SiegeDefenseService.cs:253`: float parses
  with no finiteness check (impact UNVERIFIED).
- `career-system.md` Tests table lists 14 of 35 CareerSystem test files.

Convergence pass (Step 4.6): not launched; this delegate cannot spawn agents. The fix diff is two
tests, one doc step and a CHANGELOG paragraph, with no production code change
(`git diff 78889a85 -- Main/` is empty).

## Verification

- Filtered: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter
  FullyQualifiedName~MutationParamsTests`: 7 passed, 0 failed.
- Mutation check: the guard temporarily changed to
  `(TryParse(...) || true) && FiniteFloatValidator.IsFiniteAtLeast(result, 0f)`; the two new tests
  failed (`GetFloat_UnparseableValue_ReturnsDefault`, `GetFloat_NegativeFiniteValue_ReturnsParsed`),
  the other 5 passed. Reverted; `git diff -- Main/` empty afterwards.
- Full suite on the fixed tree: `Passed! - Failed: 0, Passed: 10320, Skipped: 2, Total: 10322`.
  The branch is based on `a39a9c86`, so no failure was allowed, and none occurred.

VERDICT: READY FOR COMMIT (with the NEEDS MIKE items open; none is HIGH)

## Codex review

Codex gpt-6-astra (reasoning ultra) ran read-only against git refs; the raw output is
`docs/reviews/raw/codex-adversarial-002-nan-infinity-config-guards-2026-09-24.md` (gitignored),
complete with the `END OF CODEX REVIEW` line. Quality: it quoted the changed code and the helper,
traced every calculator's fallback, walked boot, save/load, mission and co-op paths, and
cross-referenced every key. It found no P1 or P2 and raised two P3 observations on the plan text.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| P3-1 | P3 | LOW | Yes | Plan still describes the TroopWeight half and eight tests; TroopWeight's guard has been on trunk since `bee07b48` (`TroopWeightXmlLoader.cs:86`). Plan doc, not code |
| P3-2 | P3 | LOW | Yes | `plans/002-nan-infinity-config-guards.md:105-110` lacks `-p:ModuleId=`, which `.ai/verification.md` requires. Plan doc, not code |
| S1-S9 | suspects | n/a | Yes | S1 drift CONFIRMED (acknowledged in the CHANGELOG); S2 to S9 DISPUTED, each re-checked against the cited lines; S3 (RED/GREEN, suite totals) now executed here |

- **Confirmed bugs:** none in the code. Two plan-text errors (P3-1, P3-2), left to the orchestrator.
- **False positives:** none.
- **Design questions:** none raised by Codex.
- **Things Codex missed:** the two untested guard branches (finding 1), the calculator-result
  overflow past the accessor guard (Agent 6 proposal 1; Codex saw it, "does not prevent subsequent
  arithmetic overflow", but scoped it out rather than flagging it), the silent fallback against
  rule 5, and the missing issue.

### Phase 3e root cause (Codex-found items)

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| P3-1 | Plan text stale against its base | Other: plan drift | The plan was written against a June base; the port changed scope and the plan was not refreshed | Orchestrator refreshes a plan's text when its scope shrinks at execution |
| P3-2 | Plan recipes omit `-p:ModuleId=` | Convention inconsistency | Plan template predates the `ModuleId` requirement | Orchestrator: plan template uses `.ai/verification.md` commands verbatim |

## AGENTS.md lessons (pending)

Phase 3h is consolidated later for all branches. Proposed additions:

- **What Codex does well:** traced every calculator's fallback direction in a table and checked the
  plan's own instructions against the current verification rules.
- **Bugs Codex typically misses:** a guard's untested pass-through branch (a negative finite value a
  finite-only guard must still accept), and a result computed downstream of a guarded input that
  can still go non-finite (it noted the overflow and scoped it out instead of flagging it).
