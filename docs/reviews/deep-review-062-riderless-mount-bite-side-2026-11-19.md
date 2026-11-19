# Deep review: plan 062, spare a riderless mount on the biter's side (#594)

```
DEEP REVIEW REPORT
===================
Feature: Spider and warg bites: spare a riderless mount on the biter's side (#594), plan 062
Date: 2026-11-19

Scope:   C# (new Main/Features/AdvancedCombat/MountRiderSides.cs and CreatureBiteSides.cs; AgentAdapter,
         IAgentAdapter, AdvancedCombatBehavior OnAgentBuild and a new OnAgentMount override,
         SpiderAttackService, WargAttackService), 7 test files, 2 feature doc lines.
         Diff af0f5f330..d0abf3f49 on improve/062-riderless-mount-bite-side.
Blast radius: UNCHECKED by graphify (no graph exists for this worktree, and the lead may not run its
         refresh); git grep -w: IAgentAdapter in 13 Main files, AgentAdapter in 7, SpiderAttackService
         in 4, WargAttackService in 6, AdvancedCombatBehavior in 7. CreatureBiteSides.Spares is the only
         caller of IAgentAdapter.IsSameTeam and HasKnownSide in Main (git grep, lead re-checked).
Waves:   one wave: Agent 2 (Engine compatibility), Agent 5 (Data flow). Tier 1, mission combat.

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: PASS, 0 incompatible, 2 unverified (8 verified)
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     PASS, 0 gaps, 1 inconsistency (LOW, confirmed and fixed)
DESIGN:        SKIPPED BY TIER
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Details

Line numbers in the details are at the reviewed commit `d0abf3f49`; the fix moved the lines of
`SpiderAttackService.cs` below :110 down by one.

### Agent 1, Standards

SKIPPED BY TIER (deep-review tier 1 rule).

### Agent 2, Engine compatibility

Verified against the installed v1.5.7 DLLs (taom-src cache `v1.5.7-202611101605`). The lead re-read the
load-bearing lines:

- `Agent.OnMount` is `[MBCallback(null, false)]` and calls `Mission.OnAgentMount(this)` only inside
  `if (HasBeenBuilt)` (Agent.cs:1772-1789); `Mission.OnAgentMount` raises every behaviour's
  `OnAgentMount(agent)` with the rider (Mission.cs:5084-5089). So the spawn-time mount never reaches the
  override, and the `OnAgentBuild` record is required.
- `Mission.SpawnAgent` sets the rider's team (Mission.cs:4209), calls `SetMountAgentBeforeBuild` (:4325),
  builds and raises the mount (:4374-4378), then raises the rider's `OnAgentBuild` (:4389-4391). Vanilla
  reads `agent.MountAgent` between the two (:4359), so the engine relies on the cache being set there.
- `CreateHorseAgentFromRosterElements` (:4602) creates the mount with no `SetTeam`, as the
  `MountRiderSides` summary says.
- `Agent.Team` is a managed auto-property, `Team` has no `operator ==`, and `MountAgent`, `RiderAgent`
  and `IsMount` exist (lens citations; not re-read by the lead).

UNVERIFIED (2), as the orchestrator allowed:

- Whether native updates `_cachedMountAgent` (`UpdateMountAgentCache`) synchronously inside
  `SetMountAgent`, and before `OnMount` on a remount. `native_decompile.py --string UpdateMountAgentCache`
  found no string. Vanilla `AgentHumanAILogic.OnAgentMount` reads `agent.MountAgent` in the same callback
  (AgentHumanAILogic.cs:32-35), which points to yes. If it were late, the two cases fail apart. At spawn, the
  mount is unrecorded and so spared: a missed bite. On a remount, it keeps the earlier record, so a horse
  taken from the other side can be wrongly bitten by its new side, or wrongly spared by its old side once
  loose. The in-game remount check stays owed.
- Which thread native uses for `OnMount`. The engine does not declare it multi-thread callable; the
  handler is safe either way (`ConditionalWeakTable` is thread-safe, `Side.Team` is volatile, the body
  is in try/catch, and the log-once bool matters only on the error path).

Lead verdict: no finding. The capped-path note (an unknown-side loose mount counted as an ally) is the
same item as Agent 5 trace 6, F1 below.

### Agent 3, Efficiency

SKIPPED BY TIER.

### Agent 4, Completeness

SKIPPED BY TIER.

### Agent 5, Data flow

Six flows traced: the spawn record, the remount record, a rider who changes side, reading a side, the
ledger's lifecycle and the creature diagnostics. Five connected; one inconsistency:

- Trace 6, F1 (LOW). `SpiderAttackService.IsSameSide` now delegated to `CreatureBiteSides.Spares`, which
  is true for a loose mount of unknown side. With `countAllies` (capped strikes, SpiderAttackService.cs:131)
  such a mount was counted in `allies`, which `CreatureBanditDiag.NoteAttack` logs as `alliesInArc` and
  subtracts from the arc to get its enemy count (CreatureBanditDiag.cs:201-204), while the comments at
  :110-111 and :119 still said "allies". Lead verdict: CONFIRMED, LOW, diagnostics only. The count itself
  is right for its question (a spared agent is skipped by design, so it is not a miss); the name and the
  comments were not. Fixed (see APPLIED). The other half, a loose mount of known enemy side counted as an
  enemy though the capped strike never bites a loose mount, predates the plan: FOLLOW-UP.
- Trace 3, a rider who changes side while mounted: INFO. A ridden mount is judged by its live rider; a
  riderless one keeps the team of its last rider at his last mount. The only `SetTeam` caller in Main is
  `CreatureBanditSpawner.cs:128`, which teams riderless creatures, so no call site reaches the case.
- INFO, no change: in `OnAgentBuild`, `RecordRider` shares the try block with
  `_adapterFactory.OnAgentBuilt` (AdvancedCombatBehavior.cs:59-63). A throw there skips the record, the
  mount's side stays unknown, and it is spared. That failure is safe and logged once; a second try block
  would add code for no observed fault (simplicity criterion).

### Agent 6, Design and elegance

SKIPPED BY TIER.

### Agent 7, XML and ModuleData integrity

NOT IN SCOPE (no XML in the diff).

### Tooling correctness

NOT IN SCOPE (no hook, validator or tool in the diff). No gate changed, so no differential sweep.

### Orchestrator probes

- Thread safety of `OnAgentMount` off the main thread (#634): safe either way (Agent 2, last item).
  Native's thread is UNVERIFIED.
- A remount, and a mount whose rider changes side: each `OnAgentMount` overwrites the entry with the
  current rider's team; a ridden mount is judged by its rider (CreatureBiteSides.cs:17, :20). See trace 3.
- An unresolvable side spares the mount in both paths: yes. `Spares` returns true for a riderless mount
  with no known side before the null-biter check (CreatureBiteSides.cs:18-19), so the spider's uncapped
  strike (`IsStrikeable`) and the warg (`Spares(null, mount)`) both spare it. Pinned by
  `SpiderStrikeCapTests.RiddenStrike_ALooseMountOfUnknownSide_IsNotStruck` and the warg tests
  `HandleWargTargetHit_LooseMountOfUnknownSide_DoesNotDamage` and
  `HandleWargTargetHit_RiderlessWargAndLooseMountOfUnknownSide_DoesNotDamage`; the
  lead's mutant with the rule disabled failed that spider test ("a riderless mount of unknown side is
  never bitten").
- The capped spider path: what it strikes is unchanged; `!IsLooseMount` still removes every loose mount
  (SpiderAttackService.cs:139). Only the diagnostic ally count moved (F1).
- `rider.MountAgent` when `OnAgentBuild` fires: set by strong inference (Mission.cs:4325, :4359, :4391);
  the native cache timing is UNVERIFIED.
- The executor's 36 RefAsm no-game failures (`Failed!  - Failed:    36, Passed: 36165, Skipped:     3,
  Total: 36204`, exec-062 `refasm-unit.log`): none is in a file this branch touches. The lead intersected
  the 30 distinct failed method names in that log with the 110 test methods declared in the 7 touched
  test files: 0 in common. No base RefAsm run was made, so whether all 36 also fail on the base is
  UNVERIFIED. The touched engine-bound test classes carry `[TestCategory("RequiresGame")]`.

## Action items

1. Done: the spider's side predicate is named for what it answers (`IsSpared`), and the diagnostics
   comments say a loose mount of unknown side is counted among the spared.
2. Owed (in game, not run here): spawned cavalry record their mount's side at `OnAgentBuild`, and a
   remount records the new mount.

## Improvements (Step 4)

- APPLIED (line numbers after the fix): `Main/Features/Spider/SpiderAttackService.cs:198`, `IsSameSide`
  renamed to `IsSpared` (both callers, :132 and :182) with its comment saying what it is true for; comments at :110-111 and :119-120
  name what the capped strike counts. Behaviour-preserving (a compiler-checked rename and comments).
  Test `SpiderStrikeCapTests.CappedStrike_ALooseMountOfUnknownSide_IsCountedAmongTheSpared_NotAsAMiss`
  pins the count; it failed on a mutant with the spare rule disabled ("Expected:<1>. Actual:<0>") and
  passes on the branch. `TAOM.Tests/Features/Spider/SpiderStrikeCapTests.cs:82`, the helper comment now
  names `IsSpared`.
- NOT APPLIED: `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs:59-63`, a separate try block for
  `RecordRider` in `OnAgentBuild`. The failure it would cover is safe (the mount is spared) and logged;
  the simplicity criterion rejects it. No Agent 3 or Agent 6 proposals exist (skipped by tier).
- FOLLOW-UP: a capped strike counts a loose mount of known enemy side as an enemy though it never bites
  one, so `attack-whiff` can fire on it (CreatureBanditDiag.cs:203-204, SpiderAttackService.cs:139).
  Predates the plan. No issue filed: an /improve lead files none, and the orchestrator decides at wrap-up.

## Verification

- Base totals in the worktree at `d0abf3f49`, before any edit:
  `Failed!  - Failed:     3, Passed: 43064, Skipped:     9, Total: 43076` (the three known language-file
  failures: `EveryLanguage_DeclaresARowForEveryEnglishKey`, `NoTranslatedString_MixesWritingSystems`,
  `EveryKey_IsSeededInAllTwelveLanguageFiles`).
- Mutant (the spare rule disabled in `CreatureBiteSides.Spares`, then restored with the same edit
  undone): `Failed!  - Failed:     2, Passed:     6` in `SpiderStrikeCapTests`, the two unknown-side tests.
- After the fix: `Failed!  - Failed:     3, Passed: 43065, Skipped:     9, Total: 43077`, the same three
  failures and the one new test passing.
- Not covered: no game run (tier 1 rule for this item); the new test sits in a `RequiresGame` class, so
  the RefAsm no-game replay was not re-run; no tools or hooks changed, so the Python and hook suites were
  not run.

## Codex review

Codex not run for this item (no paid dispatch was authorised for it).

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| - | Codex not run | - | - | - |

- Confirmed bugs: none from Codex.
- False positives: none.
- Design questions: none.
- Things Codex missed: not applicable; F1 came from Lens 5.

Codex lessons (pending): none. With no Codex run there is no false-positive pattern or missed category
to record.

VERDICT: READY FOR COMMIT
