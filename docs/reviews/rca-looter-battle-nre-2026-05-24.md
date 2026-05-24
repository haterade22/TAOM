# RCA — Looter-battle NRE in `Mission.CheckMissionEnded` (2026-05-24)

## ⚠ 2026-05-24 POST-CODEX REVISION

The original RCA below identified `BehaviorTreeWrapper.dll`'s `BehaviorTreeMissionLogic` as the null source. **Codex adversarial review caught that this conclusion was wrong.** The deleted DLL declared `BehaviorType => (MissionBehaviorType)1`, and v1.4.5's enum is `Logic=0, Other=1` — so the DLL was actually reporting **Other**, not Logic. Vanilla `Mission.AddMissionBehavior` puts Other-type behaviors in `_otherMissionBehaviors`, never in `MissionLogics`. The wrapper was therefore never a source of null entries in `MissionLogics`, and the user's `CheckMissionEnded` NRE has a different (still unidentified) cause.

**What still stands:**
1. The library inlining (`Main/BehaviorTreeWrapper/` + `Main/BehaviorTrees/`) is a net-good change — full source ownership, single-DLL ship, 7 inherited perf issues fixed.
2. The `: MissionLogic` inheritance change is defensive — it ensures the wrapper participates correctly in `MissionLogics` iteration in case any future TaleWorlds version starts checking it there.
3. Codex Finding 1 (real regression): the `OnTickAsAI → OnTick` rename combined with v1.4.5's auto-tick of components in `Agent.Tick` caused 2× ticks per frame on every warg/spider. **Fixed:** removed the manual `comp.OnTick(dt)` from `WargMissionBehavior.cs:127` and `SpiderMissionBehavior.cs:152` — vanilla auto-tick now handles BT components correctly for both player- and AI-controlled agents.

**What still needs investigation:** the actual source of the null `MissionLogics` entry on the affected users' v1.4.5 builds. Candidates: a community module DLL (MCM, ButterLib, UIExtenderEx, Bannerlord.MBOptionScreen, BUTR.CrashReport infrastructure), or a TAOM source class the Explore audit's source-grep missed. Follow-up tracked as a separate investigation.

---

## Original (pre-Codex) top-line

Two users on `bannerlord-1.4.5` crashed with `System.NullReferenceException` in `TaleWorlds.MountAndBlade.Mission.CheckMissionEnded()` on entering a battle (looter encounter was the trigger, but the bug fires on **every** battle — looters were just the first encounter the users hit). Stack:

```
at bool TaleWorlds.MountAndBlade.Mission.CheckMissionEnded()
at void TaleWorlds.MountAndBlade.Mission.CheckMissionEnd(float currentTime)
at void TaleWorlds.MountAndBlade.Mission.OnTick_Patch1(...)
```

Root cause: **the vendored `BehaviorTreeWrapper.dll`'s `BehaviorTreeMissionLogic` class inherited `MissionBehavior` (not `MissionLogic`) while reporting `BehaviorType => MissionBehaviorType.Logic`.** This is the exact pattern documented in `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance.md` (RCA 2026-05-14 — fixed in TAOM source for MixedFormations/SmartCavalryAI/SiegeDismount). Vanilla v1.4.5 `Mission.AddMissionBehavior` runs:

```csharp
case MissionBehaviorType.Logic:
    MissionLogics.Add(missionBehavior as MissionLogic);
    break;
```

The `as` cast returns `null`, a null slot lands in `_missionLogics`, and `CheckMissionEnded`'s `foreach (var ml in MissionLogics) ml.MissionEnded(out _);` NREs on the null receiver every tick.

The 2026-05-14 fix audited TAOM source only. It missed the vendored `BehaviorTreeWrapper.dll` because the audit was source-grep based, not assembly-introspection based. The vendored DLL has always carried this bug; on v1.3.15 it presumably manifested the same way and was masked by some accident of test ordering or by the same users not having reached battle in those builds. On v1.4.5 the cast semantics are unchanged, so the bug has always been live wherever this DLL ran.

## Fix shipped

