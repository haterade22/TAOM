# Input pack: WP 106 offthread-and-tick-safety

Title (draft): Park off-thread callback writes (#634) and recover the mission tick handshake safely
Wave: 1   Lane: A   Draft priority: P0   Groups: 2   Port commits: 2
Depends on (WP ids): 103, 105

## Orchestrator notes for this WP (binding)
- Priority P1, not P0 (V05): #634 is defence in depth; the freeze cause is unproven. Port the Patch91 / stall-watchdog half first.
- Decision D17: add a LAUNCH-AWARE recovery on Mission.OnTick that restores tickCompleted only when this call launched no agent-tick job (V04 explains why the lens's unconditional restore is unsafe: Patch35's postfix can throw after TickAgentsAndTeamsAsync launched the job).
- V04: porting Patch91 without 6eb2e1b90's PatchShield method exclusion adds a missing-API swallow inside the agent tick; coordinate with WP107 (it owns the exclusion mechanism).
- Decision D16: do NOT add a locked RNG for TaomCombatMechanicsModel (V05: concurrent xorshift128 draws cannot plausibly reach a constant state; 1.5.x never moved the draw).
- Group issue-965-coop-safety-nets: port only bd1ac8292's tickCompleted lesson and re-cited comments (decision D7 keeps co-op features out).
- Player-facing: the MCM QA toggle 'Throw On Next Mission Tick' can freeze permanently on v1.4.8 (V04): make the recovery cover it, or document it.
- K1/D16: do NOT add the locked RNG from 9f58c58de (decision D16). DO add a multi-thread callback row to .claude/rules/harmony-patches.md's thread table (v1.4.8 Mission.cs:6455-6456 GetDefendCollisionResults is [MBCallback(null, true)]; MBRandom.cs:22/:32; MBFastRandom.cs:23-32/:83) so future code on that path knows it runs off the main thread.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V04-tick-handshake-and-start-swallow.md
- plans/_audit/2026-11-04-port148/verify/V05-concurrent-collections-and-rng.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Deep-dive findings and non-port items this WP owns
- FREEZE-THREADING-01 [P1/high] The tick handshake turns a stuck agent tick, or any exception swallowed between tickCompleted=false and the agent tick, into a permanent freeze | port: 42624b95b,8e6b0935c,8c84fa20e,9e8c99bdb,f75288ebf  (report: plans/_audit/2026-11-04-port148/deepdive/freeze-threading.md)
- FREEZE-THREADING-03 [P1/medium] Eight engine-callback handlers on 1.4.x write main-thread Dictionaries and Lists from whichever thread native raises them on (#634 class) | port: f75288ebf,efe326242,e5f644da4,b8b144635,fe8f30c7a  (report: plans/_audit/2026-11-04-port148/deepdive/freeze-threading.md)
- RUNTIME-PERF-04 [P1/medium] Off-thread engine callbacks still write unlocked collections in six of the 25 battle behaviours (f75288ebf not ported) | port: f75288ebf  (report: plans/_audit/2026-11-04-port148/deepdive/runtime-perf.md)

## Port groups owned by this WP (from the commit classification)

### group `issue-634-offthread-callbacks`  P0  track=freeze-threading  commits=1  sim: clean=0 conflict=1
symptoms: battle-freeze
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/CareerSystem/CareerPerkMissionBehavior.cs, Main/Features/SignatureStrikes/Hooks/SignatureAgentRoster.cs
- `f75288ebf` 2026-09-22 PORT P0 pick=conflict :: fix(mission): v2.0.30 - park off-thread callback writes (#634)
  - why: Parks every TAOM write reachable from engine callbacks raised off the main mission thread and adds a stall watchdog that logs stuck threads' stacks; it targets the freeze shape of evidence items 3 and 5 (cause unproven on 1.5.x too) and is absent on the 1.4.x tip.
  - port notes: Drop the SignatureStrikes/Hooks/SignatureAgentRoster.cs hunk (SignatureStrikes is 1.5.x-only, 0700ca901). CareerPerkMissionBehavior.cs conflicts only on context from 1.5.x's #611/#613 commits (2b1d1ef96, 7ede923be): hand-merge and omit the #611 mount-recompute line unless #611 is ported. Patch91 targets exist with the same signatures on v1.4.8 (Mission.TickAgentsAndTeamsImp(float,bool) at Mission.cs:3601; private MissionState.TickMissionAux(float,float,bool,bool) at MissionState.cs:212) and no Patch91 exists on the 1.4.x tip. SubModule.cs is single-owner (guarded PatchCategory plus watchdog Start). The new MCM toggle needs its CoopSettingsRelevance row and the SettingsFingerprintTests count; add Patch91 to harmony-patch-registry.md and CLAUDE.md's category count. Off-thread reports move from [ERROR] via BTRegister.Logger to a file-log WARNING, so E4-style lines read differently after the port. Later 1.5.x commit 6eb2e1b90 touches the same watchdog.
  - conflicts: Main/Features/CareerSystem/CareerPerkMissionBehavior.cs, Main/Features/SignatureStrikes/Hooks/SignatureAgentRoster.cs
  - issues: 595, 634

### group `issue-965-coop-safety-nets`  P2  track=coop  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Dependencies/Foundation/CollectAssemblyTypesPolicy.cs, Dependencies/Foundation/CollectAssemblyTypesShim.cs, Dependencies/Foundation/SaveShield.cs, Dependencies/Foundation/SaveShieldPolicy.cs, Dependencies/Foundation/ShieldCoverage.cs, Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs, Main/Features/CoopInterop/CoopSessionProvider.cs, TAOM.Tests/Features/Arena/Patch69TournamentEndGuardDecisionTests.cs, TAOM.Tests/Features/CoopInterop/CoopSessionProviderBindTests.cs, TAOM.Tests/Infrastructure/Dependencies/CollectAssemblyTypesPolicyTests.cs, TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs, TAOM.Tests/Infrastructure/Dependencies/SaveShieldPolicyTests.cs, TAOM.Tests/Infrastructure/Dependencies/ShieldCoverageTests.cs
- `bd1ac8292` 2026-10-21 PORT-PARTIAL P2 pick=conflict :: fix(coop): v2.0.35 - revert co-op rethrows that could hang a battle
  - why: Mostly reverts 6ce20f6ed's co-op rethrows, which never reached 1.4.x. What remains is a behaviour-neutral ShouldSwallow refactor, a narrowed BannerlordCoop type-load policy, and a lesson that a throw out of OnMissionTick hangs the next frame, which holds on v1.4.8.
  - port notes: Take the harmony-il and misc lessons and the SaveShieldPolicy and Patch69 comments, re-cited to v1.4.8: Mission.cs:3740 sets tickCompleted=false before the OnMissionTick loop, :3585-3591 is the WaitTickCompletion spin, and :3613 (TickAgentsAndTeamsImp) is the only place that sets it true. The v1.5.5 line numbers in the 1.5.x text do not apply. The ShouldSwallow extraction and its tests are optional and behaviour-neutral on 1.4.x, where SaveShieldPolicy and Patch69 still equal the fork. Take the CollectAssemblyTypesPolicy narrowing (file created in 6ce20f6ed) only if co-op is in scope and the assembly names of a v1.4.8-era BannerlordCoop, including the runtime AutoSync assembly, are confirmed. UNVERIFIED: there is no Coop install on this laptop.
  - prereqs: 6ce20f6ed
  - conflicts: Dependencies/Foundation/CollectAssemblyTypesPolicy.cs, Dependencies/Foundation/CollectAssemblyTypesShim.cs, Dependencies/Foundation/SaveShield.cs, Dependencies/Foundation/SaveShieldPolicy.cs, Dependencies/Foundation/ShieldCoverage.cs, Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs, Main/Features/CoopInterop/CoopSessionProvider.cs, TAOM.Tests/Features/Arena/Patch69TournamentEndGuardDecisionTests.cs, TAOM.Tests/Features/CoopInterop/CoopSessionProviderBindTests.cs, TAOM.Tests/Infrastructure/Dependencies/CollectAssemblyTypesPolicyTests.cs, TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs, TAOM.Tests/Infrastructure/Dependencies/SaveShieldPolicyTests.cs, TAOM.Tests/Infrastructure/Dependencies/ShieldCoverageTests.cs
  - issues: 965

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 3, batch 22