Rebuilt **both** vendored DLLs as TAOM-owned source — no black-box code left in the BT stack. Both inlined into `TAOM.dll`, single ship surface. Steps:

1. Decompiled `Main/_Module/bin/Win64_Shipping_Client/BehaviorTreeWrapper.dll` (~1300 lines) and `BehaviorTrees.dll` (~980 lines) via `ilspycmd`.
2. Ported into `Main/BehaviorTreeWrapper/` and `Main/BehaviorTrees/` (inlined — compile into `TAOM.dll`, no separate assemblies). Cleaned ILSpy artifacts (`//IL_xxxx:` comments, struct init quirks, missing `using` directives). Rewrote C# 12 primary-constructor syntax (`class Foo(args) : Base(args)`) to plain ctors since TAOM compiles at `LangVersion=10`.
3. Dropped unused demo code: `BehaviorTreeWrapper.Tests` namespace (`ExampleTree`, `SequenceToSelectorTree`, `AlwaysFalseDecorator`), `FPSCounter`.
4. **Applied the fix:** `BehaviorTreeMissionLogic : MissionLogic` (was `: MissionBehavior`). With `MissionLogic` as the base, `BehaviorType` returns `Logic` by inherited default and the `as MissionLogic` cast in vanilla `AddMissionBehavior` succeeds.
5. Reconciled v1.3 → v1.4.5 API drift surfaced by the rebuild:
   - `AgentComponent.OnTickAsAI(float)` → `OnTick(float)` at three callsites (`BehaviorTreeAgentComponent`, `WargMissionBehavior.cs:127`, `SpiderMissionBehavior.cs:152`).
   - `MBInformationManager.AddQuickInformation` now requires an `Equipment` argument at position 4.
6. Deleted both vendored DLLs from `Main/_Module/bin/Win64_Shipping_Client/` and removed both `<Reference>` entries from `Main/TAOM.csproj`.
7. Added regression test `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs` that asserts `typeof(MissionLogic).IsAssignableFrom(typeof(BehaviorTreeMissionLogic))` so any future regression fails CI before reaching a player.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| F1 | HIGH | `BehaviorTreeWrapper.BehaviorTreeMissionLogic : MissionBehavior` with `BehaviorType => Logic` triggered the vanilla null-cast bug, NRE'd every battle. | **Same class as F1 of `rca-behaviortype-fix-2026-05-14.md` — vendored-DLL extension** | The 2026-05-14 audit was source-grep based (`grep -rn "BehaviorType" Main/`). Vendored DLLs are not in `Main/**/*.cs`, so they were invisible to the audit. The Explore agent invoked at the start of this session also missed it for the same reason — it only walked `.cs` files. | **Vendored-DLL audit is now in scope.** Memory entry `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance.md` updated to require decompiling every vendored MissionBehavior subclass before declaring an audit complete. Regression test added so a future re-vendoring that reintroduces the bug fails at test time. |
| F2 | LOW | `BehaviorTreeWrapper.dll` had no source repository — every prior bug had to be lived with or worked around. | **Vendor-source loss** | The DLL shipped from an external (now-defunct) author. No tracking issue had ever been opened to acquire or rebuild the source. | **Fix: inlined the source.** No more black-box DLL. Future bugs in this code are now fixable by `Edit` instead of `decompile → rebuild → re-vendor`. |
| F3 | LOW | The `act_map_rider_horse_attack_1h`-on-`as_human_warrior` warning flood in `rgl_log` was initially misread as the cause. The user even directed an action-set rebuild path. The 175k-line action-set rebuild plan was actively in progress (and approved) before the Phase 1 gate forced a v1.4.5 `Mission` decompile that surfaced the real cause. | **Symptom-correlation trap** | The action-set flood happens exactly at the encounter→battle transition (cosmetic engine warning that fires on pure vanilla v1.4.5 too). Its timing is identical to the crash, which made it look causal. Without the Phase 1 gate ("decompile Mission before generating XML"), we would have shipped 175k lines of useless XML and the crash would still be live. | **The gate worked.** Plan workflow already enforces Phase 1 verification before bulk-change phases — that discipline saved this session. No new rule needed; the existing pattern is sound. |

## Root Cause Pattern

F1 is the recurring lesson: **a documented bug pattern was fixed in source-grep scope but not in vendored-DLL scope.** The same class of bug shipped from two places; we caught half of it on 2026-05-14 and the other half on 2026-05-24. The memory entry now codifies "audit vendored DLLs too" so the next time someone tightens the `BehaviorType`/`MissionLogic` rule, both halves get the same treatment.

F2 is the structural fix that makes F1 unrepeatable: there's no longer a vendored DLL to forget about. Anything that was in `BehaviorTreeWrapper.dll` is now in `TAOM.dll`, visible to the standard source grep, and editable in-place.

## Inherited perf cleanup (deep-review E1–E7)

Five Claude agents reviewed the rebuild on 2026-05-24 (Standards / Compatibility / Efficiency / Completeness / Data Flow). Standards + Compatibility + Completeness + Data Flow passed clean; Efficiency surfaced **7 findings, all inherited from the vendored DLL** (none introduced by the rebuild). With the source now owned in-tree, all seven were fixed in the same session:

| # | Sev | Finding | Fix |
|---|---|---|---|
| E1 | HIGH | `OnMissionTick` allocated `new object[] { dt }` per frame (60 Hz) | Cached `_dtArgs` instance array, reused per call |
| E2 | MED | 15+ `new object[]` allocations across event handlers | Shared `EmptyArgs = Array.Empty<object>()` for empty notifications |
| E3 | MED | `FindCalledListeners` allocated `new List<>` per call | Reused instance-cached `_tempMatched` list (documented synchronous-dispatch contract) |
| E4 | MED | 18+ `list.ForEach(l => ...)` closure allocations | Rewrote as plain `for`/`foreach` |
| E5 | MED | `OnEndMissionInternal` didn't clear `actions`/`tickListeners`/`trees` dicts | Added `Clear()` calls — fixes cross-mission leak |
| E6 | MED | `Extensions.GetBehaviorTree` double dict lookup (`ContainsKey` + indexer) | `TryGetValue` |
| E7 | LOW | `BehaviorTreeAgentComponent` `new Random()` per agent | `static SharedRandom` |

## Action items completed

1. ✅ Decompile + port → `Main/BehaviorTreeWrapper/` + `Main/BehaviorTrees/`.
2. ✅ Apply inheritance fix.
3. ✅ Drop both vendored DLLs + `<Reference>` entries.
4. ✅ Regression test green (`BehaviorTreeMissionLogicInheritanceTests.cs`).
5. ✅ Full test suite: 2416 passing, 1 pre-existing unrelated failure (`GetVolunteerTroopId_EreborCulture_HighRoll` — Rhun recruitment work in flight on this branch).
6. ✅ Inherited perf cleanup E1–E7 applied.
7. ✅ `docs/features/warg-combat.md` updated to reflect inlining (was still calling the libs "Pre-compiled binary").
8. ✅ Codex Finding 1: removed manual `comp.OnTick(dt)` in WargMissionBehavior + SpiderMissionBehavior — vanilla `Agent.Tick` (line 4768) auto-calls component OnTick every frame.
9. ⏳ Open follow-up investigation for the actual NRE source in `Mission.MissionLogics`. Next steps: enumerate every `MissionBehavior` subclass in the loaded community DLLs (`Bannerlord.MBOptionScreen.v1.4.1.dll`, `Bannerlord.ButterLib.Implementation.1.4.1.dll`, `MCM.UI.Adapter.MCMv5.dll`, `MCMv5.dll`, `Bannerlord.UIExtenderEx.dll`, `BUTR.CrashReport.*.dll`) via `ilspycmd --list types`, decompile any class with `BehaviorType` override, find the one returning `Logic` while inheriting `MissionBehavior` directly.
10. ⏳ User to re-attack looters with the patched build. **Expected outcome: crash MAY still reproduce.** The patched build is no worse than before (and fixes the double-tick + adds source ownership), but doesn't yet address the actual NRE source. If users still crash, that confirms the investigation in #9 needs to start immediately.
